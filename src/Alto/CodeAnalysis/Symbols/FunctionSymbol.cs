using System.Collections.Immutable;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Symbols
{
    public sealed class FunctionSymbol : Symbol
    {
        public FunctionSymbol(string name, ImmutableArray<ParameterSymbol> parameters, TypeSymbol type, 
                              FunctionDeclarationSyntax declaration = null, SyntaxTree tree = null)
            : base(name, tree)
        {
            Parameters = parameters;
            Type = type;
            Declaration = declaration;
        }

        public override SymbolKind Kind => SymbolKind.Function;
        public ImmutableArray<ParameterSymbol> Parameters { get; }
        public TypeSymbol Type { get; }
        public FunctionDeclarationSyntax Declaration { get; }

        public override string ToString() => Name;

        public override bool Equals(object obj)
        {
            if (obj is FunctionSymbol func)
            {
                if (Type == null)
                    System.Console.WriteLine($"{base.Name} Type is null");

                if (base.Name != func.Name ||
                    !this.Type.Equals(func.Type) ||
                    Parameters.Length != func.Parameters.Length)
                {
                    if (func.Name == "write")
                        System.Console.WriteLine($"1) {base.Name != func.Name} || { !Type.Equals(func.Type)} || {Parameters.Length != func.Parameters.Length}");
                    return false;
                }

                for (int i = 0; i < Parameters.Length; i++)
                {
                    var myParam = Parameters[i];
                    var otherParam = func.Parameters[i];
                    if (!myParam.Equals(otherParam))
                        return false;
                }
                
                return true;
            }

            return Equals(obj);
        }
    }
}