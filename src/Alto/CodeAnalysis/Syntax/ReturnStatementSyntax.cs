namespace Alto.CodeAnalysis.Syntax
{
    internal class ReturnStatementSyntax : StatementSyntax
    {
        public ReturnStatementSyntax(SyntaxTree syntaxTree, SyntaxToken keyword, ExpressionSyntax returnExpression)
            : base(syntaxTree)
        {
            Keyword = keyword;
            ReturnExpression = returnExpression;
        }

        public SyntaxToken Keyword { get; }
        public ExpressionSyntax ReturnExpression { get; }
        public override SyntaxKind Kind => SyntaxKind.ReturnStatement;
    }
}