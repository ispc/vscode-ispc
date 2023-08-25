using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Data;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace ispc_languageserver
{
    public interface ITextDocumentManager
    {
        public abstract void Add(TextDocumentItem document);
        public abstract void Change(DocumentUri uri, int? version, string text);
        public abstract void Remove(DocumentUri uri);
    }

    public class TextDocumentManager : ITextDocumentManager
    {
        private readonly List<TextDocumentItem> _all = new List<TextDocumentItem>();
        public IReadOnlyList<TextDocumentItem> All => _all;
        private readonly Dictionary<DocumentUri,string[]> _allLines = new Dictionary<DocumentUri, string[]>();
        public IReadOnlyDictionary<DocumentUri, string[]> AllLines => _allLines;
        public event EventHandler<TextDocumentChangedEventArgs>? Changed;
        private readonly ILanguageServerConfiguration _configuration;

        public TextDocumentManager(ILanguageServerConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Add(TextDocumentItem document)
        {
            if (_all.Any(x => x.Uri == document.Uri))
            {
                return;
            }

            // add the document
            _all.Add(document);

            // to make things faster let's also store the lines so that work like
            // signature help is a little easier.
            _allLines[document.Uri] = document.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            OnChanged(document);
        }

        public void Change(DocumentUri uri, int? version, string text)
        {
            // do we already have this document stored?
            var index = _all.FindIndex(x => x.Uri == uri);
            if (index < 0)
            {
                return;
            }

            // is new version?
            var document = _all[index];
            if (document.Version >= version)
            {
                return;
            }

            // update the individual lines
            _allLines[document.Uri] = document.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // TextDocumentItem is read-only, so we need to instantiate a new one
            TextDocumentItem newDocument = new TextDocumentItem
            {
                Uri = document.Uri,
                Version = version,
                Text = text,
                LanguageId = "ispc"
            };

            // Replace TextDocumentItem
            _all.RemoveAt(index);
            Close(uri);
            _all[index] = newDocument;
        }

        public void Remove(DocumentUri uri)
        {
            var index = _all.FindIndex(x => x.Uri == uri);
            if (index < 0)
            {
                return;
            }
            _all.RemoveAt(index);
            Close(uri);
        }


        protected virtual void OnChanged(TextDocumentItem document)
        {
            Changed?.Invoke(this, new TextDocumentChangedEventArgs(document));
        }

        public void Close(DocumentUri uri)
        {
            if (_configuration.TryGetScopedConfiguration(uri, out var disposable))
            {
                disposable.Dispose();
            }
        }
    }

    public class TextDocumentChangedEventArgs : EventArgs
    {
        private readonly TextDocumentItem _document;

        public TextDocumentChangedEventArgs(TextDocumentItem document)
        {
            _document = document;
        }

        public TextDocumentItem Document => _document;
    }
}
