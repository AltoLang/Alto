namespace compiler.CodeAnalysis.Syntax
{
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
    }

}
