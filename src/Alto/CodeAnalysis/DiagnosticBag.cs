using System;
using System.Collections;
using System.Collections.Generic;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;
using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis
{
    public sealed class DiagnosticBag : IEnumerable<Diagnostic>
    {
        private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();

        public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void AddRange(DiagnosticBag diagnostics)
        {
            _diagnostics.AddRange(diagnostics._diagnostics);
        }

        public void Concat(DiagnosticBag diagnostics)
        {
            _diagnostics.AddRange(diagnostics._diagnostics);
        }

        private void Report(TextLocation location, string message)
        {
            var diagnostic = new Diagnostic(location, message);
            _diagnostics.Add(diagnostic);
        }

        public void ReportInvalidNumber(TextLocation location, string text, TypeSymbol type)
        {
            var message = $"The number '{text}' isn't a valid '{type.ToString()}'";
            Report(location, message);
        }

        public void ReportBadCharacter(TextLocation location, char character)
        {
            var message = $"Bad character in input: '{character}'";
            Report(location, message);
        }

        public void ReportUnexpectedToken(TextLocation location, SyntaxKind kind, SyntaxKind expectedKind)
        {
            var message = $"Unexpected token <{kind}>, expected <{expectedKind}>.";
            Report(location, message);
        }

        public void ReportUndefinedUnaryOperator(TextLocation location, string operatorText, TypeSymbol operandType)
        {
            var message = $"Unary operator '{operatorText}' is not defined for type '{operandType.ToString()}'.";
            Report(location, message);
        }

        public void ReportUndefinedBinaryOperator(TextLocation location, string text, TypeSymbol leftType, TypeSymbol rightType)
        {
            var message = $"Binary operator '{text}' is not defined for type '{leftType.ToString()}' and '{rightType.ToString()}'.";
            Report(location, message);
        }

        public void ReportParameterAlreadyDeclared(string name, TextLocation location)
        {
            var message = $"A parameter with name '{name}' is already defined.";
            Report(location, message);
        }

        public void ReportOptionalParametersMustAppearLast(TextLocation location)
        {
            var message = $"Optional parameters must appear after required parameters.";
            Report(location, message);
        }

        public void ReportSymbolAlreadyDeclared(TextLocation location, string name)
        {
            var message = $"A symbol with name '{name}' is already declared.";
            Report(location, message);
        }

        public void VariableCannotConvert(TextLocation location, TypeSymbol fromType, TypeSymbol toType)
        {
            var message = $"Cannot convert type '{fromType.ToString()}' to type '{toType.ToString()}'.";
            Report(location, message);
        }

        public void ReportNotAllCodePathsReturn(TextLocation location, string name)
        {
            var message = $"Function '{name}' doesn't return on all code paths.";
            Report(location, message);
        }

        public void ReportCannotConvert(TextLocation location, TypeSymbol fromType, TypeSymbol targetType)
        {
            var message = $"Cannot convert type '{fromType.ToString()}' to type '{targetType.ToString()}'.";
            Report(location, message);
        }

        public void ReportCannotImplicitlyConvert(TextLocation location, TypeSymbol fromType, TypeSymbol targetType)
        {
            var message = $"Cannot implicitly convert type '{fromType.ToString()}' to type '{targetType.ToString()}'. It is an explicit conversion, are you missing a cast?";
            Report(location, message);
        }

        public void ReportCannotAssign(TextLocation location, string name)
        {
            var message = $"Cannot assign to variable '{name}'.";
            Report(location, message);
        }

        public void ReportUndefinedVariable(TextLocation location, string name)
        {
            var message = $"Variable '{name}' is not defined in the current scope.";
            Report(location, message);
        }

        public void ReportNotAVariable(TextLocation location, string name)
        {
            var message = $"'{name}' is not a variable therefore, it cannot be used like one.";
            Report(location, message);
        }

        public void ReportUnterminatedString(TextLocation location)
        {
            var message = $"Unterminated string literal.";
            Report(location, message);
        }

        public void ReportOnlyOneFileCanContainGlobalStatements(TextLocation location)
        {
            var message = $"At most one file can contain global statements.";
            Report(location, message);
        }

        public void ReportMainIncorrectSignature(TextLocation location)
        {
            var message = $"Main function must be of type void and have no parameters.";
            Report(location, message);
        }

        public void ReportCannotMixMainFunctionAndGlobalStatements(TextLocation location)
        {
            var message = $"Cannot declare a main function when global statements are used. ";
            Report(location, message);
        }

        public void ReportUndefinedFunction(TextLocation location, string name)
        {
            var message = $"Function '{name}' is not defined in the current scope.";
            Report(location, message);
        }

        public void ReportNotAFunction(TextLocation location, string name)
        {
            var message = $"'{name}' is not a function therefore, it cannot be used like one.";
            Report(location, message);
        }

        public void ReportWrongArgumentCount(TextLocation location, string name, int expectedCount, int count)
        {
            string message;

            if (expectedCount == 1)
                message = $"Function '{name}' expects {expectedCount} argument, got {count}.";
            else
                message = $"Function '{name}' expects {expectedCount} arguments, got {count}.";
            
            Report(location, message);
        }

        public void ReportUndefinedType(TextLocation location, string type)
        {
            var message = $"Type '{type} is not defined in the current scope.'";
            Report(location, message);
        }

        public void ReportExpressionMustHaveAValue(TextLocation location)
        {
            var message = $"Expression must have a different value than void.";
            Report(location, message);
        }

        public void ReportInvalidBreakOrContinue(TextLocation location, string text)
        {
            var message = $"Statement '{text}' is not valid outside of loops.";
            Report(location, message);
        }

        public void ReportUnexpectedReturnExpression(TextLocation location, TypeSymbol type, string functionName)
        {
            var message = $"The function '{functionName}' expects a return value of type 'void', got an expression of type '{type}'.";
            Report(location, message);
        }

        public void ReportReturnExpectsAnExpression(TextLocation location, string functionName)
        {
            var message = $"The return statement for this function '{functionName}' expects a return expression.";
            Report(location, message);
        }

        public void MissingImportStatement(TextLocation location, string name, string fileName)
        {
            var message = $"You are referencing the object '{name}', but it's contained in a different file '{fileName}', are you missing an import statement?";
            Report(location, message);
        }

        public void ReportCannotFindImportFile(TextLocation location, string name)
        {
            var message = $"Cannot find file '{name}' to import.";
            Report(location, message);
        }

        public void ReportInvalidExpressionStatement(TextLocation location)
        {
            var message = $"Only assignment and call expressions can be used as a statement.";
            Report(location, message);
        }

        public void ReportDirectiveExpected(TextLocation location)
        {
            var message = $"Preporcessor directive exprected.";
            Report(location, message);
        }
    }
}