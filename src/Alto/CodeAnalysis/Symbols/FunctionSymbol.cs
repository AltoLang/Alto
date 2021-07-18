using System.Collections.Immutable;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Symbols
{

    public sealed class FunctionSymbol : Symbol
    {
        public FunctionSymbol(string name, ImmutableArray<ParameterSymbol> parameters, TypeSymbol type, 
                              FunctionDeclarationSyntax declaration = null, SyntaxTree tree = null)
            : base(name)
        {
            Parameters = parameters;
            Type = type;
            Declaration = declaration;
            Tree = tree;
        }

        public override SymbolKind Kind => SymbolKind.Function;
        public ImmutableArray<ParameterSymbol> Parameters { get; }
        public TypeSymbol Type { get; }
        public FunctionDeclarationSyntax Declaration { get; }
        public SyntaxTree Tree { get; }

        public override string ToString() => Name;
    }
}