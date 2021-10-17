namespace Alto.CodeAnalysis.Syntax
{
    internal class ClassDeclarationSyntax : MemberSyntax
    {
        public ClassDeclarationSyntax(SyntaxTree tree, SyntaxToken keyword, SyntaxToken identifier, BlockStatementSyntax body)
            : base(tree)
        {
            Keyword = keyword;
            Identifier = identifier;
            Body = body;
        }

        public SyntaxToken Keyword { get; }
        public SyntaxToken Identifier { get; }
        public BlockStatementSyntax Body { get; }
        public override SyntaxKind Kind => SyntaxKind.ClassDeclaration;
    }
}