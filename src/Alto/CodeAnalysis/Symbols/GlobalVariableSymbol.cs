using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Symbols
{
    public class GlobalVariableSymbol : VariableSymbol
    {
        internal GlobalVariableSymbol(string name, bool isReadOnly, TypeSymbol type, SyntaxTree tree = null)
            : base(name, isReadOnly, type, tree)
        {
        }

        public override SymbolKind Kind => SymbolKind.GlobalVariable;
        public override string ToString() => Name;
    }
}