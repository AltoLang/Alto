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

        public Compilation(SyntaxTree syntaxTree) : this(null, syntaxTree)
        {
            SyntaxTree = syntaxTree;
        }

        private Compilation(Compilation previous, SyntaxTree syntaxTree)
        {
            Previous = previous;
            SyntaxTree = syntaxTree;
        }


        public Compilation Previous { get; }
        public SyntaxTree SyntaxTree { get; set; }

        internal BoundGlobalScope GlobalScope
        {
            get
            {
                if (_globalScope == null)
                {
                    var globalScope = Binder.BindGlobalScope(Previous?.GlobalScope, SyntaxTree.Root);
                    Interlocked.CompareExchange(ref _globalScope, globalScope, null);
                }

                return _globalScope;
            }
        }

        public Compilation ContinueWith(SyntaxTree syntaxTree)
        {
            var c = new Compilation(this, SyntaxTree);
            c.SyntaxTree = syntaxTree;
            return c;
        }

        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables)
        {
            var diagnostics = SyntaxTree.Diagnostics.Concat(GlobalScope.Diagnostics).ToImmutableArray();
            if (diagnostics.Any())
                return new EvaluationResult(diagnostics, null);

            var program = Binder.BindProgram(GlobalScope);
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
                program.Statement.WriteTo(writer);
            else
                foreach (var functionBody in program.FunctionBodies)
                    if (!GlobalScope.Functions.Contains(functionBody.Key))
                        continue;
                    else
                        functionBody.Value.WriteTo(writer);
        }

        private BoundBlockStatement GetStatement()
        {
            var statements = GlobalScope.Statements;

            if (statements.Any())
            {
                var result = GlobalScope.Statements[0];
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