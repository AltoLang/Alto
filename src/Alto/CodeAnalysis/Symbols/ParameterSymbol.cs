using Alto.CodeAnalysis.Binding;

namespace Alto.CodeAnalysis.Symbols
{
    public sealed class ParameterSymbol : LocalVariableSymbol
    {
        public ParameterSymbol(string name, TypeSymbol type, bool isOptional = false)
            : base(name: name, isReadOnly: true, type: type)
        {
            IsOptional = isOptional;
        }

        internal ParameterSymbol(string name, TypeSymbol type, bool isOptional, BoundExpression optionalValue = null)
            : base(name: name, isReadOnly: true, type: type)
        {
            IsOptional = isOptional;
            OptionalValue = optionalValue;
        }

        public bool IsOptional { get; }
        internal BoundExpression OptionalValue { get; }
        public override SymbolKind Kind => SymbolKind.Parameter;
    }
}