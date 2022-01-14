namespace Alto.CodeAnalysis.Syntax
{
    internal class MemberAccessExpression : ExpressionSyntax
    {
        public MemberAccessExpression(SyntaxTree tree, ExpressionSyntax expression, SyntaxToken fullStop, SyntaxToken identififer)
            : base(tree)
        {
            Expression = expression;
            FullStop = fullStop;
            Identififer = identififer;
        }

        public ExpressionSyntax Expression { get; }
        public SyntaxToken FullStop { get; }
        public SyntaxToken Identififer { get; }

        public override SyntaxKind Kind => SyntaxKind.MemberAccessExpression;
    }
}