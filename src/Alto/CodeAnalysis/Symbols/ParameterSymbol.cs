namespace Alto.CodeAnalysis.Symbols
{
    public sealed class ParameterSymbol : LocalVariableSymbol
    {
        public ParameterSymbol(string name, TypeSymbol type)
            : base(name: name, isReadOnly: true, type: type)
        {
        }

        public override SymbolKind Kind => SymbolKind.Parameter;
    }
}