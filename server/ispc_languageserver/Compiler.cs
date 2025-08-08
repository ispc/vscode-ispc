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
using System.Linq.Expressions;

namespace ispc_languageserver
{
    public interface ICompiler
    {
        public abstract void Initialize();
        public abstract void Shutdown();
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
            public string Output = string.Empty;
            public DocumentUri? DocumentUri;
        }

        private readonly ILanguageServerFacade _languageServer;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ITextDocumentManager _documentManager;
        private IspcSettings _ispcSettings;
        private ProcessStartInfo? _startInfo;
        private ConcurrentQueue<TextDocumentItem>? _documentQueue;
        private static Thread? _compilerThread;
        public bool _isRunning = false;

        public List<Diagnostic> _diagnostics = new List<Diagnostic>();

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

            // Try to get settings with fallback
            try
            {
                _ispcSettings = ispcSettingsMonitor.CurrentValue ?? new IspcSettings();
            }
            catch (Exception)
            {
                _ispcSettings = new IspcSettings
                {
                    compilerPath = "ispc",
                    compilerTarget = "host",
                    maxNumberOfProblems = 100
                };
            }

            ispcSettingsMonitor.OnChange(DidChangeConfiguration);
        }

        public void Initialize()
        {
            _documentQueue = _documentManager._queue;
            _compilerThread = new Thread(new ThreadStart(CompilerProc)) { IsBackground = true };
            _compilerThread.Start();
            _isRunning = true;
            Console.Error.WriteLine("[ispc] - Compiler initialized");
        }

        public void Shutdown()
        {
            _isRunning = false;

            // Wait for compiler thread to finish
            if (_compilerThread != null && _compilerThread.IsAlive)
            {
                _compilerThread.Join(1000); // Wait up to 1 second
            }
        }

        private void DidChangeConfiguration(IspcSettings arg1, string? arg2)
        {
            UpdateCompilerArguments();
        }

        private void UpdateCompilerArguments()
        {
            var config = _configuration.GetSection("ispc").AsEnumerable();

            var archSetting = config.FirstOrDefault(setting => setting.Key == "ispc:compilerArchitecture");
            _ispcSettings.compilerArchitecture = archSetting.Value;

            var cpuSetting = config.FirstOrDefault(setting => setting.Key == "ispc:compilerCPU");
            _ispcSettings.compilerCPU = cpuSetting.Value;

            var targetSetting = config.FirstOrDefault(setting => setting.Key == "ispc:compilerTarget");
            _ispcSettings.compilerTarget = targetSetting.Value ?? "host";

            var osSetting = config.FirstOrDefault(setting => setting.Key == "ispc:compilerTargetOS");
            _ispcSettings.compilerTargetOS = osSetting.Value;

            var pathSetting = config.FirstOrDefault(setting => setting.Key == "ispc:compilerPath");
            _ispcSettings.compilerPath = pathSetting.Value ?? "ispc";

            var maxProblemsSetting = config.FirstOrDefault(setting => setting.Key == "ispc:maxNumberOfProblems");
            var maxProblemsStr = maxProblemsSetting.Value ?? "100";
            if (int.TryParse(maxProblemsStr, out int maxProblems))
            {
                _ispcSettings.maxNumberOfProblems = maxProblems;
            }
            else
            {
                _ispcSettings.maxNumberOfProblems = 100;
            }

            UpdateStartInfo();
        }

        private void UpdateStartInfo()
        {
            if (_ispcSettings.compilerPath == null)
            {
                return;
            }

            // Test if compiler exists
            try
            {
                var testProcess = new Process();
                testProcess.StartInfo = new ProcessStartInfo(_ispcSettings.compilerPath, "--version")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                testProcess.Start();
                testProcess.WaitForExit(2000); // 2 second timeout
                testProcess.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ispc] - Compiler not found at '{_ispcSettings.compilerPath}': {ex.Message}");
                Console.Error.WriteLine("[ispc] - Diagnostics disabled. Install ISPC compiler or update ispc.compilerPath setting");
                return;
            }

            _startInfo = new ProcessStartInfo(_ispcSettings.compilerPath);

            // Build arguments dynamically based on what's configured
            var args = new List<string>();

            if (!string.IsNullOrEmpty(_ispcSettings.compilerArchitecture))
                args.Add($"--arch={_ispcSettings.compilerArchitecture}");

            if (!string.IsNullOrEmpty(_ispcSettings.compilerCPU))
                args.Add($"--cpu={_ispcSettings.compilerCPU}");

            if (!string.IsNullOrEmpty(_ispcSettings.compilerTarget))
                args.Add($"--target={_ispcSettings.compilerTarget}");

            if (!string.IsNullOrEmpty(_ispcSettings.compilerTargetOS))
                args.Add($"--target-os={_ispcSettings.compilerTargetOS}");

            // Always add optimization and output settings
            args.Add("-O3");
            args.Add("-o");
            args.Add("-");
            args.Add("-");

            _startInfo.Arguments = string.Join(" ", args);
            _startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            _startInfo.UseShellExecute = false;
            _startInfo.RedirectStandardError = true;
            _startInfo.RedirectStandardOutput = true;
            _startInfo.RedirectStandardInput = true;
        }

        public void CompilerProc()
        {
            while (_isRunning)
            {
                TextDocumentItem? doc = null;
                if (_documentQueue != null)
                {
                    while (_documentQueue.TryDequeue(out doc) == false)
                    {
                        Thread.Sleep(30);
                    }
                }

                if (doc != null && _startInfo != null)
                {
                    // create a new process
                    Process? compilerProc = new Process();
                    compilerProc.StartInfo = _startInfo;

                    // compile the file
                    try
                    {
                        compilerProc.Start();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[ispc] - Unable to start compiler: {ex.Message}");
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

                    Console.Error.WriteLine($"[ispc] - Compiled document: {doc.Uri}");

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
                case "fatal error":
                    return DiagnosticSeverity.Error;
                default:
                    return DiagnosticSeverity.Information;
            }
        }

        private void _compiler_Completed(CompletedArgs args)
        {
            if (args.Output == null)
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
            Regex rx = new Regex(@"^((.*):(\d+):(\d+):\s+(Performance Warning|Warning|Error|warning|error|Fatal Error):\s+(.*))$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

            MatchCollection matches = rx.Matches(args.Output);

            var diagnostics = new List<Diagnostic>();
            for (int i = 0; i < matches.Count && i < _ispcSettings.maxNumberOfProblems; i++)
            {
                Match m = matches[i];
                Range range = GetDiagnosticRange(m.Groups[3], m.Groups[4]);

                DiagnosticRelatedInformation r = new DiagnosticRelatedInformation
                {
                    Location = new Location { Range = range, Uri = args.DocumentUri ?? "" },
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
                Uri = args.DocumentUri ?? "",
                Diagnostics = diagnostics,
            };


            // Send the diagnostics to the client
            try
            {
                _languageServer.TextDocument.PublishDiagnostics(diagParams);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ispc] - Failed to publish diagnostics: {ex.Message}");
                // Don't rethrow - this is not critical enough to crash the server
            }
        }
    }
}
