namespace Alto.CodeAnalysis.Syntax
{
    internal class ObjectCreationExpression : ExpressionSyntax
    {

        public ObjectCreationExpression(SyntaxTree tree, SyntaxToken newKeyword, SyntaxToken identifier, SyntaxToken openParenthesis, SyntaxToken closedParenthesis)
            : base(tree)
        {
            NewKeyword = newKeyword;
            Identifier = identifier;
            OpenParenthesis = openParenthesis;
            ClosedParenthesis = closedParenthesis;
        }

        public SyntaxToken NewKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken OpenParenthesis { get; }
        public SyntaxToken ClosedParenthesis { get; }
        public override SyntaxKind Kind => SyntaxKind.ObjectCreationExpression;
    }
}