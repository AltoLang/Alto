namespace Alto.CodeAnalysis.Symbols
{
    public class GlobalVariableSymbol : VariableSymbol
    {
        internal GlobalVariableSymbol(string name, bool isReadOnly, TypeSymbol type)
            : base(name, isReadOnly, type)
        {
        }

        public override SymbolKind Kind => SymbolKind.GlobalVariable;
        public override string ToString() => Name;
    }
}