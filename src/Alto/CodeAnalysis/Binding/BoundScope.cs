using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Alto.CodeAnalysis.Symbols;
using System.Linq;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundScope
    {
        private Dictionary<string, Symbol> _symbols;

        public BoundScope Parent { get; }
    
        public BoundScope(BoundScope parent)
        {
            Parent = parent;
        }

        public bool TryDeclareVariable(VariableSymbol variable) => TryDeclareSymbol(variable);
        public bool TryDeclareFunction(FunctionSymbol function) => TryDeclareSymbol(function);
        public bool TryDeclareType(TypeSymbol type) => TryDeclareSymbol(type);

        private bool TryDeclareSymbol<TSymbol>(TSymbol symbol)
            where TSymbol : Symbol
        {
            if (_symbols == null)
                _symbols = new Dictionary<string, Symbol>();
            else if (_symbols.ContainsKey(symbol.Name))
                return false;
            
            _symbols.Add(symbol.Name, symbol);
            return true;
        }

        public Symbol TryLookupSymbol(string name)
        {
            if (_symbols != null && _symbols.TryGetValue(name, out var symbol))
                return symbol;
            
            return Parent?.TryLookupSymbol(name);
        }

        public ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();
        public ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();
        public ImmutableArray<TypeSymbol> GetDeclaredTypes() => GetDeclaredSymbols<TypeSymbol>();

        private ImmutableArray<TSymbol> GetDeclaredSymbols<TSymbol>()
            where TSymbol : Symbol
        {
            if (_symbols == null)
                return ImmutableArray<TSymbol>.Empty;

            return _symbols.Values.OfType<TSymbol>().ToImmutableArray();
        }
    }
}