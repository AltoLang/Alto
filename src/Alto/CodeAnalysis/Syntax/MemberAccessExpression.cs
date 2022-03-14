namespace Alto.CodeAnalysis.Syntax
{
    internal class MemberAccessExpressionSyntax : ExpressionSyntax
    {
        public MemberAccessExpressionSyntax(SyntaxTree tree, ExpressionSyntax left, SyntaxToken fullStop, ExpressionSyntax right)
            : base(tree)
        {
            Tree = tree;
            Left = left;
            FullStop = fullStop;
            Right = right;
        }

        public SyntaxTree Tree { get; }
        public ExpressionSyntax Left { get; }
        public SyntaxToken FullStop { get; }
        public ExpressionSyntax Right { get; }
    
        public override SyntaxKind Kind => SyntaxKind.MemberAccessExpression;
    }
}