using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace ISPCLanguageExtension
{
    public class ISPCContentDefinition
    {
#pragma warning disable CS0649
        [Export]
        [Name("ispc")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition ISPCContentTypeDefinition;


        [Export]
        [FileExtension(".isph")]
        [ContentType("ispc")]
        internal static FileExtensionToContentTypeDefinition ISPCHeaderFileExtensionDefinition;

        [Export]
        [FileExtension(".ispc")]
        [ContentType("ispc")]
        internal static FileExtensionToContentTypeDefinition ISPCSourceFileExtensionDefinition;
#pragma warning restore CS0649
    }
}
