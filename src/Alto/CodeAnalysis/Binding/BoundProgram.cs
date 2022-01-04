using System.Collections.Immutable;
using Alto.CodeAnalysis.Emit;
using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundProgram
    {
        public BoundProgram(BoundProgram previous,
                            DiagnosticBag diagnostics,
                            FunctionSymbol mainFunction,
                            FunctionSymbol scriptFunction,
                            ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies,
                            ImmutableArray<AssemblyImport> imports)
        {
            Previous = previous;
            Diagnostics = diagnostics;
            MainFunction = mainFunction;
            ScriptFunction = scriptFunction;
            FunctionBodies = functionBodies;
            Imports = imports;
        }

        public BoundProgram Previous { get; }
        public DiagnosticBag Diagnostics { get; }
        public FunctionSymbol MainFunction { get; }
        public FunctionSymbol ScriptFunction { get; }
        public ImmutableDictionary<FunctionSymbol, BoundBlockStatement> FunctionBodies { get; internal set; }
        public ImmutableArray<AssemblyImport> Imports { get; }
    }
}