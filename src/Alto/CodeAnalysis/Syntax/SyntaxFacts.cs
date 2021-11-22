using System;
using System.Collections.Generic;
using System.Linq;

namespace Alto.CodeAnalysis.Syntax
{
    /// <summary>
    /// Contains definitions for methods used for getting operator precedences.
    /// And converting <see cref="SyntaxToken"/> to text and vise versa.
    /// This class cannot be inherited.
    /// </summary>
    public static class SyntaxFacts
    {
        /// <summary>
        /// Gets the precedence of a given operator in a valid binary expression.
        /// </summary>
        /// <returns>The precedence of the operator.</returns>
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
                case SyntaxKind.AmpersandToken:
                    return 2;
                
                case SyntaxKind.PipePipeToken:
                case SyntaxKind.PipeToken:
                case SyntaxKind.HatToken:
                    return 1;
                
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Gets the precedence of a given operator in a valid unary expression.
        /// </summary>
        /// <returns>The precedence of the operator.</returns>
        public static int GetUnaryOperatorPrecedence(this SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.PlusToken:
                case SyntaxKind.BangToken:
                case SyntaxKind.MinusToken:
                case SyntaxKind.TildeToken:
                    return 6;
                
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Gets the <see cred="SyntaxKind"/> for the given keyword text.
        /// </summary>
        /// <param name="text">The keyword.</param>
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
                case "function":
                    return SyntaxKind.FunctionKeyword;
                case "if":
                    return SyntaxKind.IfKeyword;
                case "else":
                    return SyntaxKind.ElseKeyword;
                case "while":
                    return SyntaxKind.WhileKeyword;
                case "do":
                    return SyntaxKind.DoKeyword;
                case "for":
                    return SyntaxKind.ForKeyword;
                case "to":
                    return SyntaxKind.ToKeyword;
                case "break":
                    return SyntaxKind.BreakKeyword;
                case "continue":
                    return SyntaxKind.ContinueKeyword;
                case "return":
                    return SyntaxKind.ReturnKeyword;
                default:
                    return SyntaxKind.IdentifierToken;
            }
        }

        /// <summary>
        /// Gets the text for a given <see cref="SyntaxKind"/>.
        /// </summary>
        /// <param name="kind"></param>
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
                case SyntaxKind.TildeToken:
                    return "~";
                case SyntaxKind.AmpersandToken:
                    return "&";
                case SyntaxKind.PipeToken:
                    return "|";
                case SyntaxKind.HatToken:
                    return "^";
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
                case SyntaxKind.FunctionKeyword:
                    return "function";
                case SyntaxKind.LetKeyword:
                    return "let";
                case SyntaxKind.IfKeyword:
                    return "if";
                case SyntaxKind.ElseKeyword:
                    return "else";
                case SyntaxKind.WhileKeyword:
                    return "while";
                case SyntaxKind.DoKeyword:
                    return "do";
                case SyntaxKind.ForKeyword:
                    return "for";
                case SyntaxKind.ToKeyword:
                    return "to";
                case SyntaxKind.BreakKeyword:
                    return "break";
                case SyntaxKind.ContinueKeyword:
                    return "continue";
                case SyntaxKind.ReturnKeyword:
                    return "return";
                case SyntaxKind.CommaToken:
                    return ",";
                case SyntaxKind.ColonToken:
                    return ":";
                case SyntaxKind.QuestionMarkToken:
                    return "?";
                case SyntaxKind.HashtagToken:
                    return "#";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets the syntax kinds of all unary operators.
        /// </summary>
        public static IEnumerable<SyntaxKind> GetUnaryOperatorKinds()
        {
            var kinds = (SyntaxKind[]) Enum.GetValues(typeof(SyntaxKind));
            foreach (var kind in kinds)
            {
                if (GetUnaryOperatorPrecedence(kind) > 0)
                    yield return kind;
            }
        }

        /// <summary>
        /// Gets the syntax kinds of all binary operators.
        /// </summary>
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