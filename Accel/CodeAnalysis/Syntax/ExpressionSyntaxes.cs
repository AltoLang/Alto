using System;
using System.Collections.Generic;
using System.Linq;

namespace compiler.CodeAnalysis.Syntax
{
    public abstract class ExpressionSyntax : SyntaxNode
    {
    }
    public sealed class LiteralExpressionSyntax : ExpressionSyntax
    {
        public LiteralExpressionSyntax(SyntaxToken literalToken)
        : this(literalToken, literalToken.Value)
        {}

        public LiteralExpressionSyntax(SyntaxToken literalToken, object value)
        {
            LiteralToken = literalToken;
            Value = value;
        }
        public override SyntaxKind Kind => SyntaxKind.LiteralExpression;
        public SyntaxToken LiteralToken { get; }
        public object Value { get; }

        public override IEnumerable<SyntaxNode> GetChildren()
        {
            yield return LiteralToken;
        }
    }
    public class BinaryExpressionSyntax : ExpressionSyntax
    {
        public BinaryExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right)
        {
            Left = left;
            OperatorToken = operatorToken;
            Right = right;
        }
        public override SyntaxKind Kind => SyntaxKind.BinaryExpression;
        public ExpressionSyntax Left { get; }
        public SyntaxToken OperatorToken { get; }
        public ExpressionSyntax Right { get; }
        public override IEnumerable<SyntaxNode> GetChildren()
        {
            yield return Left;
            yield return OperatorToken;
            yield return Right;
        }
    }

    public class UnaryExpressionSyntax : ExpressionSyntax
    {
        public UnaryExpressionSyntax(SyntaxToken operatorToken, ExpressionSyntax operand)
        {
            OperatorToken = operatorToken;
            Operand = operand;
        }

        public SyntaxToken OperatorToken { get; }
        public ExpressionSyntax Operand { get; }

        public override SyntaxKind Kind => SyntaxKind.UnaryExpression;

        public override IEnumerable<SyntaxNode> GetChildren()
        {
            yield return OperatorToken;
            yield return Operand;
        }
    }


    public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
    {
        public ParenthesizedExpressionSyntax(SyntaxToken openParenthesToken, ExpressionSyntax expression, SyntaxToken closedParenthesesToken)
        {
            OpenParenthesToken = openParenthesToken;
            Expression = expression;
            ClosedParenthesesToken = closedParenthesesToken;
        }
        public override SyntaxKind Kind => SyntaxKind.ParenthesizedExpression;
        public SyntaxToken OpenParenthesToken { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken ClosedParenthesesToken { get; }
        public override IEnumerable<SyntaxNode> GetChildren()
        {
            yield return OpenParenthesToken;
            yield return Expression;
            yield return ClosedParenthesesToken;
        }
    }

    public sealed class NameExpressionSyntax : ExpressionSyntax
    {
        public NameExpressionSyntax(SyntaxToken identifierToken)
        {
            IdentifierToken = identifierToken;
        }

        public SyntaxToken IdentifierToken { get; }

        public override SyntaxKind Kind => SyntaxKind.NameExpresion;

        public override IEnumerable<SyntaxNode> GetChildren()
        {
            yield return IdentifierToken;
        }
    }

    public sealed class AssignmentExpresionSyntax : ExpressionSyntax
    {
        public AssignmentExpresionSyntax(SyntaxToken identifierToken, SyntaxToken equalsToken, ExpressionSyntax expression)
        {
            IdentifierToken = identifierToken;
            EqualsToken = equalsToken;
            Expression = expression;
        }

        public SyntaxToken IdentifierToken { get; }
        public SyntaxToken EqualsToken { get; }
        public ExpressionSyntax Expression { get; }

        public override SyntaxKind Kind => SyntaxKind.AssignmentExpression;

        public override IEnumerable<SyntaxNode> GetChildren()
        {
            yield return IdentifierToken;
            yield return EqualsToken;
            yield return Expression;
        }
    }
}