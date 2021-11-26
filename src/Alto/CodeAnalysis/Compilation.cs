using System;
using System.Collections.Generic;
using System.Linq;
using Alto.CodeAnalysis.Binding;
using Alto.CodeAnalysis.Syntax;
using Alto.CodeAnalysis.Syntax.Preprocessing;
using System.Collections.Immutable;
using System.Threading;
using System.IO;
using Alto.CodeAnalysis.Lowering;
using Alto.CodeAnalysis.Symbols;
using BindingFlags = System.Reflection.BindingFlags;
using Alto.CodeAnalysis.Emit;

namespace Alto.CodeAnalysis
{
    public sealed class Compilation
    {
        private BoundGlobalScope _globalScope;
        private Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> _localFunctions = new Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>>();

        private Compilation(bool isScript, Compilation previous, bool checkCallsiteTrees = true, params SyntaxTree[] syntaxTrees)
        {
            IsScript = isScript;
            Previous = previous;
            CheckCallsiteTrees = checkCallsiteTrees;
            SyntaxTrees = syntaxTrees.ToImmutableArray();
        }

        public static Compilation Create(params SyntaxTree[] syntaxTrees)
        {
            return new Compilation(isScript: false, previous: null, checkCallsiteTrees: true, syntaxTrees);
        }

        public static Compilation CreateScript(Compilation previous, params SyntaxTree[] syntaxTrees)
        {
            return new Compilation(isScript: true, previous: previous, checkCallsiteTrees: true, syntaxTrees);
        }

        public bool IsScript { get; }
        public Compilation Previous { get; }
        public bool CheckCallsiteTrees { get; }
        public ImmutableArray<SyntaxTree> SyntaxTrees { get; }
        public FunctionSymbol MainFunction => GlobalScope.MainFunction;
        public FunctionSymbol ScriptFunction => GlobalScope.ScriptFunction;
        public ImmutableArray<FunctionSymbol> Functions => GlobalScope.Functions;
        public ImmutableArray<VariableSymbol> Variables => GlobalScope.Variables;

        internal Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> LocalFunctions 
        {
            get 
            { 
                return _localFunctions; 
            } 
        }

        internal BoundGlobalScope GlobalScope
        {
            get
            {
                if (_globalScope == null)
                {
                    var globalScope = Binder.BindGlobalScope(IsScript, Previous?.GlobalScope, SyntaxTrees, CheckCallsiteTrees, out var localFunctions);
                    _localFunctions = localFunctions;
                    Interlocked.CompareExchange(ref _globalScope, globalScope, null);
                }

                return _globalScope;
            }
        }

        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables)
        {
            var selectDiagnostics = SyntaxTrees.SelectMany(tree => tree.Diagnostics);
            var diagnostics = selectDiagnostics.Concat(GlobalScope.Diagnostics).ToImmutableArray();
            if (diagnostics.Any())
                return new EvaluationResult(diagnostics, null);

            var program = GetProgram();
            // var appPath = Environment.GetCommandLineArgs()[0];
            // var appDir = Path.GetDirectoryName(appPath);
            // var cfgPath = Path.Combine(appDir, "cfg.dot");

            // var cfgStatement = !program.Statement.Statements.Any() && program.FunctionBodies.Any() 
            //                    ? program.FunctionBodies.Last().Value 
            //                    : program.Statement;

            // var cfg = ControlFlowGraph.Create(cfgStatement);
            // using (var writer = new StreamWriter(cfgPath))
            //     cfg.WriteTo(writer);

            if (program.Diagnostics.Any())
                return new EvaluationResult(program.Diagnostics.ToImmutableArray(), null);

            MergeLocalAndGlobalFunctions(program);
            var evaluator = new Evaluator(program, variables);
            var value = evaluator.Evaluate();

            return new EvaluationResult(ImmutableArray<Diagnostic>.Empty, value);
        }
        
        public void EmitTree(TextWriter writer)
        {
            if (GlobalScope.MainFunction != null)
                EmitTree(GlobalScope.MainFunction, writer);
            else if (GlobalScope.ScriptFunction != null)
                EmitTree(GlobalScope.ScriptFunction, writer);
        }

        public void EmitTree(FunctionSymbol symbol, TextWriter writer)
        {
            var program = GetProgram();
            symbol.WriteTo(writer);
            writer.Write(" ");
            if (!program.FunctionBodies.TryGetValue(symbol, out var body))
                return;
            
            body.WriteTo(writer);
        }

        public IEnumerable<Symbol> GetSymbols()
        {
            var compilation = this;
            var seenNames = new HashSet<string>();

            while (compilation != null)
            {
                // Built-in functions
                var bindingFlags = 
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;

                var builtinFunctions = typeof(BuiltInFunctions)
                    .GetFields(bindingFlags)
                    .Where(fi => fi.FieldType == typeof(FunctionSymbol))
                    .Select(fi => (FunctionSymbol)fi.GetValue(obj: null))
                    .ToList();
                
                foreach (var function in compilation.Functions)
                    if (seenNames.Add(function.Name))
                        yield return function;

                foreach (var builtinFunction in builtinFunctions)
                    if (seenNames.Add(builtinFunction.Name))
                        yield return builtinFunction;
                
                foreach (var variable in compilation.Variables)
                    if (seenNames.Add(variable.Name))
                        yield return variable;
                
                compilation = compilation.Previous;
            }
        }

        private BoundProgram GetProgram()
        {
            var previous = Previous == null ? null : Previous.GetProgram();
            return Binder.BindProgram(IsScript, previous, GlobalScope);
        }

        private void MergeLocalAndGlobalFunctions(BoundProgram program)
        {
            var functionBodies = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();

            foreach (var func in program.FunctionBodies)
                functionBodies.Add(func.Key, func.Value);

            foreach (var localScope in _localFunctions)
            {
                foreach (var function in localScope.Value)
                    functionBodies.Add(function.Item1, function.Item2);
            }

            program.FunctionBodies = functionBodies.ToImmutable();
        }

        public ImmutableArray<Diagnostic> Emit(string moduleName, string[] references, string outPath)
        {
            var program = GetProgram();
            return Emitter.Emit(program, moduleName, references, outPath);
        }
    }
}