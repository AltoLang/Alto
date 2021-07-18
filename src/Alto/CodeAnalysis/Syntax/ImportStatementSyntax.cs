namespace Alto.CodeAnalysis.Syntax
{
    internal class ImportStatementSyntax : StatementSyntax
    {
        public ImportStatementSyntax(SyntaxTree tree, SyntaxToken keyword, SyntaxToken identifier)
            : base(tree)
        {
            Keyword = keyword;
            Identifier = identifier;
        }

        public SyntaxToken Keyword { get; }
        public SyntaxToken Identifier { get; }
        public override SyntaxKind Kind => SyntaxKind.ImportStatement;
    }
}