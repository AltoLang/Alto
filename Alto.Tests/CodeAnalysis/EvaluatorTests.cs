using System;
using System.Collections.Generic;
using Xunit;

using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Syntax;

namespace Alto.Tests.CodeAnalysis
{

    public class EvaluatorTests
    {
        [Theory]
        [InlineData("1", 1)]
        [InlineData("+1", 1)]
        [InlineData("-1", -1)]
        [InlineData("25 * 25", 625)]
        [InlineData("98 - 654165", -654067)]
        [InlineData("5654 % 415", 259)]
        [InlineData("12 / 3", 4)]
        [InlineData("(62)", 62)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("!true", false)]
        [InlineData("!false", true)]
        [InlineData("true == false", false)]
        [InlineData("true ~= false", true)]
        [InlineData("false ~= false", false)]
        [InlineData("12 == 6541", false)]
        [InlineData("1 == 1", true)]
        [InlineData("1 ~= 1", false)]
        [InlineData("154 ~= 6541", true)]
        [InlineData("1 == 1 && true", true)]
        [InlineData("true || 9873 == 8574", true)]
        [InlineData("1 > 0", true)]
        [InlineData("1 >= 1", true)]
        [InlineData("1 <= 1", true)]
        [InlineData("8 <= 80", true)]
        [InlineData("2 < 80", true)]
        [InlineData("50 <= 50", true)]
        [InlineData("1 < 0", false)]
        [InlineData("1 >= 11", false)]
        [InlineData("15 <= 1", false)]
        [InlineData("80056 <= 80", false)]
        [InlineData("2 > 80", false)]
        [InlineData("125 <= 50", false)]
        [InlineData("{ var a = 0 (a = 10) * (a * 2) }", 200)]
        [InlineData("{ var a = 10 if a == 10 { a = a + 40} a }", 50)]
        [InlineData("{ var a = 0 if a == 0 { a = a + 40} a }", 40)]
        [InlineData("{ var a = true if true { a = false} a }", false)]
        [InlineData("{ var a = 800 if a ~= 800 { a = 0} a }", 800)]
        [InlineData("{var foo = false if foo {25 * 4} else {foo = !foo} foo }", true)]
        [InlineData("{ var n = 0 var m = 10 while m ~= 0 { n = n + m m = m - 1 } n}", 55)]
        public void Evaluator_Computes_CorrectValues(string text, object expectedValue)
        {
            AssertValue(text, expectedValue);
        }

        [Fact]
        public void Evaluator_VariableDeclaration_Reports_Redeclaration()
        {
            var text = @"
                {
                    var x = 10
                    var y = 100
                    {
                        var x = 10
                    }
                    var [x] = 5
                }
            ";

            var diagnostics = @"
                Variable 'x' is already declared in the current scope.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_VariableUndefined()
        {
            var text = @"
                {
                    [x] + 20 * 52
                    var y = 25
                    {
                        var m = y + 35
                    }
                }
            ";

            var diagnostics = @"
                Variable 'x' doesn't exist
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Assigned_Reports_CannotAssign()
        {
            var text = @"
                {
                    let x = 10
                    x [=] 4 * 4 * 4 * 4
                }
            ";

            var diagnostics = @"
                Cannot assign to variable 'x'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Assigned_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 10
                    x = false
                [}]
            ";

            var diagnostics = @"
                Cannot convert type 'System.Int32' to type 'System.Boolean'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Unary_Reports_Undefined()
        {
            var text = @"[+]true";

            var diagnostics = @"
                Unary operator '+' is not defined for type 'System.Boolean'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Binary_Reports_Undefined()
        {
            var text = @"10 [%] false";

            var diagnostics = @"
                Binary operator '%' is not defined for type 'System.Int32' and 'System.Boolean'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        private void AssertDiagnostics(string text, string diagnosticText)
        {
            var annotatedText = AnnotatedText.Parse(text);
            var syntaxTree = SyntaxTree.Parse(annotatedText.Text);
            var compilation = new Compilation(syntaxTree);
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());

            var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);

            if (annotatedText.Spans.Length != expectedDiagnostics.Length)
                throw new Exception("ERROR: must mark as many spans as there are expected diagnostics.");

            Assert.Equal(expectedDiagnostics.Length, result.Diagnostics.Length);

            for (var i = 0; i < expectedDiagnostics.Length; i++)
            {
                var expectedMessage = expectedDiagnostics[i];
                var actualMessage = result.Diagnostics[i].Message;
                Assert.Equal(expectedMessage, actualMessage);

                var expectedSpan = annotatedText.Spans[i];
                var actualSpan = result.Diagnostics[i].Span;
                Assert.Equal(expectedSpan, actualSpan);
            }
        }

        private static void AssertValue(string text, object expectedValue)
        {
            var syntaxTree = SyntaxTree.Parse(text);
            var compilation = new Compilation(syntaxTree);
            var variables = new Dictionary<VariableSymbol, object>();
            var result = compilation.Evaluate(variables);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(expectedValue, result.Value);
        }
    }
}
