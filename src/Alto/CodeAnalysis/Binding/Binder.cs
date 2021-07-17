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
        private readonly FunctionSymbol _function;
        private Stack<(BoundLabel breakLabel, BoundLabel ContinueLabel)> _loopStack = new Stack<(BoundLabel breakLabel, BoundLabel ContinueLabel)>();
        private int _labelCount;
        private BoundScope _scope;

        public Binder(BoundScope parent, FunctionSymbol function)
        {
            _scope = new BoundScope(parent);
            _function = function;

            if (function != null)
            {
                foreach (var p in function.Parameters)
                    _scope.TryDeclareVariable(p);
            }
                
        }

        public static BoundGlobalScope BindGlobalScope(BoundGlobalScope previous, ImmutableArray<SyntaxTree> syntaxTrees)
        {
            var parentScope = CreateParentScope(previous);
            var binder = new Binder(parentScope, null);

            var functionDeclarations = syntaxTrees.SelectMany(t => t.Root.Members).OfType<FunctionDeclarationSyntax>();
            foreach (var function in functionDeclarations)
                binder.BindFunctionDeclaration(function);

            var globalStatements = syntaxTrees.SelectMany(t => t.Root.Members).OfType<GlobalStatementSyntax>();
            var statementBuilder = ImmutableArray.CreateBuilder<BoundStatement>();

            foreach (var globalStatement in globalStatements)
            {
                var st = binder.BindStatement(globalStatement.Statement);
                statementBuilder.Add(st);
            }
            var functions = binder._scope.GetDeclaredFunctions();
            var variables = binder._scope.GetDeclaredVariables();

            var diagnostics = binder.Diagnostics.ToImmutableArray();
            
            if (previous != null)
                diagnostics = diagnostics.InsertRange(0, previous.Diagnostics);

            return new BoundGlobalScope(previous, diagnostics, functions, variables, statementBuilder.ToImmutable());
        }

        public static BoundProgram BindProgram(BoundGlobalScope globalScope)
        {
            var parentScope = CreateParentScope(globalScope);
            var functionBodies = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();
            var diagnostics = new DiagnosticBag();

            var scope = globalScope;
            while (scope != null)
            {
                foreach (var function in scope.Functions)
                {
                    var binder = new Binder(parentScope, function);
                    var body = binder.BindStatement(function.Declaration.Body);
                    var loweredBody = Lowerer.Lower(body);

                    if (function.Type != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                        binder._diagnostics.ReportNotAllCodePathsReturn(function.Declaration.Identifier.Location, function.Name);
                    
                    functionBodies.Add(function, loweredBody);
                    diagnostics.AddRange(binder.Diagnostics);
                }

                scope = scope.Previous;
            }
            
            var statement = Lowerer.Lower(new BoundBlockStatement(globalScope.Statements));
            var program = new BoundProgram(diagnostics, functionBodies.ToImmutable(), statement);
            return program;
        }

        private void BindFunctionDeclaration(FunctionDeclarationSyntax syntax)
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

            var function = new FunctionSymbol(syntax.Identifier.Text, parameters.ToImmutable(), type, syntax);

            if (function.Declaration.Identifier.Text != null && !_scope.TryDeclareFunction(function))
                _diagnostics.ReportSymbolAlreadyDeclared(syntax.Identifier.Location, function.Name);
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

        public DiagnosticBag Diagnostics => _diagnostics;

        private BoundStatement BindStatement(StatementSyntax syntax)
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
                _diagnostics.ReportUnexpectedReturn(syntax.Keyword.Location);
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

        private BoundStatement BindBlockStatement(BlockStatementSyntax syntax)
        {
            var statements = ImmutableArray.CreateBuilder<BoundStatement>();
            _scope = new BoundScope(_scope);

            foreach (var statementSyntax in syntax.Statements)
            {
                var statement = BindStatement(statementSyntax);
                statements.Add(statement);
            }

            _scope = _scope.Parent;

            return new BoundBlockStatement(statements.ToImmutable());
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

            var symbol = _scope.TryLookupSymbol(syntax.Identifier.Text);
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
            
            bool hasErrors = false;
            for (var i = 0; i < syntax.Arguments.Count; i++)
            {
                var parameter = function.Parameters[i];
                var argument = boundArguments[i];

                if (argument.Type != parameter.Type)
                {
                    hasErrors = true;
                    if (argument.Type != TypeSymbol.Error)
                        _diagnostics.ReportWrongArgumentType(syntax.Arguments[i].Location, function.Name, parameter.Name, parameter.Type, argument.Type);
                }
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

            if (hasErrors)
                return new BoundErrorExpression();

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
    }
}