using LanguageServer;
using LanguageServer.Client;
using LanguageServer.Parameters;
using LanguageServer.Parameters.General;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.RegularExpressions;


namespace ISPCLanguageServer
{
    // a class to house the built in functions
    public class ISPCBuiltin
    {
        private String          _definition;
        private String          _name;
        private String          _return;
        private List<string>    _parameters;
        private String          _description;

        public string Definition =>                 _definition;
        public string Name =>                       _name;
        public string Return =>                     _return;
        public IReadOnlyList<string> Parameters =>  _parameters;
        public string Description =>                _description;

        public ISPCBuiltin()
        {
        }

        public ISPCBuiltin( string definition, string name, string @return, string[] parameters, string description )
        {
            _definition = definition.Trim();
            _name = name.Trim();
            _return = @return.Trim();
            _parameters = new List<string>(parameters);
            _description = description.Trim();

            foreach ( string s in _parameters )
            {
                s.Trim();
            }
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.Append(_return);
            s.Append(" ");
            s.Append(_name);
            s.Append("(");
            for( int i = 0; i < _parameters.Count; i++ )
            {
                if ( i > 0 )
                {
                    s.Append(", ");
                }
                s.Append(_parameters[i]);
            }
            s.Append(")");

            return s.ToString();
        }
    }

    public class App : ServiceConnection
    {
        private Uri _workerSpaceRoot;
        private int _maxNumberOfProblems = 1000;
        private TextDocumentManager _documents;
        private List<ISPCBuiltin> _builtins;
        private List<CompletionItem> _completionItems;
        private HashSet<int> _completionItemHashSet;
        private Dictionary<int, List<SignatureInformation>> _funcToSigInfo;

        public App(Stream input, Stream output)
            : base(input, output)
        {
            _documents = new TextDocumentManager();
            _documents.Changed += Documents_Changed;
            _builtins = new List<ISPCBuiltin>();
            _completionItems = new List<CompletionItem>();
            _completionItemHashSet = new HashSet<int>();
            _funcToSigInfo = new Dictionary<int, List<SignatureInformation>>();

            Initialize();
        }

        protected override void Exit()
        {
            base.Exit();
        }

        private void Initialize()
        {
            LoadBuiltins();
            InitKeywords();
            InitBuiltinCompletion();

            Compiler.Initialize(Logger.Instance);
            Compiler.Completed += _compiler_Completed;
        }

        private void LoadBuiltins()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            System.IO.Stream stdlib = null;

            string[] test = assembly.GetManifestResourceNames();
            foreach (string file in test )
            {
                if ( file.Contains("ispc_stdlib.txt") )
                {
                    stdlib = assembly.GetManifestResourceStream(file);
                    break;
                }
            }

            try
            {
                using (StreamReader sr = new StreamReader(stdlib))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        int open = line.IndexOf('(');
                        int close = line.IndexOf(')');

                        // find the index of the first whitespace character from the end
                        // everything before that is the return value, and after that is the function name.
                        int end = open-1;
                        int i = end; // strings are 0 based
                        while (!Char.IsWhiteSpace(line[i]) && i-- >= 0){}

                        // get the return portion of the function definition
                        string ret = line.Substring(0, i);
                        string func = line.Substring(i+1, open-i-1);

                        // get the substring of the parameter list, replace the open and close
                        // parens with nothing then split based on the ',' character
                        string paramstring = line.Substring(open, close - open);
                        paramstring = paramstring.Replace("(", "");
                        paramstring = paramstring.Replace(")", "");
                        string[] parameters = paramstring.Split(',');

                        _builtins.Add(new ISPCBuiltin(line, func, ret, parameters, line));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The builtin file could not be read:");
                Console.WriteLine(e.Message);

                _builtins.Clear();
            }

#if ISPC_DEBUG
            Console.WriteLine("Loaded ISPC Builtins:");
            foreach( ISPCBuiltin i in _builtins )
            {
                Console.WriteLine( i.ToString() );
            }
            Console.WriteLine("");
#endif
        }

        private void InitKeywords()
        {
            // add keywords
            string[] controlKeywords = "break|case|continue|default|do|cdo|else|for|cfor|foreach|foreach_tiled|foreach_active|foreach_unique|goto|if|cif|launch|return|soa|switch|sync|task|while|cwhile|unmasked".Split('|');
            string[] typeKeywords = "bool|char|double|enum|float|int|long|short|signed|struct|typedef|union|unsigned|void|int8|int16|int32|int64|uint8|uint16|uint32|uint64".Split('|');
            string[] modifierKeywords = "const|uniform|varying|extern|export|register|static|volatile|inline".Split('|');

            foreach (string s in controlKeywords)
            {
                var com = new CompletionItem();
                com.label = s;
                com.kind = CompletionItemKind.Keyword;
                com.data = s.GetHashCode();

                _completionItems.Add(com);
                _completionItemHashSet.Add(com.data);
            }

            foreach (string s in typeKeywords)
            {
                var com = new CompletionItem();
                com.label = s;
                com.kind = CompletionItemKind.Keyword;
                com.data = s.GetHashCode();

                _completionItems.Add(com);
                _completionItemHashSet.Add(com.data);
            }

            foreach (string s in modifierKeywords)
            {
                var com = new CompletionItem();
                com.label = s;
                com.kind = CompletionItemKind.Keyword;
                com.data = s.GetHashCode();

                _completionItems.Add(com);
                _completionItemHashSet.Add(com.data);
            }
        }

        private void InitBuiltinCompletion()
        {
            foreach (ISPCBuiltin b in _builtins)
            {
                // add this to the completion set
                if (!_completionItemHashSet.Contains(b.Name.GetHashCode()))
                {
                    var com = new CompletionItem();
                    com.label = b.Name;
                    com.kind = CompletionItemKind.Function;
                    com.data = b.Name.GetHashCode();
                    _completionItems.Add(com);
                    _completionItemHashSet.Add(com.data);
                }

                // add the signature information from the
                SignatureInformation s = new SignatureInformation();
                s.label = b.Definition;

                List<ParameterInformation> paramInfo = new List<ParameterInformation>();
                foreach (string p in b.Parameters)
                {
                    ParameterInformation param = new ParameterInformation();
                    param.label = p;
                    paramInfo.Add(param);
                }
                s.parameters = paramInfo.ToArray();

                if (!_funcToSigInfo.ContainsKey(b.Name.GetHashCode()))
                {
                    _funcToSigInfo.Add(b.Name.GetHashCode(), new List<SignatureInformation>());
                }

                List<SignatureInformation> overrides = _funcToSigInfo[b.Name.GetHashCode()];
                overrides.Add(s);
            }
        }

        private void Documents_Changed(object sender, TextDocumentChangedEventArgs e)
        {
            Compiler.Validate(e.Document);
        }

        protected override Result<InitializeResult, ResponseError<InitializeErrorData>> Initialize(InitializeParams @params)
        {
            _workerSpaceRoot = @params.rootUri;
            var result = new InitializeResult
            {
                capabilities = new ServerCapabilities
                {
                    textDocumentSync = TextDocumentSyncKind.Full,
                    completionProvider = new CompletionOptions
                    {
                        resolveProvider = false
                    },

                    signatureHelpProvider = new SignatureHelpOptions
                    {
                        triggerCharacters = new string[]{ "(", "," }
                    }
                }
            };
            return Result<InitializeResult, ResponseError<InitializeErrorData>>.Success(result);
        }

        protected override void DidSaveTextDocument(DidSaveTextDocumentParams @params)
        {
            base.DidSaveTextDocument(@params);

            foreach (var document in _documents.All)
            {
                Compiler.Validate(document, true);
            }
        }

        protected override void DidOpenTextDocument(DidOpenTextDocumentParams @params)
        {
            _documents.Add(@params.textDocument);
            Logger.Instance.Info($"[ispc] - {@params.textDocument.uri} opened.");
        }

        protected override void DidChangeTextDocument(DidChangeTextDocumentParams @params)
        {
            _documents.Change(@params.textDocument.uri, @params.textDocument.version, @params.contentChanges);
            Logger.Instance.Info($"[ispc] - {@params.textDocument.uri} changed.");
        }

        protected override void DidCloseTextDocument(DidCloseTextDocumentParams @params)
        {
            _documents.Remove(@params.textDocument.uri);
            Logger.Instance.Info($"[ispc] - {@params.textDocument.uri} closed.");
        }

        protected override void DidChangeConfiguration(DidChangeConfigurationParams @params)
        {
            _maxNumberOfProblems = @params?.settings?.ispc?.maxNumberOfProblems ?? _maxNumberOfProblems;
            Logger.Instance.Info($"[ispc] - maxNumberOfProblems is set to {_maxNumberOfProblems}.");

            Compiler.Target = @params?.settings?.ispc?.compilerTarget ?? Compiler.Target;
            Logger.Instance.Info($"[ispc] - compilerTarget is set to \"{Compiler.Target}\".");

            Compiler.Architecture = @params?.settings?.ispc?.compilerArchitecture ?? Compiler.Architecture;
            Logger.Instance.Info($"[ispc] - compilerArchitecture is set to {Compiler.Architecture}.");

            Compiler.CPU = @params?.settings?.ispc?.compilerCPU ?? Compiler.CPU;
            Logger.Instance.Info($"[ispc] - compilerCPU is set to {Compiler.CPU}.");

            Compiler.TargetOS = @params?.settings?.ispc?.compilerTargetOS ?? Compiler.TargetOS;
            Logger.Instance.Info($"[ispc] - compilerTargetOS is set to {Compiler.TargetOS}.");

            Compiler.CompilerPath = @params?.settings?.ispc?.compilerPath ?? Compiler.CompilerPath;
            Logger.Instance.Info($"[ispc] - compilerPath is set to {Compiler.CompilerPath}.");

            // force a recompile of all active documents
            foreach (var document in _documents.All)
            {
                Compiler.Validate(document, true);
            }
        }

        private DiagnosticSeverity GetDiagnosticSeverity(Capture cap)
        {
            string s = cap.Value.Trim().ToLower();
            switch (s)
            {
                case "performance warning":
                //                    return DiagnosticSeverity.Hint;
                case "warning":
                    return DiagnosticSeverity.Warning;
                case "error":
                    return DiagnosticSeverity.Error;
                default:
                    return DiagnosticSeverity.Information;
            }
        }

        private Range GetDiagnosticRange(Capture line, Capture column)
        {
            Position p = new Position
            {
                line = long.Parse(line.Value),
                character = long.Parse(column.Value)
            };

            // for some reason vscode is adding 1 to each of these values
            // the values are correct in the jsonrpc, but the UI is increasing them by one
            p.line -= 1;
            p.character -= 1;

            Range r = new Range
            {
                start = p,
                end = p
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

        private void _compiler_Completed(object sender, Compiler.CompletedArgs args)
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
            for (int i = 0; i < matches.Count && i < _maxNumberOfProblems; i++)
            {
                Match m = matches[i];

                Diagnostic d = new Diagnostic();
                d.range = GetDiagnosticRange(m.Groups[3], m.Groups[4]);
                d.severity = GetDiagnosticSeverity(m.Groups[5]);
                d.message = m.Groups[6].Value;

                DiagnosticRelatedInformation r = new DiagnosticRelatedInformation();
                r.location = new Location { range = d.range, uri = args.DocumentUri };
                r.message = GetInfo(args.Output, matches, i).Trim(); ;
                d.relatedInformation = new DiagnosticRelatedInformation[] { r };

                d.source = "ispc";

                diagnostics.Add(d);
            }

            Proxy.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                uri = args.DocumentUri,
                diagnostics = diagnostics.ToArray()
            });
        }

        protected override void DidChangeWatchedFiles(DidChangeWatchedFilesParams @params)
        {
            Logger.Instance.Info("[ispc] - Received a file change event");
            
        }

        protected override Result<CompletionResult, ResponseError> Completion(CompletionParams @params)
        {
            return Result<CompletionResult, ResponseError>.Success(_completionItems.ToArray());
        }

        protected override Result<CompletionItem, ResponseError> ResolveCompletionItem(CompletionItem @params)
        {
            return Result<CompletionItem, ResponseError>.Success(@params);
        }

        protected override Result<SignatureHelp, ResponseError> SignatureHelp(TextDocumentPositionParams pos)
        {
            //string shuffle = "shuffle(float v, int i)";
            SignatureHelp test = new SignatureHelp();
            test.activeParameter = -1;

            string[] currentDocument = _documents.AllLines[pos.textDocument.uri];
            if (currentDocument == null)
            {
                return Result<SignatureHelp, ResponseError>.Success(test);
            }

            string currentLine = currentDocument[pos.position.line];
            if ( currentLine !=null )
            {
                try
                {
                    // early out in case the first character is a close brace, or a semicolon
                    int lineEnd = (int)pos.position.character - 1;
                    if (currentLine[lineEnd] == ')' || currentLine[lineEnd] == ';')
                    {
                        return Result<SignatureHelp, ResponseError>.Success(test);
                    }

                    // find the first occurance of a ( character from the current position
                    // this will be the end of the function name
                    int funcEndIndex = lineEnd;
                    for ( ; funcEndIndex >= 0; funcEndIndex-- )
                    {
                        if ( currentLine[funcEndIndex] == '(' )
                        {
                            funcEndIndex--;
                            break;
                        }
                    }

                    // run backward from the end of the function until we hit whitespace or
                    // the first element
                    int funcStartIndex = funcEndIndex;
                    for ( ; funcStartIndex >= 0; funcStartIndex-- )
                    {
                        if ( Char.IsWhiteSpace( currentLine[funcStartIndex] ) )
                        {
                            funcStartIndex++;
                            break;
                        }
                    }

                    // if we didn't identify a function, return
                    if (funcStartIndex < 0)
                        return Result<SignatureHelp, ResponseError>.Success(test);

                    // the length of the function name
                    int funcLength = funcEndIndex - funcStartIndex + 1;

                    // now that we have the function name
                    string funcName = currentLine.Substring(funcStartIndex, funcLength);

                    // lets see if it is in our collection, if it is we'll add it to the list
                    // of signatures
                    if (_funcToSigInfo.ContainsKey(funcName.GetHashCode()))
                    {
                        List<SignatureInformation> funcs = _funcToSigInfo[funcName.GetHashCode()];
                        test.signatures = funcs.ToArray();

                        // we found a function so we must be on the first active parameter
                        test.activeParameter = 0;

                        // try setting the active parameter by using the number of commas found after the '(' symbol
                        // and our cursor location.
                        for (int j = funcEndIndex; j < currentLine.Length; j++)
                        {
                            if (currentLine[j] == ',')
                                test.activeParameter++;
                        }
                    }
                }
                catch( Exception e )
                {
                    Logger.Instance.Warn("[ispc] - Signature matcher exception: " + e.ToString());
                }
            }

            return Result<SignatureHelp, ResponseError>.Success(test);
        }

        // Handle custom commands from client
        protected override Result<dynamic, ResponseError> ExecuteCommand(ExecuteCommandParams @params)
        {
            switch(@params.command)
            {
                case "CompileDebug":
                    Compiler.CompileDebug(@params.arguments[0].fsPath.ToString());
                    break;
            }
            return Result<dynamic, ResponseError>.Success("Success");
        }

        protected override VoidResult<ResponseError> Shutdown()
        {
            Logger.Instance.Shutdown();
            Compiler.Shutdown();

            Task.Delay(1000).ContinueWith(_ => Environment.Exit(0));

            return VoidResult<ResponseError>.Success();
        }
    }
}
