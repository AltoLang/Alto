using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using System.Linq;

using compiler.CodeAnalysis.Syntax;

namespace Accel.Tests.CodeAnalysis.Syntax
{

    public class LexerTest
    {
        [Theory]
        [MemberData(nameof(GetTokensData))]
        public void Lexer_Lex_Token(SyntaxKind kind, string text)
        {
            var tokens = SyntaxTree.ParseTokens(text);

            var token = Assert.Single(tokens);
            Assert.Equal(kind, token.Kind);
            Assert.Equal(text, token.Text);
        }

        [Theory]
        [MemberData(nameof(GetTokensPairsData))]
        public void Lexer_Lex_TokenPairs(SyntaxKind t1Kind, string t1Text, SyntaxKind t2Kind, string t2Text)
        {
            var text = t1Text + t2Text;
            var tokens = SyntaxTree.ParseTokens(text).ToArray();

            Assert.Equal(2, tokens.Length);
            Assert.Equal(tokens[0].Kind, t1Kind);
            Assert.Equal(tokens[0].Text, t1Text);

            Assert.Equal(tokens[1].Kind, t2Kind);
            Assert.Equal(tokens[1].Text, t2Text);
        }

        [Theory]
        [MemberData(nameof(GetTokensPairsWithSeparatorData))]
        public void Lexer_Lex_TokenPairs_WithSeparators(SyntaxKind t1Kind, string t1Text, SyntaxKind separatorKind, string separatorText, SyntaxKind t2Kind, string t2Text)
        {
            var text = t1Text + separatorText + t2Text;
            var tokens = SyntaxTree.ParseTokens(text).ToArray();

            Assert.Equal(3, tokens.Length);
            Assert.Equal(tokens[0].Kind, t1Kind);
            Assert.Equal(tokens[0].Text, t1Text);

            Assert.Equal(tokens[1].Kind, separatorKind);
            Assert.Equal(tokens[1].Text, separatorText);

            Assert.Equal(tokens[2].Kind, t2Kind);
            Assert.Equal(tokens[2].Text, t2Text);
        }

        public static IEnumerable<object[]> GetTokensData()
        {
            foreach ( var t in GetTokens().Concat(GetSeparators()))
                yield return new object[] { t.kind, t.text };
        }
        public static IEnumerable<object[]> GetTokensPairsData()
        {
            foreach ( var t in GetTokenPairs())
                yield return new object[] { t.t1Kind, t.t1Text, t.t2Kind, t.t2Text };
        }

        public static IEnumerable<object[]> GetTokensPairsWithSeparatorData()
        {
            foreach ( var t in GetTokenPairsWithSeparator())
                yield return new object[] { t.t1Kind, t.t1Text, t.separatorKind, t.separatorText, t.t2Kind, t.t2Text };
        }

        private static IEnumerable<(SyntaxKind kind, string text)> GetTokens()
        {
            return new[] {
                (SyntaxKind.IdentifierToken, "a"),
                (SyntaxKind.IdentifierToken, "abc"),
                /*
                (SyntaxKind.WhitespaceToken, " "),
                (SyntaxKind.WhitespaceToken, "         "),
                (SyntaxKind.WhitespaceToken, "  "),
                (SyntaxKind.WhitespaceToken, "\r"),
                (SyntaxKind.WhitespaceToken, "\n"),
                (SyntaxKind.WhitespaceToken, "\r\n"), 
                */
                (SyntaxKind.NumberToken, "4516541"),
                (SyntaxKind.NumberToken, "4"),
                (SyntaxKind.NumberToken, "782"),

                (SyntaxKind.PlusToken, "+"),
                (SyntaxKind.MinusToken, "-"),
                (SyntaxKind.StarToken, "*"),
                (SyntaxKind.SlashToken, "/"),
                (SyntaxKind.PercentageToken, "%"),
                (SyntaxKind.OpenParenthesesToken, "("),
                (SyntaxKind.CloseParenthesesToken, ")"),  
                (SyntaxKind.BangToken, "!"),
                (SyntaxKind.AmpersandAmpersandToken, "&&"),
                (SyntaxKind.PipePipeToken, "||"),
                (SyntaxKind.EqualsEqualsToken, "=="),
                (SyntaxKind.BangEqualsToken, "~="),
                (SyntaxKind.EqualsToken, "="),
                (SyntaxKind.FalseKeyword, "false"),
                (SyntaxKind.TrueKeyword, "true")
            };
        }

        private static IEnumerable<(SyntaxKind kind, string text)> GetSeparators()
        {
            return new[] {
                (SyntaxKind.WhitespaceToken, " "),
                (SyntaxKind.WhitespaceToken, "         "),
                (SyntaxKind.WhitespaceToken, "  "),
                (SyntaxKind.WhitespaceToken, "\r"),
                (SyntaxKind.WhitespaceToken, "\n"),
                (SyntaxKind.WhitespaceToken, "\r\n"), 
            };
        }

        private static bool RequiresSeparator(SyntaxKind t1Kind, SyntaxKind t2Kind)
        {
            var t1IsKeyword = t1Kind.ToString().EndsWith("Keyword");
            var t2IsKeyword = t2Kind.ToString().EndsWith("Keyword");

            if (t1Kind == SyntaxKind.IdentifierToken && t2Kind == SyntaxKind.IdentifierToken)
                return true;

             if (t1IsKeyword && t2IsKeyword)
                return true;

            if (t1IsKeyword && t2Kind == SyntaxKind.IdentifierToken)
                return true;

            if (t1Kind == SyntaxKind.IdentifierToken && t2IsKeyword)
                return true;

            if (t1Kind == SyntaxKind.NumberToken && t2Kind == SyntaxKind.NumberToken)
                return true;

            if (t1Kind == SyntaxKind.BangToken && t2Kind == SyntaxKind.EqualsToken)
                return true;

            if (t1Kind == SyntaxKind.BangToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                return true;

            if (t1Kind == SyntaxKind.EqualsToken && t2Kind == SyntaxKind.EqualsToken)
                return true;

            if (t1Kind == SyntaxKind.EqualsToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                return true;
            
            return false;
        }

        private static IEnumerable<(SyntaxKind t1Kind, string t1Text, SyntaxKind t2Kind, string t2Text)> GetTokenPairs()
        {
            foreach (var t1 in GetTokens()) 
            {
                foreach (var t2 in GetTokens())
                {
                    if (!RequiresSeparator(t1.kind, t2.kind))
                        yield return (t1.kind, t1.text, t2.kind, t2.text);
                }
            }
        }

        private static IEnumerable<(SyntaxKind t1Kind, string t1Text, SyntaxKind separatorKind, string separatorText, SyntaxKind t2Kind, string t2Text)> GetTokenPairsWithSeparator()
        {
            foreach (var t1 in GetTokens()) 
            {
                foreach (var t2 in GetTokens())
                {
                    if (RequiresSeparator(t1.kind, t2.kind))
                        foreach (var s in GetSeparators())
                            yield return (t1.kind, t1.text, s.kind, s.text, t2.kind, t2.text);
                }
            }
        }
    }
}
