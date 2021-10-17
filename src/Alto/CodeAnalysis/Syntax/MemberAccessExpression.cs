namespace Alto.CodeAnalysis.Syntax
{
    internal class MemberAccessExpression : ExpressionSyntax
    {

        public MemberAccessExpression(SyntaxTree tree, SyntaxToken parentIdentifier, SyntaxToken fullStop, SyntaxToken memberIdentifier)
            : base(tree)
        {
            ParentIdentifier = parentIdentifier;
            FullStop = fullStop;
            MemberIdentifier = memberIdentifier;
        }

        public SyntaxToken ParentIdentifier { get; }
        public SyntaxToken FullStop { get; }
        public SyntaxToken MemberIdentifier { get; }
        public override SyntaxKind Kind => SyntaxKind.MemberAccessExpression;
    }
}