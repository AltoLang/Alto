using System.Collections.Immutable;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundGlobalScope
    {
        public BoundGlobalScope(BoundGlobalScope previous, ImmutableArray<Diagnostic> diagnostics, 
            ImmutableArray<FunctionSymbol> functions, ImmutableArray<VariableSymbol> variables, 
            ImmutableArray<BoundStatement> statements, ImmutableArray<SyntaxTree> importedTrees)
        {
            Previous = previous;
            Diagnostics = diagnostics;
            Functions = functions;  
            Variables = variables;
            Statements = statements;
            ImportedTrees = importedTrees;
        }

        public BoundGlobalScope Previous { get; }
        public ImmutableArray<Diagnostic> Diagnostics { get; }
        public ImmutableArray<FunctionSymbol> Functions { get; }
        public ImmutableArray<VariableSymbol> Variables { get; }
        public ImmutableArray<BoundStatement> Statements { get; }
        public ImmutableArray<SyntaxTree> ImportedTrees { get; }
    }
}