using System;
using System.Collections.Generic;
using System.Linq;

namespace Alto.CodeAnalysis.Syntax
{
    public static class SyntaxFacts
    {
        public static int GetBinaryOperatorPrecedence(this SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.StarToken:
                case SyntaxKind.SlashToken:
                case SyntaxKind.PercentageToken:
                    return 5;

                case SyntaxKind.PlusToken:
                case SyntaxKind.MinusToken:
                    return 4;

                case SyntaxKind.EqualsEqualsToken:
                case SyntaxKind.BangEqualsToken:
                case SyntaxKind.GreaterOrEqualsToken:
                case SyntaxKind.GreaterToken:
                case SyntaxKind.LesserOrEqualsToken:
                case SyntaxKind.LesserToken:
                    return 3;

                case SyntaxKind.AmpersandAmpersandToken:
                    return 2;
                
                case SyntaxKind.PipePipeToken:
                    return 1;
                
                default:
                    return 0;
            }
        }
        public static int GetUnaryOperatorPrecedence(this SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.PlusToken:
                case SyntaxKind.BangToken:
                case SyntaxKind.MinusToken:
                    return 6;
                
                default:
                    return 0;
            }
        }

        public static SyntaxKind GetKeywordKind(string text)
        {
            switch (text)
            {
                case "true":
                    return SyntaxKind.TrueKeyword;
                case "false":
                    return SyntaxKind.FalseKeyword;
                case "var":
                    return SyntaxKind.VarKeyword;
                case "let":
                    return SyntaxKind.LetKeyword;
                case "if":
                    return SyntaxKind.IfKeyword;
                case "else":
                    return SyntaxKind.ElseKeyword;
                default:
                    return SyntaxKind.IdentifierToken;
            }
        }

        public static string GetText(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.PlusToken: 
                    return "+";
                case SyntaxKind.MinusToken: 
                    return "-";
                case SyntaxKind.StarToken: 
                    return "*";
                case SyntaxKind.SlashToken: 
                    return "/";
                case SyntaxKind.PercentageToken: 
                    return "%";
                case SyntaxKind.OpenParenthesesToken: 
                    return "(";
                case SyntaxKind.CloseParenthesesToken: 
                    return ")";
                case SyntaxKind.OpenBraceToken: 
                    return "{";
                case SyntaxKind.CloseBraceToken: 
                    return "}";
                case SyntaxKind.BangToken: 
                    return "!";
                case SyntaxKind.AmpersandAmpersandToken: 
                    return "&&";
                case SyntaxKind.PipePipeToken: 
                    return "||";
                case SyntaxKind.EqualsEqualsToken: 
                    return "==";
                case SyntaxKind.BangEqualsToken: 
                    return "~=";
                case SyntaxKind.LesserToken:
                    return "<";
                case SyntaxKind.LesserOrEqualsToken:
                    return "<=";
                case SyntaxKind.GreaterOrEqualsToken:
                    return ">=";
                case SyntaxKind.GreaterToken:
                    return ">";
                case SyntaxKind.EqualsToken: 
                    return "=";
                case SyntaxKind.FalseKeyword:
                    return "false";
                case SyntaxKind.TrueKeyword:
                    return "true";
                case SyntaxKind.VarKeyword:
                    return "var";
                case SyntaxKind.LetKeyword:
                    return "let";
                case SyntaxKind.IfKeyword:
                    return "if";
                case SyntaxKind.ElseKeyword:
                    return "else";
                default:
                    return null;
            }
        }

        public static IEnumerable<SyntaxKind> GetUnaryOperatorKinds()
        {
            var kinds = (SyntaxKind[]) Enum.GetValues(typeof(SyntaxKind));
            foreach (var kind in kinds)
            {
                if (GetUnaryOperatorPrecedence(kind) > 0)
                    yield return kind;
            }
        }

        public static IEnumerable<SyntaxKind> GetBinaryOperatorKinds()
        {
            var kinds = (SyntaxKind[]) Enum.GetValues(typeof(SyntaxKind));
            foreach (var kind in kinds)
            {
                if (GetBinaryOperatorPrecedence(kind) > 0)
                    yield return kind;
            }
        }
    }   
}