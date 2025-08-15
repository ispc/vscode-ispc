using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ispc_languageserver
{
    public class CompletionItem
    {
        public string FunctionName { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public List<string> Parameters { get; set; } = new List<string>();
    }

    internal class CompletionHandler : ICompletionHandler
    {
        private readonly ILogger<CompletionHandler> _logger;
        private readonly ITextDocumentManager _documents;
        private readonly List<CompletionItem> _stdlibFunctions = new List<CompletionItem>();
        private readonly List<string> _ispcKeywords = new List<string>
        {
            // TODO: to be extended
            "programIndex",
            "programCount",
            "threadIndex",
            "threadCount",
            "taskIndex",
            "taskCount",
            "uniform",
            "varying"
        };

        private readonly List<string> _ispcTypes = new List<string>
        {
            "int",
            "int8",
            "int16",
            "int32",
            "int64",
            "uint8",
            "uint16",
            "uint32",
            "uint64",
            "float",
            "float16",
            "double",
            "bool"
        };

        public CompletionHandler(ILogger<CompletionHandler> logger, ITextDocumentManager documents)
        {
            _logger = logger;
            _documents = documents;
            LoadStdlibFunctions();
        }

        public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                    new TextDocumentFilter
                    {
                        Pattern = "**/*.ispc"
                    }
                ),
                TriggerCharacters = new Container<string>(),
                ResolveProvider = false,
                AllCommitCharacters = new Container<string>("(", " ", "\t")
            };
        }

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            await Task.Yield();

            var completionItems = new List<OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem>();

            // Get the document content
            if (!_documents.Documents.ContainsKey(request.TextDocument.Uri))
            {
                return new CompletionList(completionItems);
            }

            var documentInfo = _documents.Documents[request.TextDocument.Uri];
            var lines = documentInfo.Document.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (request.Position.Line >= lines.Length)
            {
                return new CompletionList(completionItems);
            }

            var currentLine = lines[request.Position.Line];
            var currentPosition = Math.Min(request.Position.Character, currentLine.Length);
            var textBeforeCursor = currentLine.Substring(0, currentPosition);

            // Extract the word being typed
            var wordMatch = Regex.Match(textBeforeCursor, @"(\w*)$");
            var partialWord = wordMatch.Success ? wordMatch.Groups[1].Value : "";

            // Filter stdlib functions based on partial word (allow empty partial word for Ctrl+Space)
            var filteredFunctions = _stdlibFunctions
                .Where(f => string.IsNullOrEmpty(partialWord) || f.FunctionName.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Filter ISPC keywords
            var filteredKeywords = _ispcKeywords
                .Where(k => string.IsNullOrEmpty(partialWord) || k.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Filter ISPC types
            var filteredTypes = _ispcTypes
                .Where(t => string.IsNullOrEmpty(partialWord) || t.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Add user-defined symbols from AST
            var userDefinitions = documentInfo.Definitions
                .Where(d => d.Identifier != null && d.Identifier.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Create completion items for stdlib functions
            foreach (var func in filteredFunctions)
            {
                var completionItem = new OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem
                {
                    Label = func.FunctionName,
                    Kind = CompletionItemKind.Function,
                    Detail = func.ReturnType,
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = $"```ispc\n{func.Signature}\n```\n\nISPC standard library function"
                    },
                    InsertText = func.FunctionName,
                    InsertTextFormat = InsertTextFormat.PlainText,
                    SortText = "C" + func.FunctionName // Third priority (after keywords and types)
                };

                completionItems.Add(completionItem);
            }

            // Create completion items for ISPC keywords
            foreach (var keyword in filteredKeywords)
            {
                var completionItem = new OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem
                {
                    Label = keyword,
                    Kind = CompletionItemKind.Keyword,
                    Detail = "ISPC built-in variable",
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = GetKeywordDocumentation(keyword)
                    },
                    InsertText = keyword,
                    InsertTextFormat = InsertTextFormat.PlainText,
                    SortText = "A" + keyword // Highest priority (before stdlib functions)
                };

                completionItems.Add(completionItem);
            }

            // Create completion items for ISPC types
            foreach (var type in filteredTypes)
            {
                var completionItem = new OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem
                {
                    Label = type,
                    Kind = CompletionItemKind.TypeParameter,
                    Detail = "ISPC built-in type",
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = GetTypeDocumentation(type)
                    },
                    InsertText = type,
                    InsertTextFormat = InsertTextFormat.PlainText,
                    SortText = "B" + type // Second priority (after keywords, before stdlib functions)
                };

                completionItems.Add(completionItem);
            }

            // Create completion items for user-defined symbols
            foreach (var definition in userDefinitions)
            {
                var completionItem = new OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem
                {
                    Label = definition.Identifier ?? "",
                    Kind = CompletionItemKind.Variable,
                    Detail = "User-defined identifier",
                    InsertText = definition.Identifier ?? "",
                    InsertTextFormat = InsertTextFormat.PlainText,
                    SortText = "D" + (definition.Identifier ?? "") // Lowest priority
                };

                completionItems.Add(completionItem);
            }


            return new CompletionList(completionItems, isIncomplete: false);
        }

        private string GetKeywordDocumentation(string keyword)
        {
            return keyword switch
            {
                "programIndex" => "```ispc\nvarying int programIndex\n```\n\nThe index of the current program instance in the gang. " +
                                "Values range from 0 to `programCount-1`.\n\nThis variable provides the unique identifier for each program instance when executing in SPMD mode.",
                "programCount" => "```ispc\nuniform int programCount\n```\n\nThe total number of program instances in the gang (the gang size). " +
                                "This is typically the target width (e.g., 4 for SSE, 8 for AVX, 16 for AVX-512).\n\nUseful for algorithms that need to know the total parallelism available.",
                "taskIndex" => "```ispc\nvarying int taskIndex\n```\n\nAvailable within each task - ranges from zero to one minus the number of tasks provided to launch. ",
                "taskCount" => "```ispc\nuniform int taskCount\n```\n\nEquals the number of launched tasks. " +
                              "Available within each task along with `taskIndex`.\n\n",
                "threadIndex" => "```ispc\nvarying int threadIndex\n```\n\nAvailable inside functions with the `task` qualifier. " +
                                "Provides an index between zero and `threadCount-1` that gives a unique index corresponding to the hardware thread executing the current task.\n\n",
                "threadCount" => "```ispc\nuniform int threadCount\n```\n\nAvailable inside functions with the `task` qualifier. " +
                                "Gives the total number of hardware threads that have been launched by the task system.\n\n",
                _ => $"ISPC built-in keyword: `{keyword}`"
            };
        }

        private string GetTypeDocumentation(string type)
        {
            return $"ISPC built-in type: `{type}`";
        }

        private void LoadStdlibFunctions()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "ispc_languageserver.ispc_stdlib.txt";


                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        _logger.LogError($"Embedded resource not found: {resourceName}");

                        // List available resources for debugging
                        var resources = assembly.GetManifestResourceNames();
                        _logger.LogInformation($"Available resources: {string.Join(", ", resources)}");
                        return;
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        var content = reader.ReadToEnd();
                        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);


                        var functionRegex = new Regex(@"^(?:inline\s+)?(?:uniform\s+|varying\s+)?(?:unmasked\s+)?(\w+(?:\s*\*)?)\s+(\w+)\s*\((.*?)\)$");
                        var parsedCount = 0;

                        foreach (var line in lines)
                        {
                            var match = functionRegex.Match(line.Trim());
                            if (match.Success)
                            {
                                var returnType = match.Groups[1].Value.Trim();
                                var functionName = match.Groups[2].Value.Trim();
                                var parametersStr = match.Groups[3].Value.Trim();

                                // Skip duplicate function names (overloads)
                                if (_stdlibFunctions.Any(f => f.FunctionName == functionName))
                                    continue;

                                var parameters = new List<string>();
                                if (!string.IsNullOrEmpty(parametersStr))
                                {
                                    // Simple parameter parsing - split by comma and clean up
                                    parameters = parametersStr.Split(',')
                                        .Select(p => p.Trim())
                                        .Where(p => !string.IsNullOrEmpty(p))
                                        .ToList();
                                }

                                var completionItem = new CompletionItem
                                {
                                    FunctionName = functionName,
                                    ReturnType = returnType,
                                    Signature = line.Trim(),
                                    Parameters = parameters
                                };

                                _stdlibFunctions.Add(completionItem);
                                parsedCount++;
                            }
                        }

                        _logger.LogInformation($"Loaded {_stdlibFunctions.Count} stdlib functions for completion");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load stdlib functions");
            }
        }
    }
}