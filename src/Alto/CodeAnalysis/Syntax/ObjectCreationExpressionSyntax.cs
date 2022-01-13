namespace Alto.CodeAnalysis.Syntax
{
    internal class ObjectCreationExpressionSyntax : ExpressionSyntax
    {
        public ObjectCreationExpressionSyntax(SyntaxTree tree, SyntaxToken newKeyword, SyntaxToken identifier, SyntaxToken openParenthesisToken, SeparatedSyntaxList<ExpressionSyntax> args, SyntaxToken closedParenthesisToken)
            : base(tree)
        {
            Tree = tree;
            NewKeyword = newKeyword;
            Identifier = identifier;
            OpenParenthesisToken = openParenthesisToken;
            Args = args;
            ClosedParenthesisToken = closedParenthesisToken;
        }

        public SyntaxTree Tree { get; }
        public SyntaxToken NewKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken OpenParenthesisToken { get; }
        public SeparatedSyntaxList<ExpressionSyntax> Args { get; }
        public SyntaxToken ClosedParenthesisToken { get; }

        public override SyntaxKind Kind => SyntaxKind.ObjectCreationExpression;
    }
}