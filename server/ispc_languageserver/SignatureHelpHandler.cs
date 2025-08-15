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
    public class FunctionOverload
    {
        public string FunctionName { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string FullSignature { get; set; } = string.Empty;
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
    }

    public class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string FullParameter { get; set; } = string.Empty;
    }

    internal class SignatureHelpHandler : ISignatureHelpHandler
    {
        private readonly ILogger<SignatureHelpHandler> _logger;
        private readonly ITextDocumentManager _documents;
        private readonly Dictionary<string, List<FunctionOverload>> _stdlibFunctions = new Dictionary<string, List<FunctionOverload>>();

        public SignatureHelpHandler(ILogger<SignatureHelpHandler> logger, ITextDocumentManager documents)
        {
            _logger = logger;
            _documents = documents;
            LoadStdlibFunctions();
        }

        public SignatureHelpRegistrationOptions GetRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SignatureHelpRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(
                    new TextDocumentFilter
                    {
                        Pattern = "**/*.ispc"
                    },
                    new TextDocumentFilter
                    {
                        Pattern = "**/*.isph"
                    }
                ),
                TriggerCharacters = new Container<string>("(", ","),
                RetriggerCharacters = new Container<string>()
            };
        }

        public async Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            await Task.Yield();

            if (!_documents.Documents.ContainsKey(request.TextDocument.Uri))
            {
                return new SignatureHelp();
            }

            var documentInfo = _documents.Documents[request.TextDocument.Uri];
            var lines = documentInfo.Document.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (request.Position.Line >= lines.Length)
            {
                return new SignatureHelp();
            }

            var currentLine = lines[request.Position.Line];
            var currentPosition = Math.Min(request.Position.Character, currentLine.Length);
            var textBeforeCursor = currentLine.Substring(0, currentPosition);

            // Find the function call context
            var functionContext = FindFunctionCall(textBeforeCursor);
            if (functionContext == null)
            {
                return new SignatureHelp();
            }

            // Look up function overloads
            if (!_stdlibFunctions.TryGetValue(functionContext.FunctionName, out var overloads))
            {
                return new SignatureHelp();
            }

            var signatures = new List<SignatureInformation>();

            foreach (var overload in overloads)
            {
                var parameters = new List<ParameterInformation>();

                foreach (var param in overload.Parameters)
                {
                    parameters.Add(new ParameterInformation
                    {
                        Label = param.FullParameter,
                        Documentation = new MarkupContent
                        {
                            Kind = MarkupKind.Markdown,
                            Value = $"**{param.Type}** {param.Name}"
                        }
                    });
                }

                var signature = new SignatureInformation
                {
                    Label = overload.FullSignature,
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = $"```ispc\n{overload.FullSignature}\n```\n\nISPC standard library function"
                    },
                    Parameters = new Container<ParameterInformation>(parameters)
                };

                signatures.Add(signature);
            }

            var activeSignature = 0; // Default to first overload
            var activeParameter = Math.Max(0, Math.Min(functionContext.ParameterIndex,
                signatures.Count > 0 ? signatures[activeSignature].Parameters?.Count() - 1 ?? 0 : 0));


            return new SignatureHelp
            {
                Signatures = new Container<SignatureInformation>(signatures),
                ActiveSignature = activeSignature,
                ActiveParameter = activeParameter
            };
        }

        private FunctionCallContext? FindFunctionCall(string textBeforeCursor)
        {
            // Find the last opening parenthesis that hasn't been closed
            var parenStack = 0;
            var functionStartIndex = -1;
            var parameterCount = 0;

            for (int i = textBeforeCursor.Length - 1; i >= 0; i--)
            {
                char c = textBeforeCursor[i];

                if (c == ')')
                {
                    parenStack++;
                }
                else if (c == '(')
                {
                    parenStack--;
                    if (parenStack < 0)
                    {
                        // Found the opening paren of current function call
                        functionStartIndex = i;
                        break;
                    }
                }
                else if (c == ',' && parenStack == 0)
                {
                    parameterCount++;
                }
            }

            if (functionStartIndex == -1)
            {
                return null;
            }

            // Extract function name before the opening parenthesis
            var textBeforeFunction = textBeforeCursor.Substring(0, functionStartIndex);
            var functionNameMatch = Regex.Match(textBeforeFunction, @"(\w+)\s*$");

            if (!functionNameMatch.Success)
            {
                return null;
            }

            var functionName = functionNameMatch.Groups[1].Value;

            // Count parameters in the current function call
            var textInsideParens = textBeforeCursor.Substring(functionStartIndex + 1);
            var commaCount = 0;
            parenStack = 0;

            foreach (char c in textInsideParens)
            {
                if (c == '(')
                {
                    parenStack++;
                }
                else if (c == ')')
                {
                    parenStack--;
                }
                else if (c == ',' && parenStack == 0)
                {
                    commaCount++;
                }
            }

            return new FunctionCallContext
            {
                FunctionName = functionName,
                ParameterIndex = commaCount
            };
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
                        return;
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        var content = reader.ReadToEnd();
                        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        var functionRegex = new Regex(@"^(?:inline\s+)?(?:uniform\s+|varying\s+)?(?:unmasked\s+)?(\w+(?:\s*\*)?)\s+(\w+)\s*\((.*?)\)$");

                        foreach (var line in lines)
                        {
                            var match = functionRegex.Match(line.Trim());
                            if (match.Success)
                            {
                                var returnType = match.Groups[1].Value.Trim();
                                var functionName = match.Groups[2].Value.Trim();
                                var parametersStr = match.Groups[3].Value.Trim();

                                var parameters = new List<ParameterInfo>();
                                if (!string.IsNullOrEmpty(parametersStr))
                                {
                                    var paramParts = SplitParameters(parametersStr);
                                    foreach (var paramStr in paramParts)
                                    {
                                        var param = ParseParameter(paramStr.Trim());
                                        if (param != null)
                                        {
                                            parameters.Add(param);
                                        }
                                    }
                                }

                                var overload = new FunctionOverload
                                {
                                    FunctionName = functionName,
                                    ReturnType = returnType,
                                    FullSignature = line.Trim(),
                                    Parameters = parameters
                                };

                                if (!_stdlibFunctions.ContainsKey(functionName))
                                {
                                    _stdlibFunctions[functionName] = new List<FunctionOverload>();
                                }

                                _stdlibFunctions[functionName].Add(overload);
                            }
                        }

                        _logger.LogInformation($"Loaded {_stdlibFunctions.Count} stdlib function groups for signature help");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load stdlib functions for signature help");
            }
        }

        private List<string> SplitParameters(string parametersStr)
        {
            var parameters = new List<string>();
            var current = "";
            var parenLevel = 0;

            foreach (char c in parametersStr)
            {
                if (c == '(')
                {
                    parenLevel++;
                    current += c;
                }
                else if (c == ')')
                {
                    parenLevel--;
                    current += c;
                }
                else if (c == ',' && parenLevel == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        parameters.Add(current.Trim());
                    }
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                parameters.Add(current.Trim());
            }

            return parameters;
        }

        private ParameterInfo? ParseParameter(string paramStr)
        {
            if (string.IsNullOrWhiteSpace(paramStr))
                return null;

            // Simple parameter parsing - extract type and name
            var parts = paramStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            string paramName = "";
            string paramType = paramStr;

            // Try to extract parameter name (last identifier)
            var nameMatch = Regex.Match(paramStr, @"\b(\w+)\s*$");
            if (nameMatch.Success)
            {
                paramName = nameMatch.Groups[1].Value;
                paramType = paramStr.Substring(0, nameMatch.Index).Trim();
            }

            return new ParameterInfo
            {
                Name = paramName,
                Type = paramType,
                FullParameter = paramStr
            };
        }

        private class FunctionCallContext
        {
            public string FunctionName { get; set; } = string.Empty;
            public int ParameterIndex { get; set; }
        }
    }
}