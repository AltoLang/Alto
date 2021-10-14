using System;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Symbols
{
    public abstract class VariableSymbol : Symbol
    {
        internal VariableSymbol(string name, bool isReadOnly, TypeSymbol type, SyntaxTree tree = null)
            : base(name, tree)
        {
            IsReadOnly = isReadOnly;
            Type = type;
        }

        public bool IsReadOnly { get; }
        public TypeSymbol Type { get; }
        public override string ToString() => Name;
    }
}