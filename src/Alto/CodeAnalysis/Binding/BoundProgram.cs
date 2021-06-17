using System.Collections.Immutable;
using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundProgram
    {
        public BoundProgram(DiagnosticBag diagnostics, ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies, BoundBlockStatement statement)
        {
            Statement = statement;
            Diagnostics = diagnostics;
            FunctionBodies = functionBodies;
        }

        public BoundBlockStatement Statement { get; }
        public DiagnosticBag Diagnostics { get; }
        public ImmutableDictionary<FunctionSymbol, BoundBlockStatement> FunctionBodies { get; }
    }
}