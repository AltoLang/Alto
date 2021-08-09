namespace Alto.CodeAnalysis.Syntax
{
    public sealed class ParameterSyntax : SyntaxNode
    {
        public ParameterSyntax(SyntaxTree syntaxTree, SyntaxToken identifier, TypeClauseSyntax type, bool isOptional, ExpressionSyntax optionalExpression)
            : base(syntaxTree)
        {
            Identifier = identifier;
            Type = type;
            IsOptional = isOptional;
            OptionalExpression = optionalExpression;
        }

        public SyntaxToken Identifier { get; }
        public TypeClauseSyntax Type { get; }
        public bool IsOptional { get; }
        public ExpressionSyntax OptionalExpression { get; }
        public override SyntaxKind Kind => SyntaxKind.Parameter;
    }
}