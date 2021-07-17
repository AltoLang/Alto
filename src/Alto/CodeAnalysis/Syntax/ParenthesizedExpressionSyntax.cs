namespace Alto.CodeAnalysis.Syntax
{
    public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
    {
        public ParenthesizedExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken openParenthesToken, ExpressionSyntax expression, SyntaxToken closedParenthesesToken)
            : base(syntaxTree)
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
