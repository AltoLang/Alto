using System;
using System.Collections.Generic;
using System.Linq;
using Alto.CodeAnalysis.Syntax;
using System.Collections.Immutable;
using Alto.CodeAnalysis.Text;
using System.IO;

namespace Alto.CodeAnalysis.Syntax
{
    public sealed class SyntaxTree
    {
        private delegate void ParseHandler(SyntaxTree tree, out CompilationUnitSyntax root, out ImmutableArray<Diagnostic> diagnostics);
        public List<SyntaxTree> _importedTrees = new List<SyntaxTree>();

        private SyntaxTree(SourceText text, ParseHandler handler)
        {
            Text = text;

            handler(this, out var root, out var diagnostics);

            Diagnostics = diagnostics;
            Root = root;
        }

        public SourceText Text { get; }
        public ImmutableArray<Diagnostic> Diagnostics { get; }
        public CompilationUnitSyntax Root { get; }

        public static SyntaxTree Load(string fileName)
        {
            var text = File.ReadAllText(fileName);
            var source = SourceText.From(text, fileName);

            return Parse(source);
        }

        private static void Parse(SyntaxTree tree, out CompilationUnitSyntax root, out ImmutableArray<Diagnostic> diagnostics)
        {
            var parser = new Parser(tree);
            root = parser.ParseCompilationUnit();
            diagnostics = parser.Diagnostics.ToImmutableArray();
        }

        public static SyntaxTree Parse(string text)
        {
            var sourceText = SourceText.From(text);
            return Parse(sourceText);
        }

        public static SyntaxTree Parse(SourceText text)
        {
            return new SyntaxTree(text, Parse);
        }

        public static ImmutableArray<SyntaxToken> ParseTokens(string text)
        {
            var sourceText =  SourceText.From(text);
            return ParseTokens(sourceText);
        }
    
        public static ImmutableArray<SyntaxToken> ParseTokens(string text, out ImmutableArray<Diagnostic> diagnostics)
        {
            var sourceText =  SourceText.From(text);
            return ParseTokens(sourceText, out diagnostics);
        }


        public static ImmutableArray<SyntaxToken> ParseTokens(SourceText text)
        {
            return ParseTokens(text, out _);
        }

        public static ImmutableArray<SyntaxToken> ParseTokens(SourceText text, out ImmutableArray<Diagnostic> diagnostics)
        {
            List<SyntaxToken> tokens = new List<SyntaxToken>();

            void ParseTokens(SyntaxTree tree, out CompilationUnitSyntax root, out ImmutableArray<Diagnostic> d)
            {
                root = null;

                var lexer = new Lexer(tree);
                while (true)
                {
                    var token = lexer.Lex();
                    if (token.Kind == SyntaxKind.EndOfFileToken)
                    {
                        root = new CompilationUnitSyntax(tree, ImmutableArray<MemberSyntax>.Empty, token);
                        break;
                    }
                    
                    tokens.Add(token);
                }
                
                d = lexer.Diagnostics.ToImmutableArray();
            }

            var tree = new SyntaxTree(text, ParseTokens);
            diagnostics = tree.Diagnostics.ToImmutableArray();
            return tokens.ToImmutableArray();
        }
    }
}