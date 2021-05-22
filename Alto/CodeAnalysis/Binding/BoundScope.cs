using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundScope
    {
        private Dictionary<string, VariableSymbol> _variables = new Dictionary<string, VariableSymbol>();
        private Dictionary<string, FunctionSymbol> _functions = new Dictionary<string, FunctionSymbol>(); 

        public BoundScope Parent { get; }
    
        public BoundScope(BoundScope parent)
        {
            Parent = parent;
        }

        public bool TryLookupVariable(string name, out VariableSymbol variable)
        {
            if (_variables.TryGetValue(name, out variable))
                return true;

            if (Parent == null)
                return false;

            return Parent.TryLookupVariable(name, out variable);
        }

        public bool TryDeclareVariable(VariableSymbol variable)
        {
            if (_variables.ContainsKey(variable.Name))
                return false;

            _variables.Add(variable.Name, variable);
            return true;
        }

        public bool TryLookupFunction(string name, out FunctionSymbol function)
        {
            if (_functions.TryGetValue(name, out function))
                return true;

            if (Parent == null)
                return false;

            return Parent.TryLookupFunction(name, out function);
        }

        public bool TryDeclareFunction(FunctionSymbol function)
        {
            if (_functions.ContainsKey(function.Name))
                return false;

            _functions.Add(function.Name, function);
            return true;
        }

        public ImmutableArray<VariableSymbol> GetDeclaredVariables()
        {
            return _variables.Values.ToImmutableArray();
        }
    }
}