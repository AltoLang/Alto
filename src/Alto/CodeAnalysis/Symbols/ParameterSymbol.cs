using Alto.CodeAnalysis.Binding;

namespace Alto.CodeAnalysis.Symbols
{
    public sealed class ParameterSymbol : LocalVariableSymbol
    {
        public ParameterSymbol(string name, TypeSymbol type, int ordinal)
            : base(name: name, isReadOnly: true, type: type)
        {
            Ordinal = ordinal;
            IsOptional = false;
        }

        internal ParameterSymbol(string name, TypeSymbol type, int ordinal, bool isOptional, BoundExpression optionalValue = null)
            : base(name: name, isReadOnly: true, type: type)
        {
            Ordinal = ordinal;
            IsOptional = isOptional;
            OptionalValue = optionalValue;
        }

        public int Ordinal { get; }
        public bool IsOptional { get; }
        internal BoundExpression OptionalValue { get; }
        public override SymbolKind Kind => SymbolKind.Parameter;
    }
}