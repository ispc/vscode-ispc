using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ispc_languageserver
{
    internal class DefinitionHandler : IDefinitionHandler
    {
        private readonly ITextDocumentManager _documents;

        public DefinitionHandler(ITextDocumentManager documents)
        {
            _documents = documents;
        }

        public DefinitionRegistrationOptions GetRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions();
        }

        public async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            var dummyLocation = new Location
            {
                Uri = request.TextDocument.Uri,
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new Position(6, 0),
                    End = new Position(6, 10)
                }
            };

            var location = _documents.GetLocation(request);

            await Console.Error.WriteLineAsync("[ispc] - Recieved definition request.");

            return new LocationOrLocationLinks(dummyLocation);
        }
    }
}
