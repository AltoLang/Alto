using System.Collections.Immutable;
using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundProgram
    {
        public BoundProgram(BoundProgram previous, DiagnosticBag diagnostics, ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies, BoundBlockStatement statement)
        {
            Statement = statement;
            Previous = previous;
            Diagnostics = diagnostics;
            FunctionBodies = functionBodies;
        }

        public BoundBlockStatement Statement { get; }
        public BoundProgram Previous { get; }
        public DiagnosticBag Diagnostics { get; }
        public ImmutableDictionary<FunctionSymbol, BoundBlockStatement> FunctionBodies { get; }
    }
}