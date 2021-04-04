namespace Alto.CodeAnalysis.Syntax
{
    internal class PrintStatementSyntax : StatementSyntax
    {
        // TEMP
        public SyntaxToken Keyword {get;}
        public ExpressionSyntax Print {get;}

        public PrintStatementSyntax(SyntaxToken keyword, ExpressionSyntax print)
        {
            Keyword = keyword;
            Print = print;
        }

        public override SyntaxKind Kind => SyntaxKind.PrintStatement;
    }
}