using System;
using System.Collections.Generic;
using System.Linq;
using Alto.CodeAnalysis.Syntax;
using System.Collections.Immutable;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Text;
using Alto.CodeAnalysis.Lowering;
using Alto.CodeAnalysis.Syntax.Preprocessing;
using System.IO;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class Binder
    {
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly bool _isScript;
        private readonly FunctionSymbol _function;
        private Stack<(BoundLabel breakLabel, BoundLabel ContinueLabel)> _loopStack = new Stack<(BoundLabel breakLabel, BoundLabel ContinueLabel)>();
        private Dictionary<string, SyntaxTree> _syntaxTrees = new Dictionary<string, SyntaxTree>();
        private Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> _localFunctions = new Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>>();
        private int _labelCount;
        private BoundScope _scope;

        public Binder(bool isScript, BoundScope parent, FunctionSymbol function)
        {
            _scope = new BoundScope(parent);
            _isScript = isScript;
            _function = function;

            if (function != null)
            {
                foreach (var p in function.Parameters)
                    _scope.TryDeclareVariable(p);
            }
        }

        public Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> LocalFunctions => _localFunctions;
        public DiagnosticBag Diagnostics => _diagnostics;

        public static BoundGlobalScope BindGlobalScope(bool isScript, BoundGlobalScope previous, 
                                                       ImmutableArray<SyntaxTree> syntaxTrees, bool checkCallsiteTrees,
                                                       out Dictionary<BoundScope, List<Tuple<FunctionSymbol, BoundBlockStatement>>> localFunctions)
        {
            var parentScope = CreateParentScope(previous);
            var binder = new Binder(isScript, parentScope, null);

            foreach (var tree in syntaxTrees) 
            {
                // process usings here
                var usingDirectives = tree.Root.Members.OfType<PreprocessorDirective>().Where(d => d.DirectiveKind == DirectiveKind.UsingDirective);
                foreach (var @using in usingDirectives)
                {
                    var name = @using.Identifiers[1].Text;
                    var treesToUse = syntaxTrees.Where(t => Path.GetFileNameWithoutExtension(t.Text.FileName) == name);
                    if (treesToUse.Count() == 0)
                        binder._diagnostics.ReportCannotFindFile(@using.Identifiers[1].Location, name);
                    
                    tree._importedTrees.Add(treesToUse.FirstOrDefault());
                }

                var functionDeclarations = tree.Root.Members.OfType<FunctionDeclarationSyntax>();
                foreach (var function in functionDeclarations)
                    binder.BindFunctionDeclaration(function, tree);
            }

            var globalStatements = syntaxTrees.SelectMany(t => t.Root.Members).OfType<GlobalStatementSyntax>();
            var firstGlobalStatementPerSyntaxTree = syntaxTrees.Select(t => t.Root.Members.OfType<GlobalStatementSyntax>().FirstOrDefault())
                                                               .Where(s => s != null)
                                                               .ToArray();  
    
            var statementBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (var globalStatement in globalStatements)
            {
                var st = binder.BindGlobalStatement(globalStatement.Statement);
                statementBuilder.Add(st);
            }

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


            var diagnostics = binder.Diagnostics.ToImmutableArray();
            
            var variables = binder._scope.GetDeclaredVariables();

            if (previous != null)
                diagnostics = diagnostics.InsertRange(0, previous.Diagnostics);

            localFunctions = binder._localFunctions;
            return new BoundGlobalScope(previous, diagnostics, mainFunction, scriptFunction, functions, variables, 
                                        statementBuilder.ToImmutable());
        }

        public static BoundProgram BindProgram(bool isScript, BoundProgram previous, BoundGlobalScope globalScope)
        {
            var parentScope = CreateParentScope(globalScope);
            var functionBodies = new Dictionary<FunctionSymbol, BoundBlockStatement>();
            var diagnostics = new DiagnosticBag();

            foreach (var function in globalScope.Functions)
            {
                var binder = new Binder(isScript, parentScope, function);

                var body = binder.BindGlobalStatement(function.Declaration.Body);
                var loweredBody = Lowerer.Lower(function, body);

                if (function.Type != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                    binder._diagnostics.ReportNotAllCodePathsReturn(function.Declaration.Identifier.Location, function.Name);

                functionBodies.Add(function, loweredBody);
                diagnostics.AddRange(binder.Diagnostics);
            }
            
            
            if (globalScope.MainFunction != null && globalScope.Statements.Any())
            {
                var body = Lowerer.Lower(globalScope.MainFunction, new BoundBlockStatement(globalScope.Statements));
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

                var body = Lowerer.Lower(globalScope.ScriptFunction, new BoundBlockStatement(statements));
                functionBodies.Add(globalScope.ScriptFunction, body);
            }

            var program = new BoundProgram(previous, diagnostics, globalScope.MainFunction, globalScope.ScriptFunction, functionBodies.ToImmutableDictionary());
            return program;
        }

        private FunctionSymbol BindFunctionDeclaration(FunctionDeclarationSyntax syntax, SyntaxTree tree, bool declare = true)
        {
            var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
            var seenParameterNames = new HashSet<string>();

            for (int i = 0; i < syntax.Parameters.Count; i++)
            {
                var parameterSyntax = syntax.Parameters[i];
                
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
                    
                    var parameter = new ParameterSymbol(paramName, paramType, i, isOptional, optionalExpression);
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
                    _diagnostics.ReportUnexpectedGlobalReturnWithExpression(syntax.ReturnExpression.Location);
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

                Binder binder = new Binder(false, _scope, funcSymbol);

                // TODO: Also have to check for duplicate names
                if (!LocalFunctionNameIsUnique(funcSymbol))
                    _diagnostics.ReportSymbolAlreadyDeclared(function.Identifier.Location, funcSymbol.Name);
                
                var body = binder.BindBlockStatement(function.Body, funcSymbol.Parameters);
                var loweredBody = Lowerer.Lower(funcSymbol, body);

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

            Symbol symbol = LookupFunction(syntax.Identifier.Text);
            if (symbol == null)
            {
                _diagnostics.ReportUndefinedFunction(syntax.Identifier.Location, syntax.Identifier.Text);
                return new BoundErrorExpression();
            }
            else if (!SymbolIsImported(syntax.SyntaxTree, symbol))
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
            var tree = identifier.SyntaxTree;
            var declare = !identifier.IsMissing;
            var variable = _function == null ? (VariableSymbol) new GlobalVariableSymbol(name, isReadOnly, type, tree) : (VariableSymbol) new LocalVariableSymbol(name, isReadOnly, type, tree);

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
            foreach (var (scope, functions) in _localFunctions)
                if (functions.Where(f => f.Item1.Name == function.Name).Count() > 0)
                    return false;

            return true;
        }

        /// <summary>
        /// Checks whether a symbol is imported.
        /// </summary>
        /// <param name="tree">The tree we're checking from.</param>
        /// <param name="symbol">The symbol to check if it's imported.</param>
        private bool SymbolIsImported(SyntaxTree tree, Symbol symbol)
        {
            // If symbol.Tree is null, it means it's a built-in function
            if (_isScript ||
                symbol.Tree == null ||
                tree == symbol.Tree ||
                tree._importedTrees.Contains(symbol.Tree))
            {
                return true;
            }

            return false;
        }
    }
}