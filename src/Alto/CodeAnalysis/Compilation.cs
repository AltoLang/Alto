using System;
using System.Collections.Generic;
using System.Linq;
using Alto.CodeAnalysis.Binding;
using Alto.CodeAnalysis.Syntax;
using System.Collections.Immutable;
using System.Threading;
using System.IO;
using Alto.CodeAnalysis.Lowering;
using Alto.CodeAnalysis.Symbols;
using BindingFlags = System.Reflection.BindingFlags;

namespace Alto.CodeAnalysis
{
    public sealed class Compilation
    {
        private BoundGlobalScope _globalScope;
        private Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> _localFunctions = new Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>>();

        private Compilation(bool isScript, Compilation previous, SyntaxTree coreSyntax, bool checkCallsiteTrees = true, params SyntaxTree[] syntaxTrees)
        {
            IsScript = isScript;
            Previous = previous;
            CoreSyntax = coreSyntax;
            CheckCallsiteTrees = checkCallsiteTrees;
            SyntaxTrees = syntaxTrees.ToImmutableArray();
        }

        public static Compilation Create(SyntaxTree coreSyntax, params SyntaxTree[] syntaxTrees)
        {
            return new Compilation(isScript: false, previous: null, coreSyntax, checkCallsiteTrees: true, syntaxTrees);
        }

        public static Compilation CreateScript(Compilation previous, SyntaxTree coreSyntax, params SyntaxTree[] syntaxTrees)
        {
            return new Compilation(isScript: true, previous: previous, coreSyntax, checkCallsiteTrees: true, syntaxTrees);
        }

        public bool IsScript { get; }

        public Compilation Previous { get; }
        public bool CheckCallsiteTrees { get; }
        public ImmutableArray<SyntaxTree> SyntaxTrees { get; }
        public ImmutableArray<FunctionSymbol> Functions => GlobalScope.Functions;
        public ImmutableArray<VariableSymbol> Variables => GlobalScope.Variables;
        public SyntaxTree CoreSyntax { get; }

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
                    var globalScope = Binder.BindGlobalScope(IsScript, Previous?.GlobalScope, CoreSyntax, SyntaxTrees, CheckCallsiteTrees, out var localFunctions);
                    _localFunctions = localFunctions;
                    Interlocked.CompareExchange(ref _globalScope, globalScope, null);
                }

                return _globalScope;
            }
        }

        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables)
        {
            var selectDiagnostics = SyntaxTrees.SelectMany(tree => tree.Diagnostics);
            var diagnostics = selectDiagnostics.Concat(GlobalScope.Diagnostics).Concat(CoreSyntax.Diagnostics).ToImmutableArray();
            if (diagnostics.Any())
                return new EvaluationResult(diagnostics, null);

            var program = GetProgram();
            var appPath = Environment.GetCommandLineArgs()[0];
            var appDir = Path.GetDirectoryName(appPath);
            var cfgPath = Path.Combine(appDir, "cfg.dot");

            var cfgStatement = !program.Statement.Statements.Any() && program.FunctionBodies.Any() 
                               ? program.FunctionBodies.Last().Value 
                               : program.Statement;

            var cfg = ControlFlowGraph.Create(cfgStatement);
            using (var writer = new StreamWriter(cfgPath))
                cfg.WriteTo(writer);

            if (program.Diagnostics.Any())
                return new EvaluationResult(program.Diagnostics.ToImmutableArray(), null);

            var statement = GetStatement();
            MergeLocalAndGlobalFunctions(program);
            var evaluator = new Evaluator(program, variables);
            var value = evaluator.Evaluate();

            return new EvaluationResult(ImmutableArray<Diagnostic>.Empty, value);
        }
        
        public void EmitTree(TextWriter writer)
        {
            var program = GetProgram();
            if (program.Statement.Statements.Any())
            {
                program.Statement.WriteTo(writer);
            }
            else
            {
                foreach (var functionBody in program.FunctionBodies)
                {
                    if (!GlobalScope.Functions.Contains(functionBody.Key))
                        continue;

                    functionBody.Key.WriteTo(writer);
                    functionBody.Value.WriteTo(writer);
                }
            }
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

        private BoundBlockStatement GetStatement()
        {
            var statements = GlobalScope.Statements;
            if (statements.Any())
            {
                var result = GlobalScope.Statements[0];
                if (result.Kind != BoundNodeKind.BlockStatement)
                    result = new BoundBlockStatement(GlobalScope.Statements);

                return Lowerer.Lower(result);
            }
            else
            {
                var childStatements = ImmutableArray<BoundStatement>.Empty;
                var statement = new BoundBlockStatement(childStatements);

                return statement;
            }   
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
    }
}