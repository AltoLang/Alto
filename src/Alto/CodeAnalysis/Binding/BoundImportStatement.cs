using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal class BoundImportStatement : BoundStatement
    {
        public BoundImportStatement(SyntaxTree importTree, string name)
        {
            ImportTree = importTree;
            Name = name;
        }

        public SyntaxTree ImportTree { get; }
        public string Name { get; }
        public override BoundNodeKind Kind => BoundNodeKind.ImportStatement;
    }
}