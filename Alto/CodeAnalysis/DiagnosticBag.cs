using System;
using System.Collections;
using System.Collections.Generic;
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

        private void Report(TextSpan span, string message)
        {
            var diagnostic = new Diagnostic(span, message);
            _diagnostics.Add(diagnostic);
        }

        public void ReportInvalidNumber(TextSpan textSpan, string text, Type type)
        {
            var message = $"The number {text} isn't a valid {type}";
            Report(textSpan, message);
        }

        public void ReportBadCharacter(int position, char character)
        {
            var message = $"Bad character in input: '{character}'";
            TextSpan span = new TextSpan(position, 1);
            Report(span, message);
        }

        public void ReportUnexpectedToken(TextSpan span, SyntaxKind kind, SyntaxKind expectedKind)
        {
            var message = $"Unexpeced token <{kind}, expected <{expectedKind}>";
            Report(span, message);
        }

        public void ReportUndefinedUnaryOperator(TextSpan span, string operatorText, Type operandType)
        {
            var message = $"Unary operator '{operatorText}' is not defined for type {operandType}";
            Report(span, message);
        }

        public void ReportUndefinedName(TextSpan span, string name)
        {
           var message = $"Variable '{name}' doesn't exist";
           Report(span, message);
        }

        public void ReportUndefinedBinaryOperator(TextSpan span, string text, Type leftType, Type rightType)
        {
            var message = $"Binary operator {text} is not defined for type {leftType} and {rightType}";
            Report(span, message);
        }

        public void ReportVariableAlreadyDeclared(TextSpan span, string name)
        {
            var message = $"Variable '{name}' is already declared in the current scope.";
            Report(span, message);
        }

        internal void VariableCannotConvert(TextSpan span, Type fromType, Type toType)
        {
            var message = $"Cannot convert type '{fromType} to type '{toType}'.";
            Report(span, message);
        }
    }
}