namespace Alto.CodeAnalysis.Syntax
{
    public sealed class ParameterSyntax : SyntaxNode
    {
        public ParameterSyntax(SyntaxToken identifier, TypeClauseSyntax type)
        {
            Identifier = identifier;
            Type = type;
        }

        public SyntaxToken Identifier { get; }
        public TypeClauseSyntax Type { get; }
        public override SyntaxKind Kind => SyntaxKind.Parameter;
    }
}