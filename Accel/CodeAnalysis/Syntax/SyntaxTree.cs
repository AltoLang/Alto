using System;
using System.Collections.Generic;
using System.Linq;
using Accel.CodeAnalysis.Syntax;
using System.Collections.Immutable;
using Accel.CodeAnalysis.Text;

namespace Accel.CodeAnalysis.Syntax
{
    public sealed class SyntaxTree
    {
        public SyntaxTree(SourceText text, ImmutableArray<Diagnostic> diagnostics, ExpressionSyntax root, SyntaxToken endOfFile)
        {
            Text = text;
            Diagnostics = diagnostics;
            Root = root;
            EndOfFile = endOfFile;
        }

        public SourceText Text { get; }
        public ImmutableArray<Diagnostic> Diagnostics { get; }
        public ExpressionSyntax Root { get; }
        public SyntaxToken EndOfFile { get; }

        public static SyntaxTree Parse(string text)
        {
            var sourceText =  SourceText.From(text);
            return Parse(sourceText);
        }

        public static SyntaxTree Parse(SourceText text)
        {
            var parser = new Parser(text);
            return parser.Parse();
        }

        public static IEnumerable<SyntaxToken> ParseTokens(string text)
        {
            var sourceText =  SourceText.From(text);
            return ParseTokens(sourceText);
        }

        public static IEnumerable<SyntaxToken> ParseTokens(SourceText text)
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