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
            // DEBUG
            // DELETE ME
            if (_symbols != null)
            {
                Console.WriteLine("Symbols:");
                foreach (var (nm, sym) in _symbols)
                    Console.WriteLine($"    {nm}");
            }
            
            if (_symbols != null && _symbols.TryGetValue(name, out var symbol))
                return symbol;

            return Parent?.TryLookupSymbol(name);
        }

        public ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();

        public ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();

        private ImmutableArray<TSymbol> GetDeclaredSymbols<TSymbol>()
            where TSymbol : Symbol
        {
            if (_symbols == null)
                return ImmutableArray<TSymbol>.Empty;

            return _symbols.Values.OfType<TSymbol>().ToImmutableArray();
        }
    }
}