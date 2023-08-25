using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;


#pragma warning disable CS0618

namespace ispc_languageserver
{
    internal class TextDocumentHandler : TextDocumentSyncHandlerBase
    {
        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ITextDocumentManager _documents;
        private readonly Compiler _compiler;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter {
                Pattern = "**/*.ispc"
            }
        );

        public TextDocumentHandler(
            ILogger<TextDocumentHandler> logger,
            ILanguageServerConfiguration configuration,
            ITextDocumentManager documents,
            Compiler compiler
            )
        {
            _logger = logger;
            _configuration = configuration;
            _documents = documents;
            _compiler = compiler;
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            DocumentUri uri = notification.TextDocument.Uri;
            int? version = notification.TextDocument.Version;
            string text = notification.ContentChanges.Single().Text;
            _documents.Change(uri, version, text);

            return Unit.Task;
        }

        public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();
            await _configuration.GetScopedConfiguration(notification.TextDocument.Uri, token).ConfigureAwait(false);
            _documents.Add(notification.TextDocument);

            return Unit.Value;
        }

        public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            if (_configuration.TryGetScopedConfiguration(notification.TextDocument.Uri, out var disposable))
            {
                disposable.Dispose();
            }

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) => new TextDocumentSyncRegistrationOptions() {
            DocumentSelector = _documentSelector,
            Change = Change,
            Save = new SaveOptions() { IncludeText = true }
        };

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, "ispc");

    }
}