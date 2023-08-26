using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ispc_languageserver
{
    public interface ICompiler
    {
        public abstract void Initialize();
    };

    public class IspcSettings
    {
        public string? compilerTarget { get; set; }
        public string? compilerArchitecture { get; set; }
        public string? compilerCPU { get; set; }
        public string? compilerTargetOS { get; set; }
        public string? compilerPath { get; set; }
        public int? maxNumberOfProblems { get; set; }
    }

    internal class Compiler : ICompiler
    {
        public class CompletedArgs
        {
            public string? Output;
            public DocumentUri? DocumentUri;
        }

        private readonly ILanguageServerFacade _languageServer;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ITextDocumentManager _documentManager;
        private IspcSettings _ispcSettings;
        private ILogger _logger;
        private ProcessStartInfo _startInfo;
        private ConcurrentQueue<TextDocumentItem>? _documentQueue;
        private static Thread? _compilerThread;
        private bool _isRunning = false;
        private bool _configured = false;

        public List<Diagnostic> _diagnostics;

        public Compiler(
            ILanguageServerFacade languageServer,
            ILanguageServerConfiguration configuration,
            ITextDocumentManager documentManager,
            IOptionsMonitor<IspcSettings> ispcSettingsMonitor
            )
        {
            _languageServer = languageServer;
            _configuration = configuration;
            _documentManager = documentManager;
            _ispcSettings = ispcSettingsMonitor.CurrentValue;
            ispcSettingsMonitor.OnChange(DidChangeConfiguration);
        }

        public void Initialize()
        {
            Console.Error.WriteLine("[ispc] - Starting Compiler");
            _documentQueue = _documentManager._queue;

            _compilerThread = new Thread(new ThreadStart(CompilerProc));
            _compilerThread.Start();

            _isRunning = true;
            Console.Error.WriteLine("[ispc] - Compiler is running");
        }

        private void DidChangeConfiguration(IspcSettings arg1, string? arg2)
        {
            Console.Error.WriteLine("[ispc] - Changing ISPC Settings");
            UpdateCompilerArguments();
        }

        private void UpdateCompilerArguments()
        {
            var config = _configuration.GetSection("ispc").AsEnumerable();
            _ispcSettings.compilerArchitecture = config.FirstOrDefault(setting => setting.Key == "ispc:compilerArchitecture").Value;
            _ispcSettings.compilerCPU = config.FirstOrDefault(setting => setting.Key == "ispc:compilerCPU").Value;
            _ispcSettings.compilerTarget = config.FirstOrDefault(setting => setting.Key == "ispc:compilerTarget").Value;
            _ispcSettings.compilerTargetOS = config.FirstOrDefault(setting => setting.Key == "ispc:compilerTargetOS").Value;
            _ispcSettings.compilerPath = config.FirstOrDefault(setting => setting.Key == "ispc:compilerPath").Value;
            _ispcSettings.maxNumberOfProblems = int.Parse(config.FirstOrDefault(setting => setting.Key == "ispc:maxNumberOfProblems").Value);

            Console.Error.WriteLine($"[ispc] - Target: {_ispcSettings.compilerTarget}");
            Console.Error.WriteLine($"[ispc] - OS: {_ispcSettings.compilerTargetOS}");
            Console.Error.WriteLine($"[ispc] - Architecture: {_ispcSettings.compilerArchitecture}");
            Console.Error.WriteLine($"[ispc] - CPU: {_ispcSettings.compilerCPU}");
            Console.Error.WriteLine($"[ispc] - Compiler Path: {_ispcSettings.compilerPath}");
            Console.Error.WriteLine($"[ispc] - Max Number of Problems: {_ispcSettings.maxNumberOfProblems}");
            Console.Error.WriteLine("[ispc] - Compiler Settings Updated");

            UpdateStartInfo();

            _configured = true;
        }

        private void UpdateStartInfo()
        {
            _startInfo = new ProcessStartInfo(_ispcSettings.compilerPath);
            _startInfo.Arguments = $"--arch={_ispcSettings.compilerArchitecture} --cpu={_ispcSettings.compilerCPU} --target={_ispcSettings.compilerTarget} --target-os={_ispcSettings.compilerTargetOS} -O3 -o - -";
            _startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            _startInfo.UseShellExecute = false;
            _startInfo.RedirectStandardError = true;
            _startInfo.RedirectStandardOutput = true;
            _startInfo.RedirectStandardInput = true;
        }

        public async void CompilerProc()
        {
            while(_isRunning)
            {
                TextDocumentItem doc = null;
                while(_documentQueue.TryDequeue(out doc) == false)
                {
                    Thread.Sleep(30);
                }

                if(doc != null && _startInfo != null)
                {
                    // create a new process
                    Process compilerProc = new Process();
                    compilerProc.StartInfo = _startInfo;

                    // compile the file
                    try
                    {
                        compilerProc.Start();
                        Console.Error.WriteLine("[ispc] - compiler started.");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[ispc] - unable to start compiler: "+ ex.Message);
                        Console.Error.WriteLine("[ispc] - For realtime error reporting, ensure location to ISPC is on PATH.");
                        return;
                    }

                    // write the file to stdin
                    compilerProc.StandardInput.WriteLine(doc.Text);

                    // flush and close stdin
                    compilerProc.StandardInput.Flush();
                    compilerProc.StandardInput.Close();

                    // wait for exit
                    compilerProc.WaitForExit();

                    // collect the output data
                    string stderr = compilerProc.StandardError.ReadToEnd();
                    string stdout = compilerProc.StandardOutput.ReadToEnd();

                    // completed successfully
                    Console.Error.WriteLine("[ispc] - compiler completed.");

                    // if the compiler reported errors show them in the output
                    if (stderr.Length > 1)
                    {
                        Console.Error.WriteLine("[ispc] - stderr - " + stderr + "\n");
                    }

                    CompletedArgs args = new CompletedArgs();
                    args.Output = stderr + stdout;
                    args.DocumentUri = doc.Uri;

                    _compiler_Completed(args);

                    // close the process
                    compilerProc.Close();
                    compilerProc = null;
                }
            }
        }

        private Range GetDiagnosticRange(Capture line, Capture column)
        {
            Position p = new Position
            {
                Line = int.Parse(line.Value),
                Character = int.Parse(column.Value)
            };

            // for some reason vscode is adding 1 to each of these values
            // the values are correct in the jsonrpc, but the UI is increasing them by one
            p.Line -= 1;
            p.Character -= 1;

            Range r = new Range
            {
                Start = p,
                End = p
            };

            return r;
        }

        private string GetInfo(string output, MatchCollection mc, int index)
        {
            // if there's another match we wan to grab the text between the last match group
            // and the next match group
            if (index + 1 < mc.Count)
            {
                Match m = mc[index];
                Match m2 = mc[index + 1];

                int startIndex = m.Index + m.Length;
                int length = m2.Index - startIndex;

                return output.Substring(startIndex, length);
            }
            else
            {
                // there isn't another match so we want to grab the text between the last match and the end of the file
                Match m = mc[index];

                int startIndex = m.Index + m.Length;

                return output.Substring(startIndex, output.Length - startIndex);
            }
        }
        private DiagnosticSeverity GetDiagnosticSeverity(Capture cap)
        {
            string s = cap.Value.Trim().ToLower();
            switch (s)
            {
                case "performance warning":
                case "warning":
                    return DiagnosticSeverity.Warning;
                case "error":
                    return DiagnosticSeverity.Error;
                default:
                    return DiagnosticSeverity.Information;
            }
        }

        private void _compiler_Completed(CompletedArgs args)
        {
            if ( args.Output == null )
            {
                return;
            }

            //@"^((.*):(\d+):(\d+):\s+(Performance Warning|Warning|Error|warning|error):\s+(.*))$"
            // Group 0 = Complete warning/error message
            // Group 1 = Complete warning/error message
            // Group 2 = File
            // Group 3 = Line
            // Group 4 = Column
            // Group 5 = Severity
            // Group 6 = Message
            Regex rx = new Regex(@"^((.*):(\d+):(\d+):\s+(Performance Warning|Warning|Error|warning|error):\s+(.*))$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

            MatchCollection matches = rx.Matches(args.Output);

            var diagnostics = new List<Diagnostic>();
            for (int i = 0; i < matches.Count && i < _ispcSettings.maxNumberOfProblems; i++)
            {
                Match m = matches[i];
                Range range = GetDiagnosticRange(m.Groups[3], m.Groups[4]);

                DiagnosticRelatedInformation r = new DiagnosticRelatedInformation
                {
                    Location = new Location { Range = range, Uri = args.DocumentUri },
                    Message = GetInfo(args.Output, matches, i).Trim(),
                };

                Diagnostic d = new Diagnostic
                {
                    Range = range,
                    Severity = GetDiagnosticSeverity(m.Groups[5]),
                    Message = m.Groups[6].Value,
                    RelatedInformation = new DiagnosticRelatedInformation[] { r },
                    Source = "ispc"
                };

                diagnostics.Add(d);
            }

            var diagParams = new PublishDiagnosticsParams
            {
                Uri = args.DocumentUri,
                Diagnostics = diagnostics,
            };


            // Send the diagnostics to the client
            _languageServer.TextDocument.PublishDiagnostics(diagParams);
        }
    }
}
