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

namespace ispc_languageserver
{
    public interface IAbstractSyntaxTreeManager
    {
        public abstract void Initialize();
        public abstract void ParseDocument(TextDocumentItem document);
    };

    public class Definition
    {
        public Range Range;
        public DocumentUri DocumentUri { get; set; }
        public string Name { get; set; }
        public string Text;
    }

    internal class AbstractSyntaxTreeManager : IAbstractSyntaxTreeManager
    {
        private IntPtr parser;
        private List<IntPtr> trees = new List<IntPtr>();
        public List<Definition> Definitions = new List<Definition>();

        public void Initialize() {
            [DllImport("ispc.tree-sitter.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            static extern IntPtr tree_sitter_ispc();
        
            parser = TSMethods.ts_parser_new();
            TSMethods.ts_parser_set_language(parser, tree_sitter_ispc());
        }

        public void ParseDocument(TextDocumentItem document)
        {
            BuildSyntaxTree(document.Text);
        }

        public void BuildSyntaxTree(string source)
        {
            // Convert document text to c-style string
            IntPtr cstr = Marshal.StringToCoTaskMemAnsi(source);
            uint length = (uint)source.Length;

            // Parse c string to Syntax Tree
            IntPtr tree = TSMethods.ts_parser_parse_string(
                parser,
                IntPtr.Zero,
                cstr,
                length
            );
            trees.Add(tree);

            TSNode root_node = TSMethods.ts_tree_root_node(tree);
            LoadDefinitions(root_node, source);

            //foreach(Definition definition in Definitions)
            //{
            //    Console.Error.WriteLine("[tree-sitter] - Definition: "+definition.Text);
            //}

        }

        private void LoadDefinitions(TSNode node, string source)
        {
            uint ChildCount = TSMethods.ts_node_child_count(node);
            if(TSMethods.ts_node_is_named(node))
            {
                IntPtr NodeType = TSMethods.ts_node_type(node);
                string NodeTypeStr = Marshal.PtrToStringAnsi(NodeType);
                if(NodeTypeStr == "declaration")
                {
                    for(uint i = 0; i < ChildCount; ++i)
                    {
                        TSNode childNode = TSMethods.ts_node_child(node, i);
                        IntPtr childNodeType = TSMethods.ts_node_type(childNode);
                        string childNodeTypeStr = Marshal.PtrToStringAnsi(childNodeType);
                        PrintNodeText(childNode, source);
                        if(childNodeTypeStr == "identifier")
                        {
                            i = ChildCount;
                        }
                    }
                    var newDefinition = new Definition
                    {
                        Range = GetNodeRange(node),
                        Text = Marshal.PtrToStringAnsi(TSMethods.ts_node_string(node))
                    };
                    Definitions.Add(newDefinition);
                }
            }

            for(uint i = 0; i < ChildCount; ++i)
            {
                TSNode child = TSMethods.ts_node_child(node, i);
                LoadDefinitions(child, source);
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
            Console.Error.WriteLine("[tree-sitter] - Node Type: "+NodeTypeStr+" | Node Text: \n" + nodeText + "\n\n");
        }
    }
}
