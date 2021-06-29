namespace Alto.CodeAnalysis.Syntax
{
    internal class ReturnStatementSyntax : StatementSyntax
    {
        public ReturnStatementSyntax(SyntaxToken keyword, ExpressionSyntax returnExpression)
        {
            Keyword = keyword;
            ReturnExpression = returnExpression;
        }

        public SyntaxToken Keyword { get; }
        public ExpressionSyntax ReturnExpression { get; }
        public override SyntaxKind Kind => SyntaxKind.ReturnStatement;
    }
}