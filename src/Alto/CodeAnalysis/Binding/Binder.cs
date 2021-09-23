using System;
using System.Collections.Generic;
using System.Linq;
using Alto.CodeAnalysis.Syntax;
using System.Collections.Immutable;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Text;
using Alto.CodeAnalysis.Lowering;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class Binder
    {
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly bool _isScript;
        private readonly FunctionSymbol _function;
        private Stack<(BoundLabel breakLabel, BoundLabel ContinueLabel)> _loopStack = new Stack<(BoundLabel breakLabel, BoundLabel ContinueLabel)>();
        private Dictionary<string, SyntaxTree> _syntaxTrees = new Dictionary<string, SyntaxTree>();
        private Dictionary<SyntaxTree, IEnumerable<SyntaxTree>> _importedTrees = new Dictionary<SyntaxTree, IEnumerable<SyntaxTree>>();
        private Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> _localFunctions = new Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>>();
        private int _labelCount;
        private BoundScope _scope;

        public Binder(bool isScript, BoundScope parent, FunctionSymbol function, bool checkCallsiteTrees = true)
        {
            _scope = new BoundScope(parent);
            _isScript = isScript;
            _function = function;

            CheckCallsiteTrees = checkCallsiteTrees;

            if (function != null)
            {
                foreach (var p in function.Parameters)
                    _scope.TryDeclareVariable(p);
            }
        }

        private Binder(bool isScript, BoundScope parent, FunctionSymbol function, bool checkCallsiteTrees = true, 
                       Dictionary<SyntaxTree, IEnumerable<SyntaxTree>> importedTrees = null)
            : this(isScript, parent, function, checkCallsiteTrees)
        {
                if (importedTrees != null)
                    _importedTrees = importedTrees;
        }

        /// <summary>
        /// Gets the value that indicated whether to check function call-sites are in the same syntax tree or if they're imported.
        /// This is set to false in the interactive experience.
        /// </summary>
        private bool CheckCallsiteTrees { get; }
        public Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> LocalFunctions => _localFunctions;
        public DiagnosticBag Diagnostics => _diagnostics;

        public static BoundGlobalScope BindGlobalScope(bool isScript, BoundGlobalScope previous, SyntaxTree coreSyntax, 
                                                       ImmutableArray<SyntaxTree> syntaxTrees, bool checkCallsiteTrees,
                                                       out Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> localFunctions)
        {
            var parentScope = CreateParentScope(previous);
            var binder = new Binder(isScript, parentScope, null, checkCallsiteTrees);

            { // add the core syntax tree to the syntax tree arr
                var trees = syntaxTrees.ToList();
                trees.Add(coreSyntax);
                syntaxTrees = trees.ToImmutableArray();
            }

            // load the syntax trees (this is used for imports)
            foreach (var tree in syntaxTrees)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(tree.Text.FileName);
                binder._syntaxTrees.Add(name, tree);
            }

            // automatically import the 0th syntax tree
            var coreSyntaxes = new SyntaxTree[1] { coreSyntax };
            foreach (var tree in syntaxTrees)
                binder._importedTrees.Add(tree, coreSyntaxes);

            foreach (var tree in syntaxTrees) 
            {
                var functionDeclarations = tree.Root.Members.OfType<FunctionDeclarationSyntax>();
                foreach (var function in functionDeclarations)
                    binder.BindFunctionDeclaration(function, tree);
            }

            var globalStatements = syntaxTrees.SelectMany(t => t.Root.Members).OfType<GlobalStatementSyntax>();
            var firstGlobalStatementPerSyntaxTree = syntaxTrees.Select(t => t.Root.Members.OfType<GlobalStatementSyntax>().FirstOrDefault())
                                                               .Where(s => s != null)
                                                               .ToArray();

            if (firstGlobalStatementPerSyntaxTree.Length > 1)
            {
                foreach (var globalStatement in firstGlobalStatementPerSyntaxTree)
                    binder.Diagnostics.ReportOnlyOneFileCanContainGlobalStatements(globalStatement.Location);
            }
            
            var functions = binder._scope.GetDeclaredFunctions();

            FunctionSymbol mainFunction;
            FunctionSymbol scriptFunction;

            if (isScript)
            {
                mainFunction = null;
                if (globalStatements.Any())
                    scriptFunction = new FunctionSymbol("$eval", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.Any);
                else
                    scriptFunction = null;
            }
            else
            {
                mainFunction = functions.FirstOrDefault(f => f.Name == "main");
                scriptFunction = null;

                if (mainFunction != null)
                {
                    if (mainFunction.Type != TypeSymbol.Void || mainFunction.Parameters.Any())
                        binder.Diagnostics.ReportMainIncorrectSignature(mainFunction.Declaration.Identifier.Location);
                }

                if (globalStatements.Any())
                {   
                    if (mainFunction != null)
                    {
                        binder.Diagnostics.ReportCannotMixMainFunctionAndGlobalStatements(mainFunction.Declaration.Identifier.Location);

                        foreach (var globalStatement in globalStatements)
                            binder.Diagnostics.ReportCannotMixMainFunctionAndGlobalStatements(globalStatement.Location);
                    }
                    else
                    {
                        mainFunction = new FunctionSymbol("main", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.Void);
                    }
                }
            }

            var statementBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            var globalStatementFunction = mainFunction ?? scriptFunction;
            if (globalStatementFunction != null)
            {
                var statementBinder = new Binder(isScript, parentScope, globalStatementFunction, checkCallsiteTrees);

                foreach (var globalStatement in globalStatements)
                {
                    var st = binder.BindGlobalStatement(globalStatement.Statement);
                    statementBuilder.Add(st);
                }
            }

            var diagnostics = binder.Diagnostics.ToList();
            
            var variables = binder._scope.GetDeclaredVariables();

            if (previous != null)
                diagnostics.InsertRange(0, previous.Diagnostics);

            localFunctions = binder._localFunctions;
            return new BoundGlobalScope(previous, diagnostics.ToImmutableArray(), mainFunction, scriptFunction, functions, variables, 
                                        statementBuilder.ToImmutable(), binder._importedTrees);
        }

        public static BoundProgram BindProgram(bool isScript, BoundProgram previous, BoundGlobalScope globalScope)
        {
            var parentScope = CreateParentScope(globalScope);
            var functionBodies = new Dictionary<FunctionSymbol, BoundBlockStatement>();
            var diagnostics = new DiagnosticBag();

            foreach (var function in globalScope.Functions)
            {
                // if we're getting 'missing import' errors, this is where we've gone wrong... in checkCallSiteTrees: true
                var binder = new Binder(isScript, parentScope, function, checkCallsiteTrees: true, globalScope.ImportedTrees);

                var body = binder.BindGlobalStatement(function.Declaration.Body);
                var loweredBody = Lowerer.Lower(body);

                if (function.Type != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                    binder._diagnostics.ReportNotAllCodePathsReturn(function.Declaration.Identifier.Location, function.Name);

                functionBodies.Add(function, loweredBody);
                diagnostics.AddRange(binder.Diagnostics);
            }
            
            
            if (globalScope.MainFunction != null && globalScope.Statements.Any())
            {
                var body = Lowerer.Lower(new BoundBlockStatement(globalScope.Statements));
                functionBodies.Add(globalScope.MainFunction, body);
            }
            else if (globalScope.ScriptFunction != null)
            {
                var statements = globalScope.Statements;
                if (statements.Length == 1 && statements[0] is BoundExpressionStatement ex && ex.Expression.Type != TypeSymbol.Void)
                {
                    statements = statements.SetItem(0, new BoundReturnStatement(ex.Expression));
                }
                else if (statements.Any() && statements.Last().Kind != BoundNodeKind.ReturnStatement)
                {
                    var nullValue = new BoundLiteralExpression("");
                    statements = statements.Add(new BoundReturnStatement(nullValue));
                }

                var body = Lowerer.Lower(new BoundBlockStatement(statements));
                functionBodies.Add(globalScope.ScriptFunction, body);
            }

            var program = new BoundProgram(previous, diagnostics, globalScope.MainFunction, globalScope.ScriptFunction, functionBodies.ToImmutableDictionary());
            return program; 
        }

        private FunctionSymbol BindFunctionDeclaration(FunctionDeclarationSyntax syntax, SyntaxTree tree, bool declare = true)
        {
            var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
            var seenParameterNames = new HashSet<string>();

            foreach (var parameterSyntax in syntax.Parameters)
            {
                var paramName = parameterSyntax.Identifier.Text;
                var paramType = BindTypeClause(parameterSyntax.Type);
                var isOptional = parameterSyntax.IsOptional;
                var optionalUnboundExpression = parameterSyntax.OptionalExpression;

                if (!seenParameterNames.Add(paramName))
                {
                    _diagnostics.ReportParameterAlreadyDeclared(paramName, parameterSyntax.Location);
                }
                else
                {
                    BoundExpression optionalExpression = null;
                    if (optionalUnboundExpression != null)
                    {
                        var expression = BindExpression(optionalUnboundExpression);
                        optionalExpression = BindConversion(expression, paramType, parameterSyntax.OptionalExpression.Location);
                    }
                    
                    var parameter = new ParameterSymbol(paramName, paramType, isOptional, optionalExpression);
                    parameters.Add(parameter);
                }
            }

            var type = BindTypeClause(syntax.Type) ?? TypeSymbol.Void;
            var function = new FunctionSymbol(syntax.Identifier.Text, parameters.ToImmutable(), type, syntax, tree);

            if (declare)
            {
                var sucess = _scope.TryDeclareFunction(function);
                if (function.Declaration.Identifier.Text != null && !sucess)
                    _diagnostics.ReportSymbolAlreadyDeclared(syntax.Identifier.Location, function.Name);
            }

            return function;
        }

        private static BoundScope CreateParentScope(BoundGlobalScope previous)
        {
            var stack = new Stack<BoundGlobalScope>();
            while (previous != null)
            {
                stack.Push(previous);
                previous = previous.Previous;
            }
            BoundScope parent = CreateRootScope();

            while (stack.Count > 0)
            {
                previous = stack.Pop();
                var scope = new BoundScope(parent);

                foreach (var f in previous.Functions)
                    scope.TryDeclareFunction(f);
                
                foreach (var v in previous.Variables)
                    scope.TryDeclareVariable(v);

                parent = scope;
            }

            return parent;
        }

        private static BoundScope CreateRootScope()
        {
            var result = new BoundScope(null);

            foreach (var function in BuiltInFunctions.GetAll())
                result.TryDeclareFunction(function);
            
            return result;
        }

        private BoundStatement BindGlobalStatement(StatementSyntax syntax)
        {
            return BindStatement(syntax, true);
        }

        private BoundStatement BindStatement(StatementSyntax syntax, bool isGlobal = false)
        {
            var result = BindStatementInternal(syntax);
            
            if (!_isScript || !isGlobal)
            {
                if (result is BoundExpressionStatement e)
                {
                    var isAllowed = e.Expression.Kind == BoundNodeKind.ErrorExpression ||
                                    e.Expression.Kind == BoundNodeKind.AssignmentExpression ||
                                    e.Expression.Kind == BoundNodeKind.CallExpression;

                    if (!isAllowed)
                        _diagnostics.ReportInvalidExpressionStatement(syntax.Location);
                }
            }

            return result;
        }

        private BoundStatement BindStatementInternal(StatementSyntax syntax)
        {
            switch (syntax.Kind)
            {
                case SyntaxKind.BlockStatement:
                    return BindBlockStatement((BlockStatementSyntax) syntax);
                case SyntaxKind.ExpressionStatement:
                    return BindExpressionStatement((ExpressionStatementSyntax) syntax);
                case SyntaxKind.VariableDeclaration:
                    return BindVariable((VariableDeclarationSyntax) syntax);
                case SyntaxKind.IfStatement:
                    return BindIfStatement((IfStatementSyntax) syntax);
                case SyntaxKind.WhileStatement:
                    return BindWhileStatement((WhileStatementSyntax) syntax);
                case SyntaxKind.DoWhileStatement:
                    return BindDoWhileStatement((DoWhileStatementSyntax) syntax);
                case SyntaxKind.ForStatement:
                    return BindForStatement((ForStatementSyntax) syntax);
                case SyntaxKind.BreakStatement:
                    return BindBreakStatement((BreakStatementSyntax) syntax);
                case SyntaxKind.ContinueStatement:
                    return BindContinueStatement((ContinueStatementSyntax) syntax);
                case SyntaxKind.ReturnStatement:
                    return BindReturnStatement((ReturnStatementSyntax) syntax);
                case SyntaxKind.ImportStatement:
                    return BindImportStatement((ImportStatementSyntax) syntax);
                default:
                    throw new Exception($"Unexpected syntax {syntax.Kind}");
            }
        }

        private TypeSymbol BindTypeClause(TypeClauseSyntax syntax)
        {
            if (syntax == null)
                return null;
            
            var type = LookupType(syntax.Identifier.Text);
            
            if (type == null)
                _diagnostics.ReportUndefinedType(syntax.Identifier.Location, syntax.Identifier.Text);

            return type;
        }

        private BoundStatement BindIfStatement(IfStatementSyntax syntax)
        {
            var condition = BindExpression(syntax.Condition, TypeSymbol.Bool);
            var thenStatement = BindStatement(syntax.ThenStatement);
            var elseStatement = syntax.ElseClause == null ? null : BindStatement(syntax.ElseClause.ElseStatement);
            return new BoundIfStatement(condition, thenStatement, elseStatement);
        }

        private BoundStatement BindWhileStatement(WhileStatementSyntax syntax)
        {
            var condition = BindExpression(syntax.Condition, TypeSymbol.Bool);
            BoundStatement body = BindLoopBody(syntax.Body, out var breakLabel, out var continueLabel);
            return new BoundWhileStatement(condition, body, breakLabel, continueLabel);
        }

        private BoundStatement BindDoWhileStatement(DoWhileStatementSyntax syntax)
        {
            var body = BindLoopBody(syntax.Body, out var breakLabel, out var continueLabel);
            var condition = BindExpression(syntax.Condition);

            return new BoundDoWhileStatement(body, condition, breakLabel, continueLabel);
        }

        private BoundStatement BindForStatement(ForStatementSyntax syntax)
        {
            var lowerBound = BindExpression(syntax.LowerBound, TypeSymbol.Int);
            var upperBound = BindExpression(syntax.UpperBound, TypeSymbol.Int);

            _scope = new BoundScope(_scope);

            var variable = BindVariableDeclaration(syntax.Identifier, true, TypeSymbol.Int);

            var body = BindLoopBody(syntax.Body, out var breakLabel, out var continueLabel);

            _scope = _scope.Parent;

            return new BoundForStatement(variable, lowerBound, upperBound, body, breakLabel, continueLabel);
        }

        
        private BoundStatement BindLoopBody(StatementSyntax syntax, out BoundLabel breakLabel, out BoundLabel continueLabel)
        {
            _labelCount++;
            breakLabel = GenerateLabel("break", _labelCount, false);
            continueLabel = GenerateLabel("continue", _labelCount, false);
            _labelCount++;

            _loopStack.Push((breakLabel, continueLabel));
            var boundBody = BindStatement(syntax);
            _loopStack.Pop();

            return boundBody;
        }

        private BoundStatement BindBreakStatement(BreakStatementSyntax syntax)
        {
            if (_loopStack.Count == 0)
            {
                _diagnostics.ReportInvalidBreakOrContinue(syntax.Keyword.Location, syntax.Keyword.Text);
                return BindErrorStatement();
            }

            var breakLabel = _loopStack.Peek().breakLabel;
            return new BoundGotoStatement(breakLabel);
        }

        private BoundStatement BindContinueStatement(ContinueStatementSyntax syntax)
        {
            if (_loopStack.Count == 0)
            {
                _diagnostics.ReportInvalidBreakOrContinue(syntax.Keyword.Location, syntax.Keyword.Text);
                return BindErrorStatement();
            }

            var continueLabel = _loopStack.Peek().ContinueLabel;
            return new BoundGotoStatement(continueLabel);
        }

        private BoundStatement BindReturnStatement(ReturnStatementSyntax syntax)
        {
            var expression = syntax.ReturnExpression == null ? null : BindExpression(syntax.ReturnExpression);

            if (_function == null)
            {
                if (_isScript)
                {
                    // Allow for "blank" returns
                    if (expression == null)
                        expression = new BoundLiteralExpression("");
                }
                else if (expression != null)
                {
                    // Main is always of type void
                    _diagnostics.ReportUnexpectedReturnExpression(syntax.ReturnExpression.Location, _function.Type, _function.Name);
                }
            }
            else
            {
                if (_function.Type == TypeSymbol.Void)
                {
                    if (expression != null)
                        _diagnostics.ReportUnexpectedReturnExpression(syntax.ReturnExpression.Location, expression.Type, _function.Name);
                }
                else
                {
                    if (expression == null)
                        _diagnostics.ReportReturnExpectsAnExpression(syntax.Keyword.Location, _function.Name);
                    else
                        expression = BindConversion(expression, _function.Type, syntax.ReturnExpression.Location); 
                }
            }
            
            return new BoundReturnStatement(expression);
        }

        private BoundStatement BindImportStatement(ImportStatementSyntax syntax)
        {
            var name = syntax.Identifier.Text;

            SyntaxTree importTree = null;
            if (_syntaxTrees.ContainsKey(name))
                importTree = _syntaxTrees[name];
            else
                _diagnostics.ReportCannotFindImportFile(syntax.Location, name);

            if (_importedTrees.ContainsKey(syntax.SyntaxTree))
            {
                var imports = _importedTrees[syntax.SyntaxTree].ToList();
                imports.Add(importTree);

                _importedTrees.Remove(syntax.SyntaxTree);
                _importedTrees.Add(syntax.SyntaxTree, imports);
            }
            else
            {
                var syntaxTrees = new SyntaxTree[] { importTree };
                _importedTrees.Add(syntax.SyntaxTree, syntaxTrees);
            }
            
            
            return new BoundImportStatement(importTree, name);
        }

        private BoundStatement BindBlockStatement(BlockStatementSyntax syntax, IEnumerable<VariableSymbol> declareAdditionalVariables = null)
        {
            var statements = ImmutableArray.CreateBuilder<BoundStatement>();
            _scope = new BoundScope(_scope);

            if (declareAdditionalVariables != null)
                foreach (var v in declareAdditionalVariables)
                    _scope.TryDeclareVariable(v);
            
            foreach (var function in syntax.Functions)
            {
                var funcSymbol = BindFunctionDeclaration(function, syntax.SyntaxTree, declare: false);

                Binder binder = new Binder(false, _scope, funcSymbol, CheckCallsiteTrees);

                // TODO: Also have to check for duplicate names
                if (!LocalFunctionNameIsUnique(funcSymbol))
                    _diagnostics.ReportSymbolAlreadyDeclared(function.Identifier.Location, funcSymbol.Name);
                
                var body = binder.BindBlockStatement(function.Body, funcSymbol.Parameters);
                var loweredBody = Lowerer.Lower(body);

                if (funcSymbol.Type != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                    _diagnostics.ReportNotAllCodePathsReturn(function.Identifier.Location, funcSymbol.Name);
                
                if (!_localFunctions.ContainsKey(_scope))
                {
                    var funcs = new List<Tuple<FunctionSymbol, BoundBlockStatement>>();
                    funcs.Add(new Tuple<FunctionSymbol, BoundBlockStatement>(funcSymbol, loweredBody));
                    _localFunctions.Add(_scope, funcs);
                }
                else
                {
                    _localFunctions[_scope].Add(new Tuple<FunctionSymbol, BoundBlockStatement>(funcSymbol, loweredBody));
                }
            }

            foreach (var statementSyntax in syntax.Statements)
            {
                var statement = BindStatement(statementSyntax);
                statements.Add(statement);
            }

            var blockStatement = new BoundBlockStatement(statements.ToImmutable());

            _scope = _scope.Parent;
            return blockStatement;
        }
        
        private BoundStatement BindExpressionStatement(ExpressionStatementSyntax syntax)
        {
            var expression = BindExpression(syntax.Expression, canBeVoid: true);
            return new BoundExpressionStatement(expression);
        }

        private BoundExpression BindExpression(ExpressionSyntax syntax, bool canBeVoid = false)
        {
            var result = BindExpressionInternal(syntax);

            if (!canBeVoid && result.Type == TypeSymbol.Void)
            {
                _diagnostics.ReportExpressionMustHaveAValue(syntax.Location);
                return new BoundErrorExpression();
            }

            return result;
        }

        private BoundExpression BindExpression(ExpressionSyntax syntax, TypeSymbol targetType)
        {
            return BindConversion(syntax, targetType);
        }

        private BoundExpression BindExpressionInternal(ExpressionSyntax syntax)
        {
            switch (syntax.Kind)
            {
                case SyntaxKind.ParenthesizedExpression:
                    return BindParenthesizedExpression((ParenthesizedExpressionSyntax)syntax);
                case SyntaxKind.LiteralExpression:
                    return BindLiteralExpression((LiteralExpressionSyntax)syntax);
                case SyntaxKind.UnaryExpression:
                    return BindUnaryExpression((UnaryExpressionSyntax)syntax);
                case SyntaxKind.BinaryExpression:
                    return BindBinaryExpression((BinaryExpressionSyntax)syntax);
                case SyntaxKind.NameExpression:
                    return BindNameExpression((NameExpressionSyntax)syntax);
                case SyntaxKind.AssignmentExpression:
                    return BindAssignmentExpression((AssignmentExpressionSyntax)syntax);
                case SyntaxKind.CallExpression:
                    return BindCallExpression((CallExpressionSyntax)syntax);
                default:
                    throw new Exception($"Unexpected syntax {syntax.Kind}");
            }
        }
 
        private BoundExpression BindConversion(ExpressionSyntax syntax, TypeSymbol type, bool allowExplicit = false)
        {
            var expression = BindExpression(syntax);
            var location = syntax.Location;

            return BindConversion(expression, type, location, allowExplicit);
        }

        private BoundExpression BindConversion(BoundExpression expression, TypeSymbol type, TextLocation location, bool allowExplicit = false)
        {
            var conversion = Conversion.Classify(expression.Type, type);

            if (!conversion.Exists)
            {
                if (expression.Type != TypeSymbol.Error && type != TypeSymbol.Error)
                {
                    _diagnostics.ReportCannotConvert(location, expression.Type, type);
                }

                return new BoundErrorExpression();
            }

            if (conversion.IsExplicit && !allowExplicit)
            {
                _diagnostics.ReportCannotImplicitlyConvert(location, expression.Type, type);
                return new BoundErrorExpression();
            }

            if (conversion.IsIdentity)
                return expression;

            return new BoundConversionExpression(type, expression);
        }

        private BoundExpression BindNameExpression(NameExpressionSyntax syntax) 
        {
            var name = syntax.IdentifierToken.Text;
 
            if (syntax.IdentifierToken.IsMissing)
                return new BoundErrorExpression();
                
            var variable = BindVariableReference(syntax.IdentifierToken);
            if (variable == null)
                return new BoundErrorExpression();

            return new BoundVariableExpression(variable);
        }

        private BoundExpression BindAssignmentExpression(AssignmentExpressionSyntax syntax)
        {
            var name = syntax.IdentifierToken.Text;
            var boundExpression = BindExpression(syntax.Expression);

            var variable = BindVariableReference(syntax.IdentifierToken);
            if (variable == null)
                return boundExpression;

            if (variable.IsReadOnly)
                _diagnostics.ReportCannotAssign(syntax.AssignmentToken.Location, name);

            var convertedExpression = BindConversion(boundExpression, variable.Type, syntax.Expression.Location);

            return new BoundAssignmentExpression(variable, convertedExpression);
        }

        private BoundExpression BindLiteralExpression(LiteralExpressionSyntax syntax)
        {
            var value = syntax.Value ?? 0;
            return new BoundLiteralExpression(value);
        }

        private BoundExpression BindUnaryExpression(UnaryExpressionSyntax syntax)
        {
            var boundOperand = BindExpression(syntax.Operand);

            if (boundOperand.Type == TypeSymbol.Error)
                return new BoundErrorExpression();
            
            var boundOperator = BoundUnaryOperator.Bind(syntax.OperatorToken.Kind, boundOperand.Type);
            if (boundOperator == null)
            {
                _diagnostics.ReportUndefinedUnaryOperator(syntax.OperatorToken.Location, syntax.OperatorToken.Text, boundOperand.Type);
                return new BoundErrorExpression();
            }

            return new BoundUnaryExpression(boundOperator, boundOperand);
        }
        
        private BoundExpression BindBinaryExpression(BinaryExpressionSyntax syntax)
        {       
            var boundLeft = BindExpression(syntax.Left);
            var boundRight = BindExpression(syntax.Right);

            if (boundLeft.Type == TypeSymbol.Error || boundRight.Type == TypeSymbol.Error)
                return new BoundErrorExpression();

            var boundOperator = BoundBinaryOperator.Bind(syntax.OperatorToken.Kind, boundLeft.Type, boundRight.Type);
            if (boundOperator == null)
            {
                _diagnostics.ReportUndefinedBinaryOperator(syntax.OperatorToken.Location, syntax.OperatorToken.Text, boundLeft.Type, boundRight.Type);
                return new BoundErrorExpression();
            }

            return new BoundBinaryExpression(boundLeft, boundOperator, boundRight);
        }

        private BoundExpression BindCallExpression(CallExpressionSyntax syntax)
        {
            if (syntax.Arguments.Count == 1 && LookupTypeConversion(syntax.Identifier.Text) is TypeSymbol type)
                return BindConversion(syntax.Arguments[0], type, allowExplicit: true);

            var boundArguments = ImmutableArray.CreateBuilder<BoundExpression>();

            foreach (var argument in syntax.Arguments)
            {
                var boundArgument = BindExpression(argument);
                boundArguments.Add(boundArgument);
            }

            var symbol = LookupFunction(syntax.Identifier.Text);
            if (symbol == null)
            {
                _diagnostics.ReportUndefinedFunction(syntax.Identifier.Location, syntax.Identifier.Text);
                return new BoundErrorExpression();
            }

            var function = symbol as FunctionSymbol;
            if (function == null)
            {
                _diagnostics.ReportNotAFunction(syntax.Identifier.Location, syntax.Identifier.Text);
                return new BoundErrorExpression();
            }
            
            // if function.Tree == null, it's a built-in function
            if (CheckCallsiteTrees && function.Tree != null && !IsImported(syntax.SyntaxTree, function.Tree))
            {
                _diagnostics.MissingImportStatement(syntax.Identifier.Location, function.Name, function.Tree.Root.Location.FileName);
                return new BoundErrorExpression();
            }

            var parameterCount = function.Parameters.Length; 
            var nonOptionalParametersCount = (from p in function.Parameters
                                  where p.IsOptional == false
                                  select p).Count();
            
            if ( (syntax.Arguments.Count >= nonOptionalParametersCount && 
                syntax.Arguments.Count <= parameterCount) == false) // this only runs when we have a wrong arg count, that's why `== false`
            {
                TextSpan span;
                if (parameterCount < syntax.Arguments.Count)
                {
                    SyntaxNode firstExceedingNode;
                    if (parameterCount > 0)
                        firstExceedingNode = syntax.Arguments.GetSeparator(parameterCount - 1);
                    else
                        firstExceedingNode = syntax.Arguments[0];
                    
                    var lastExceedingNode = syntax.Arguments[syntax.Arguments.Count - 1];
                    span = TextSpan.FromBounds(firstExceedingNode.Span.Start, lastExceedingNode.Span.End);
                }
                else
                {
                    span = syntax.ClosedParenthesisToken.Span;
                }

                var location = new TextLocation(syntax.SyntaxTree.Text, span);
                _diagnostics.ReportWrongArgumentCount(location, function.Name, parameterCount, syntax.Arguments.Count);
                return new BoundErrorExpression();
            }       
            
            for (var i = 0; i < syntax.Arguments.Count; i++)
            {
                var argumentLocation = syntax.Arguments[i].Location;
                var parameter = function.Parameters[i];
                var argument = boundArguments[i];
                boundArguments[i] = BindConversion(argument, parameter.Type, argumentLocation);
            }

            for (var i = syntax.Arguments.Count(); i < function.Parameters.Count(); i++)
            {
                var parameter = function.Parameters[i];
                if (parameter.OptionalValue != null && parameter.IsOptional)
                {
                    // add the v to the args
                    var v = parameter.OptionalValue;
                    boundArguments.Add(v);
                }
            }

            return new BoundCallExpression(function, boundArguments.ToImmutable());
        }
    
        private BoundExpression BindParenthesizedExpression(ParenthesizedExpressionSyntax syntax)
        {
            return BindExpression(syntax.Expression);
        }
        
        private VariableSymbol BindVariableDeclaration(SyntaxToken identifier, bool isReadOnly, TypeSymbol type)
        {
            var name = identifier.Text ?? "?";
            var declare = !identifier.IsMissing;
            var variable = _function == null ? (VariableSymbol) new GlobalVariableSymbol(name, isReadOnly, type) : (VariableSymbol) new LocalVariableSymbol(name, isReadOnly, type);

            if (declare && !_scope.TryDeclareVariable(variable))
                _diagnostics.ReportSymbolAlreadyDeclared(identifier.Location, name);
            
            return variable;
        }

        private BoundStatement BindVariable(VariableDeclarationSyntax syntax)
        {
            var isReadOnly = syntax.Keyword.Kind == SyntaxKind.LetKeyword;
            var type = BindTypeClause(syntax.TypeClause);
            var initializer = BindExpression(syntax.Initializer);
            var variableType = type ?? initializer.Type;
            var castedInitializer = BindConversion(initializer, variableType, syntax.Initializer.Location);
            var variable = BindVariableDeclaration(syntax.Identifier, isReadOnly, variableType);

            return new BoundVariableDeclaration(variable, castedInitializer);
        }

        private VariableSymbol BindVariableReference(SyntaxToken identifier)
        {
            var name = identifier.Text;
            var location = identifier.Location;

            switch (_scope.TryLookupSymbol(name))
            {
                case VariableSymbol variable:
                    return variable;

                case null:
                    _diagnostics.ReportUndefinedVariable(location, name);
                    return null;

                default:
                    _diagnostics.ReportNotAVariable(location, name);
                    return null;
            }
        }

        private BoundStatement BindErrorStatement()
        {
            var statement = new BoundExpressionStatement(new BoundErrorExpression());
            return statement;
        }

        private TypeSymbol LookupType(string name) 
        {
            switch (name)
            {
                case "any":
                    return TypeSymbol.Any;
                case "bool":
                    return TypeSymbol.Bool;
                case "string":
                    return TypeSymbol.String;
                case "int":
                    return TypeSymbol.Int;
            }

            return null;
        }

        private TypeSymbol LookupTypeConversion(string name)
        {
            switch (name)
            {
                case "toany":
                    return TypeSymbol.Any;
                case "tobool":
                    return TypeSymbol.Bool;
                case "tostring":
                    return TypeSymbol.String;
                case "toint":
                    return TypeSymbol.Int;
            }

            return null;
        }

        private BoundLabel GenerateLabel(string prefix = "Label", int? count = null, bool incrementCount = true)
        {
            if (incrementCount)
                _labelCount++;
            
            var nameCount = count == null ? _labelCount : count; 
            var name = prefix + nameCount.ToString();
            
            return new BoundLabel(name);
        }

        private bool IsImported(SyntaxTree currentTree, SyntaxTree tree) 
        {   
            if (currentTree == tree)
                return true;
            
            if (!_importedTrees.ContainsKey(currentTree))
                return false;
            
            var importedTrees = _importedTrees[currentTree];
            foreach (var importedTree in importedTrees)
                if (importedTree.Text.FileName == tree.Text.FileName)
                    return true;
            
            return false;
        }

        private Symbol LookupFunction(string name)
        {
            // search through local functions
            var symbol = _scope.TryLookupSymbol(name);
            if (symbol == null)
            {
                var scope = _scope;
                while (scope != null)
                {
                    if (_localFunctions.ContainsKey(scope))
                    {
                        foreach (var body in _localFunctions[scope])
                        {
                            if (body.Item1.Name == name)
                            {
                                symbol = body.Item1;
                                return symbol;
                            }
                        }
                    }
                    scope = scope.Parent;
                }
            }

            return symbol;
        }

        private bool LocalFunctionNameIsUnique(FunctionSymbol function)
        {
            foreach (var localScope in _localFunctions)
                foreach (var func in localScope.Value)
                    if (func.Item1.Name == function.Name)
                        return false;

            return true;
        }
    }
}