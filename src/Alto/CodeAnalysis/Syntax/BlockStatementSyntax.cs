using System.Collections.Immutable;

namespace Alto.CodeAnalysis.Syntax
{
    public sealed class BlockStatementSyntax : StatementSyntax
    {
        public BlockStatementSyntax(SyntaxTree syntaxTree, SyntaxToken openBraceToken, 
                                    ImmutableArray<StatementSyntax> statements,
                                    ImmutableArray<FunctionDeclarationSyntax> functions,
                                    SyntaxToken closeBraceToken)
            : base(syntaxTree)
        {
            OpenBraceToken = openBraceToken;
            Statements = statements;
            Functions = functions;
            CloseBraceToken = closeBraceToken;
        }

        public SyntaxToken OpenBraceToken { get; }
        public ImmutableArray<StatementSyntax> Statements { get; }
        public ImmutableArray<FunctionDeclarationSyntax> Functions { get; }
        public SyntaxToken CloseBraceToken { get; }

        public override SyntaxKind Kind => SyntaxKind.BlockStatement;
    }
}