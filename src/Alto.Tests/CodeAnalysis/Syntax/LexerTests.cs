using System;
using System.Collections.Generic;
using Xunit;
using System.Linq;
using Alto.CodeAnalysis.Syntax;
using Alto.CodeAnalysis.Text;

namespace Alto.Tests.CodeAnalysis.Syntax
{

    public class LexerTests
    {
        [Fact]
        public void Lexer_Lexes_UnterminatedString()
        {
            var text = "\" unterminated string test";
            var tokens = SyntaxTree.ParseTokens(text, out var diagnostics);

            var token = Assert.Single(tokens);
            Assert.Equal(SyntaxKind.StringToken, token.Kind);
            Assert.Equal(text, token.Text);

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(new TextSpan(0, 1), diagnostic.Location.Span);
            Assert.Equal("Unterminated string literal.", diagnostic.Message);
        }

        [Theory]
        [MemberData(nameof(GetTokensData))]
        public void Lexer_Lex_Token(SyntaxKind kind, string text)
        {
            var tokens = SyntaxTree.ParseTokens(text);

            var token = Assert.Single(tokens);
            Assert.Equal(kind, token.Kind);
            Assert.Equal(text, token.Text);
        }

        [Fact]
        public void Lexer_Test_AllTokens()
        {
            var tokenKinds = Enum.GetValues(typeof(SyntaxKind)).Cast<SyntaxKind>().Where(k => k.ToString().EndsWith("Keyword") || k.ToString().EndsWith("Token"));
            var testedTokenKinds = GetTokens().Concat(GetSeparators()).Select(t => t.kind);

            var untestedTokens = new SortedSet<SyntaxKind>(tokenKinds);
            untestedTokens.Remove(SyntaxKind.EndOfFileToken);
            untestedTokens.Remove(SyntaxKind.BadToken);
            untestedTokens.ExceptWith(testedTokenKinds);

            Assert.Empty(untestedTokens);
        }

        [Theory]
        [MemberData(nameof(GetTokensPairsData))]
        public void Lexer_Lex_TokenPairs(SyntaxKind t1Kind, string t1Text, SyntaxKind t2Kind, string t2Text)
        {
            var text = t1Text + t2Text;
            var tokens = SyntaxTree.ParseTokens(text).ToArray();

            Assert.Equal(2, tokens.Length);
            Assert.Equal(t1Kind, tokens[0].Kind);
            Assert.Equal(t1Text, tokens[0].Text);

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
            Assert.Equal(t1Kind, tokens[0].Kind);
            Assert.Equal(t1Text, tokens[0].Text);

            Assert.Equal(separatorKind, tokens[1].Kind);
            Assert.Equal(separatorText, tokens[1].Text);
            
            Assert.Equal(t2Text, tokens[2].Text);
        }

        [Fact]
        public void Lexer_Lexes_Directive()
        {
            var text = "#directive true while else TEST";
            var allTokens = SyntaxTree.ParseTokens(text, out var diagnostics);
            var tokens = allTokens.Where(t => t.Kind == SyntaxKind.HashtagToken).Concat(allTokens.Where(t => t.Kind == SyntaxKind.IdentifierToken)).ToArray();

            Assert.Equal(6, tokens.Length);
            Assert.Equal(SyntaxKind.HashtagToken, tokens[0].Kind);
            
            for (int i = 1; i < tokens.Length; i++)
            {
                var token = tokens[i];
                Assert.Equal(SyntaxKind.IdentifierToken, token.Kind);
            }
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
            var fixedTokens = Enum.GetValues(typeof(SyntaxKind)).Cast<SyntaxKind>().Select(k => (k, text: SyntaxFacts.GetText(k))).Where(t => t.text != null);

            var dynamicTokens = new[] {
                (SyntaxKind.IdentifierToken, "a"),
                (SyntaxKind.IdentifierToken, "abc"),
                (SyntaxKind.NumberToken, "4516541"),
                (SyntaxKind.NumberToken, "4"),
                (SyntaxKind.NumberToken, "782"),

                (SyntaxKind.StringToken, "\"idk\""),
                (SyntaxKind.StringToken, "\"idk asdasdwdasdw test\""),
                (SyntaxKind.StringToken, "\" test " + @"\" + "\"" + " test \""),
            };

            return fixedTokens.Concat(dynamicTokens);
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

            if (t1Kind == SyntaxKind.LesserToken && t2Kind == SyntaxKind.EqualsToken)
                return true;

            if (t1Kind == SyntaxKind.LesserToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                return true;

            if (t1Kind == SyntaxKind.GreaterToken && t2Kind == SyntaxKind.EqualsToken)
                return true;

            if (t1Kind == SyntaxKind.GreaterToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                return true;

            if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandToken)
                return true;

            if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandAmpersandToken)
                return true;

            if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipeToken)
                return true;

            if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipePipeToken)
                return true;

            if (t1Kind == SyntaxKind.TildeToken && t2Kind == SyntaxKind.EqualsEqualsToken)
                return true;

            if (t1Kind == SyntaxKind.EqualsEqualsToken && t2Kind == SyntaxKind.TildeToken)
                return true;

            if (t1Kind == SyntaxKind.TildeToken && t2Kind == SyntaxKind.EqualsToken)
                return true;

            if (t1Kind == SyntaxKind.EqualsToken && t2Kind == SyntaxKind.TildeToken)
                return true;

            if (t1IsKeyword && t2Kind == SyntaxKind.NumberToken)
                return true;

            if (t1Kind == SyntaxKind.IdentifierToken && t2Kind == SyntaxKind.NumberToken)
                return true;
            
            return false;
        }

        private static IEnumerable<(SyntaxKind t1Kind, string t1Text, SyntaxKind t2Kind, string t2Text)> GetTokenPairs()
        {
            foreach (var t1 in GetTokens()) 
            {
                foreach (var t2 in GetTokens())
                {
                    if (t1.kind == SyntaxKind.HashtagToken || t2.kind == SyntaxKind.HashtagToken)
                        continue;
                    
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
                    if (t1.kind == SyntaxKind.HashtagToken || t2.kind == SyntaxKind.HashtagToken)
                        continue;
                    
                    if (RequiresSeparator(t1.kind, t2.kind))
                        foreach (var s in GetSeparators())
                            yield return (t1.kind, t1.text, s.kind, s.text, t2.kind, t2.text);
                }
            }
        }
    }
}
