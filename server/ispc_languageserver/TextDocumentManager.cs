using System;
using System.Text;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Data;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Timer = System.Timers.Timer;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace ispc_languageserver
{
    public interface ITextDocumentManager
    {
        public abstract void Add(TextDocumentItem document);
        public abstract void Change(DocumentUri uri, int? version, string text);
        public abstract void Remove(DocumentUri uri);
        public abstract Location GetLocation(DefinitionParams request);
        public ConcurrentQueue<TextDocumentItem> _queue { get; set; }
    }

    public class DocumentInfo
    {
        public DocumentUri Uri { get; set; }
        public IntPtr Ast { get; set; }
        public TextDocumentItem Document { get; set; }
        public List<Definition> Definitions { get; set; }

        public DocumentInfo(TextDocumentItem document)
        {
            Uri = document.Uri;
            Document = document;
            Definitions = new List<Definition>();
        }

    }

    public class TextDocumentManager : ITextDocumentManager
    {
        private readonly IAbstractSyntaxTreeManager _syntaxTrees;
        private readonly List<TextDocumentItem> _all = new List<TextDocumentItem>();
        public IReadOnlyList<TextDocumentItem> All => _all;
        private readonly Dictionary<DocumentUri,string[]> _allLines = new Dictionary<DocumentUri, string[]>();
        public IReadOnlyDictionary<DocumentUri, string[]> AllLines => _allLines;
        public ConcurrentQueue<TextDocumentItem>? _queue { get; set; }
        public event EventHandler<TextDocumentChangedEventArgs>? Changed;
        public Dictionary<DocumentUri, DocumentInfo> Documents = new Dictionary<DocumentUri, DocumentInfo>();

        public TextDocumentManager(IAbstractSyntaxTreeManager syntaxTrees)
        {
            _queue = new ConcurrentQueue<TextDocumentItem>();
            _syntaxTrees = syntaxTrees;
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

            Documents.Add(document.Uri, new DocumentInfo(document));

            OnChanged(document);
            Console.Error.WriteLine($"[ispc] - Opened Document at: {document.Uri}");
            Validate(Documents[document.Uri]);
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
            _all[index] = newDocument;
            Documents[uri].Document = newDocument;
            Console.Error.WriteLine("[ispc] - Document Changed");
            Validate(Documents[uri]);
        }

        private void Validate(DocumentInfo documentInfo, bool forceEnqueue = false)
        {
            if (documentInfo.Document == null || _queue == null)
                return;
            // Check if document is already queued for validation by the compiler
            if (forceEnqueue == false && Enumerable.Contains(_queue, documentInfo.Document, new DocumentComparer()))
                return;

            _syntaxTrees.ParseDocument(documentInfo);

            Console.Error.WriteLine("[ispc] - Queuing Document for Validation");
            _queue.Enqueue(documentInfo.Document);
        }

        public Location GetLocation(DefinitionParams request)
        {
            Location location = new Location();
            return location;
        }

        public void Remove(DocumentUri uri)
        {
            var index = _all.FindIndex(x => x.Uri == uri);
            if (index < 0)
            {
                return;
            }
            _all.RemoveAt(index);
        }

        protected virtual void OnChanged(TextDocumentItem document)
        {
            Changed?.Invoke(this, new TextDocumentChangedEventArgs(document));
        }

        private class DocumentComparer : IEqualityComparer<TextDocumentItem>
        {
            public bool Equals(TextDocumentItem x, TextDocumentItem y)
            {
                if (Object.ReferenceEquals(x, y)) return true;

                if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                    return false;

                return x.Uri == y.Uri;
            }

            // If Equals() returns true for a pair of objects 
            // then GetHashCode() must return the same value for these objects.
            public int GetHashCode(TextDocumentItem document)
            {
                if (Object.ReferenceEquals(document, null)) return 0;

                return document.Uri.GetHashCode();
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
