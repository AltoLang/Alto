using System;
using System.Collections.Generic;
using System.Linq;
using compiler.CodeAnalysis.Syntax;

namespace compiler.CodeAnalysis.Syntax
{
    public sealed class SyntaxTree
    {
        public SyntaxTree(IEnumerable<Diagnostic> diagnostics, ExpressionSyntax root, SyntaxToken endOfFile)
        {
            Diagnostics = diagnostics.ToArray();
            Root = root;
            EndOfFile = endOfFile;
        }

        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public ExpressionSyntax Root { get; }
        public SyntaxToken EndOfFile { get; }

        public static SyntaxTree Parse(string text)
        {
            var parser = new Parser(text);
            return parser.Parse();
        }

        public static IEnumerable<SyntaxToken> ParseTokens(string text)
        {
            var lexer = new Lexer(text);
            while (true)
            {
                var token = lexer.Lex();
                if (token.Kind == SyntaxKind.EndOfFileToken)
                    break;
                
                yield return token;
            }
        }
    }
}