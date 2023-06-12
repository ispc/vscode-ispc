using LanguageServer;
using LanguageServer.Client;
using LanguageServer.Parameters;
using LanguageServer.Parameters.General;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ISPCLanguageServer;

namespace ISPCLanguageServer
{
    public static class Compiler
    {
        public class CompletedArgs
        {
            public string Output;
            public Uri DocumentUri;
        }

        private static ProcessStartInfo                     _startInfo;
        private static Thread                               _compilerThread;
        private static bool                                 _isRunning;
        private static ConcurrentQueue<TextDocumentItem>    _documents;
        private static Logger                               _logger;
        private static string                               _target;
        private static string                               _arch;
        private static string                               _CPU;
        private static string                               _TargetOS;
        private static string                               _compilerPath;

        public static event EventHandler<CompletedArgs>     Completed;

        public static void Initialize(Logger logger, string path = "c:\\devtools\\bin\\ispc.exe", string arch = "x86", string target = "avx2", string cpu = "icelake-client", string targetOS = "windows" )
        {
            _documents = new ConcurrentQueue<TextDocumentItem>();
            _logger = logger;

            _compilerPath = path;
            _target = target;
            _arch = arch;
            _CPU = cpu;
            _TargetOS = targetOS;
            UpdateStartInfo();

            _compilerThread = new Thread(new ThreadStart(CompileProc));
            _compilerThread.Start();

            _isRunning = true;
        }

        public static void Shutdown()
        {
            _isRunning = false;
            _compilerThread.Abort();
        }

        public static void Validate(TextDocumentItem document, bool forceEnqueue = false)
        {
            if (document == null)
                return;

            if (forceEnqueue == false && Enumerable.Contains<TextDocumentItem>(_documents, document, new DocumentComparer()))
                return;

            // enqueue the document
            _documents.Enqueue(document);
        }

        private static void CompileProc()
        {
            // while we are running
            while ( _isRunning )
            {
                // try to dequeue a document
                // if no documents sleep for 30ms
                TextDocumentItem doc = null;
                while ( _documents.TryDequeue( out doc ) == false )
                {
                    Thread.Sleep(30);
                }

                if (doc != null && _startInfo != null)
                {
                    // create a new process
                    Process compilerProc = new Process();
                    compilerProc.StartInfo = _startInfo;

                    // compile the file
                    try
                    {
                        compilerProc.Start();
                        _logger.Info("[ispc] - compiler started.");
                    }
                    catch (Exception ex)
                    {
                        _logger.Info("[ispc] - unable to start compiler: "+ ex.Message);
                        return;
                    }

                    // write the file to stdin
                    compilerProc.StandardInput.WriteLine(doc.text);

                    // flush and close stdin
                    compilerProc.StandardInput.Flush();
                    compilerProc.StandardInput.Close();

                    // wait for exit
                    compilerProc.WaitForExit();

                    // collect the output data
                    string stderr = compilerProc.StandardError.ReadToEnd();
                    string stdout = compilerProc.StandardOutput.ReadToEnd();

                    // completed successfully
                    _logger.Info("[ispc] - compiler completed.");

                    // if the compiler reported errors show them in the output
                    if (stderr.Length > 1)
                    {
                        _logger.Error("[ispc] - stderr - " + stderr + "\n");
                    }

                    // raise the event
                    CompletedArgs args = new CompletedArgs();
                    args.Output = stderr + stdout;
                    args.DocumentUri = doc.uri;

                    Completed?.Invoke(_startInfo, args);
                    
                    // close the process
                    compilerProc.Close();
                    compilerProc = null;
                }
            }
        }

        private class DocumentComparer : IEqualityComparer<TextDocumentItem>
        {
            public bool Equals(TextDocumentItem x, TextDocumentItem y)
            {
                if (Object.ReferenceEquals(x, y)) return true;

                if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                    return false;

                return x.uri == y.uri;
            }

            // If Equals() returns true for a pair of objects 
            // then GetHashCode() must return the same value for these objects.
            public int GetHashCode(TextDocumentItem document)
            {
                if (Object.ReferenceEquals(document, null)) return 0;

                return document.uri.GetHashCode();
            }

        }

        public static string Target
        {
            set
            {
                _target = value;
                UpdateStartInfo();
            }
            get
            {
                return _target;
            }
        }

        public static string Architecture
        {
            set
            {
                _arch = value;
                UpdateStartInfo();
            }
            get
            {
                return _arch;
            }
        }

        public static string CPU
        {
            set
            {
                _CPU = value;
                UpdateStartInfo();
            }
            get
            {
                return _CPU;
            }
        }

        public static string TargetOS
        {
            set
            {
                _TargetOS = value;
                UpdateStartInfo();
            }
            get
            {
                return _TargetOS;
            }
        }

        public static string CompilerPath
        {
            set
            {
                _compilerPath = value;

                // warn that the compiler path doesn't exist
                if (Path.IsPathRooted(value) && !File.Exists(_compilerPath))
                {
                    _logger.Error($"[ispc] - Path to compiler does not exist.  \"{_compilerPath}\"");
                }

                // update the start info
                UpdateStartInfo();
            }
            get
            {
                return _compilerPath;
            }
        }

        private static void UpdateStartInfo()
        {
            _startInfo = new ProcessStartInfo(_compilerPath);
            _startInfo.Arguments = $"--arch={_arch} --cpu={_CPU} --target={_target} --target-os={_TargetOS} -O3 -";
            _startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            _startInfo.UseShellExecute = false;
            _startInfo.RedirectStandardError = true;
            _startInfo.RedirectStandardOutput = true;
            _startInfo.RedirectStandardInput = true;
        }
    }
}
