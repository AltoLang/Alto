using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Alto.CodeAnalysis.Symbols
{
    internal static class BuiltInFunctions
    {
        public static readonly FunctionSymbol Print = new FunctionSymbol("print", ImmutableArray.Create(new ParameterSymbol("text", TypeSymbol.String, false)), TypeSymbol.Void);
        public static readonly FunctionSymbol ReadLine = new FunctionSymbol("readLine", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.String);
        public static readonly FunctionSymbol Random = new FunctionSymbol("random", ImmutableArray.Create(new ParameterSymbol("min", TypeSymbol.Int, false), new ParameterSymbol("max", TypeSymbol.Int, false)), TypeSymbol.Int);
        
        internal static IEnumerable<FunctionSymbol> GetAll()
        {
            var functions = typeof(BuiltInFunctions)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(FunctionSymbol))
                .Select(f => (FunctionSymbol) f.GetValue(null));

            return functions;
        }
    }
}