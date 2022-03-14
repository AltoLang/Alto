using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Alto.CodeAnalysis.Symbols
{
    internal static class BuiltInTypes
    {
        internal static IEnumerable<TypeSymbol> GetAll()
        {
            var types = typeof(TypeSymbol)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(TypeSymbol))
                .Select(f => (TypeSymbol) f.GetValue(null));

            return types;
        }
    }
}