namespace Alto.CodeAnalysis.Syntax
{
    public sealed class AssignmentExpressionSyntax : ExpressionSyntax
    {
        public AssignmentExpressionSyntax(SyntaxToken identifierToken, SyntaxToken assignmentToken, ExpressionSyntax expression)
        {
            IdentifierToken = identifierToken;
            AssignmentToken = assignmentToken;
            Expression = expression;
        }

        public override SyntaxKind Kind => SyntaxKind.AssignmentExpression;
        public SyntaxToken IdentifierToken { get; }
        public SyntaxToken AssignmentToken { get; }
        public ExpressionSyntax Expression { get; }
    }
}