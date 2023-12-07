using api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.ComponentModel.DataAnnotations;

namespace ispc_languageserver
{
    public interface IAbstractSyntaxTreeManager
    {
        public abstract void Initialize();
        public abstract void ParseDocument(DocumentInfo documentInfo);
    };

    public class Definition
    {
        public Range? Range;
        public string? Identifier { get; set; }
    }

    internal class AbstractSyntaxTreeManager : IAbstractSyntaxTreeManager
    {
        private IntPtr parser;
        private IntPtr language;
        private List<IntPtr> trees = new List<IntPtr>();
        public List<Definition> Definitions = new List<Definition>();

        public void Initialize() {
            [DllImport("ispc.tree-sitter", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            static extern IntPtr tree_sitter_ispc();
        
            parser = TSMethods.ts_parser_new();
            TSMethods.ts_parser_set_language(parser, tree_sitter_ispc());
            language = TSMethods.ts_parser_language(parser);
        }

        public void ParseDocument(DocumentInfo documentInfo)
        {
            BuildSyntaxTree(documentInfo);
            LoadDefinitions(documentInfo);
        }

        public void BuildSyntaxTree(DocumentInfo documentInfo)
        {
            // Convert document text to c-style string
            IntPtr cstr = Marshal.StringToCoTaskMemAnsi(documentInfo.Document.Text);
            uint length = (uint)documentInfo.Document.Text.Length;

            // Parse c string to Syntax Tree
            IntPtr tree = TSMethods.ts_parser_parse_string(
                parser,
                IntPtr.Zero,
                cstr,
                length
            );
            trees.Add(tree);

            documentInfo.Ast = tree;

        }

        private void LoadDefinitions(DocumentInfo documentInfo)
        {
            if(documentInfo.Definitions.Any())
            {
                documentInfo.Definitions.Clear();
            }

            TSNode root_node = TSMethods.ts_tree_root_node(documentInfo.Ast);

            String queryStr = "(declaration (init_declarator declarator: (identifier) @identifier))";
            IntPtr queryCstr = Marshal.StringToCoTaskMemAnsi(queryStr);
            IntPtr error_offset = IntPtr.Zero;
            TSQueryError error_type = new TSQueryError();
            IntPtr query = TSMethods.ts_query_new(
                language,
                queryCstr,
                (uint)queryStr.Length,
                error_offset,
                ref error_type 
                );
            IntPtr cursor = TSMethods.ts_query_cursor_new();
            TSMethods.ts_query_cursor_exec(cursor, query, root_node);

            TSQueryMatch match = new TSQueryMatch();
            unsafe
            {
                while(TSMethods.ts_query_cursor_next_match(cursor, ref match) == 1)
                {
                    Definition definition = new Definition();
                    definition.Range = GetNodeRange(match.captures->node);
                    definition.Identifier = GetNodeText(match.captures->node,documentInfo.Document.Text);

                    documentInfo.Definitions.Add(definition);
                }
            }

            Console.Error.WriteLine("[tree-sitter] Definitions Found in Document:");
            foreach(Definition definition in documentInfo.Definitions)
            {
                Console.Error.WriteLine($"  -Idenfitifer: {definition.Identifier} at Line {definition.Range.Start.Line + 1}");
            }

        }

        private static Range GetNodeRange(TSNode node)
        {
            TSPoint StartPoint = TSMethods.ts_node_start_point(node);
            TSPoint EndPoint = TSMethods.ts_node_end_point(node);

            var range = new Range(
                (int)StartPoint.row,
                (int)StartPoint.column,
                (int)EndPoint.row,
                (int)EndPoint.column
                );

            return range;
        }

        private static void PrintNode(TSNode node)
        {
            // Print tree for debug purposes
            IntPtr str = TSMethods.ts_node_string(node);
            string newStr = Marshal.PtrToStringAnsi((IntPtr)str);
            Console.Error.WriteLine("[Tree-Sitter] - Printing Node: " + newStr);
        }

        private static void PrintNodeText(TSNode node, string document)
        {
            uint StartOffset = TSMethods.ts_node_start_byte(node);
            uint EndOffset = TSMethods.ts_node_end_byte(node);

            uint textLength = EndOffset - StartOffset;
            string nodeText = document.Substring((int)StartOffset, (int)textLength);

            IntPtr NodeType = TSMethods.ts_node_type(node);
            string NodeTypeStr = Marshal.PtrToStringAnsi(NodeType);
            Console.Error.WriteLine("[tree-sitter] - Node Type: "+NodeTypeStr+" | Node Text: " + nodeText);
        }

        private static string GetNodeText(TSNode node, string document)
        {
            uint StartOffset = TSMethods.ts_node_start_byte(node);
            uint EndOffset = TSMethods.ts_node_end_byte(node);

            uint textLength = EndOffset - StartOffset;
            string nodeText = document.Substring((int)StartOffset, (int)textLength);

            return nodeText;
        }
    }
}
