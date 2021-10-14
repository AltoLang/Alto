using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Symbols
{
    public class LocalVariableSymbol : VariableSymbol
    {
        internal LocalVariableSymbol(string name, bool isReadOnly, TypeSymbol type, SyntaxTree tree = null)
            : base(name, isReadOnly, type, tree)
        {
        }

        public override SymbolKind Kind => SymbolKind.LocalVariable;
        public override string ToString() => Name;
    }
}