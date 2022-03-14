using System.Collections.Generic;
using Alto.CodeAnalysis.Symbols;
using Mono.Cecil;

namespace Alto.CodeAnalysis.Emit
{
    internal class TypeMap
    {
        public TypeMap(TypeSymbol symbol, TypeReference reference,
                       Dictionary<FunctionSymbol, MethodReference> methods)
        {
            Symbol = symbol;
            Reference = reference;
            Methods = methods;
        }

        public TypeSymbol Symbol { get; }
        public TypeReference Reference { get; }
        public Dictionary<FunctionSymbol, MethodReference> Methods { get; }
        public string Name => Symbol.Name;

        public MethodReference GetMethod(FunctionSymbol function)
        {
            foreach (var (func, method) in Methods)
            {
                if (function.Equals(func))
                    return method;
            }

            return null;
        }
    }
}