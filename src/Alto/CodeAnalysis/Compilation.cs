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

namespace Alto.CodeAnalysis
{
    public sealed class Compilation
    {
        private BoundGlobalScope _globalScope;

        public Compilation(SyntaxTree coreSyntax, params SyntaxTree[] syntaxTrees) : this(null, coreSyntax, true, syntaxTrees)
        {
        }

        private Compilation(Compilation previous, SyntaxTree coreSyntax, bool checkCallsiteTrees = true, params SyntaxTree[] syntaxTrees)
        {
            Previous = previous;
            CoreSyntax = coreSyntax;
            CheckCallsiteTrees = checkCallsiteTrees;
            SyntaxTrees = syntaxTrees.ToImmutableArray();
        }


        public Compilation Previous { get; }
        public bool CheckCallsiteTrees { get; }
        public ImmutableArray<SyntaxTree> SyntaxTrees { get; }
        public ImmutableArray<FunctionSymbol> Functions => GlobalScope.Functions;
        public ImmutableArray<VariableSymbol> Variables => GlobalScope.Variables;
        public SyntaxTree CoreSyntax { get; }

        internal BoundGlobalScope GlobalScope
        {
            get
            {
                if (_globalScope == null)
                {
                    var globalScope = Binder.BindGlobalScope(Previous?.GlobalScope, CoreSyntax, SyntaxTrees, CheckCallsiteTrees);
                    Interlocked.CompareExchange(ref _globalScope, globalScope, null);
                }

                return _globalScope;
            }
        }

        public Compilation ContinueWith(SyntaxTree syntaxTree)
        {
            var c = new Compilation(this, syntaxTree, false);
            return c;
        }

        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables)
        {
            var selectDiagnostics = SyntaxTrees.SelectMany(tree => tree.Diagnostics);
            var diagnostics = selectDiagnostics.Concat(GlobalScope.Diagnostics).Concat(CoreSyntax.Diagnostics).ToImmutableArray();
            if (diagnostics.Any())
                return new EvaluationResult(diagnostics, null);

            var program = Binder.BindProgram(GlobalScope);
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
            var evaluator = new Evaluator(program.FunctionBodies, statement, variables);
            var value = evaluator.Evaluate();

            return new EvaluationResult(ImmutableArray<Diagnostic>.Empty, value);
        }
        
        public void EmitTree(TextWriter writer)
        {
            var program = Binder.BindProgram(GlobalScope);

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
    }
}