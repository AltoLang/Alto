using System;
using System.Collections.Generic;
using Xunit;

using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Syntax;
using Alto.CodeAnalysis.Symbols;

namespace Alto.Tests.CodeAnalysis
{

    public class EvaluatorTests
    {
        [Theory]
        [InlineData("1", 1)]
        [InlineData("+1", 1)]
        [InlineData("-1", -1)]
        [InlineData("~1", -2)]
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
        [InlineData("true && true", true)]
        [InlineData("false || false", false)]
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
        [InlineData("1 & 2", 0)]
        [InlineData("1 | 2", 3)]
        [InlineData("1 | 0", 1)]
        [InlineData("1 & 0", 0)]
        [InlineData("1 ^ 0", 1)]
        [InlineData("0 & 1", 0)]
        [InlineData("1 & 3", 1)]
        [InlineData("false | false", false)]
        [InlineData("false | true", true)]
        [InlineData("true | false", true)]
        [InlineData("true | true", true)]
        [InlineData("false & false", false)]
        [InlineData("false & true", false)]
        [InlineData("true & false", false)]
        [InlineData("true & true", true)]
        [InlineData("false ^ false", false)]
        [InlineData("false ^ true", true)]
        [InlineData("true ^ false", true)]
        [InlineData("true ^ true", false)]
        [InlineData("var a = 0 return (a = 10) * (a * 2)", 200)]
        [InlineData("var result = 0 { var a = 10 if a == 10 { a = a + 40} result = a } return result", 50)]
        [InlineData("var result = 0 { var a = 0 if a == 0 { a = a + 40} result = a } return result", 40)]
        [InlineData("var a = true { if true { a = false} } return a", false)]
        [InlineData("var result = 0 { var a = 800 if a ~= 800 { a = 0} result = a } return result", 800)]
        [InlineData("var foo = false { if foo {print(tostring(25 * 4))} else {foo = !foo} } return foo", true)]
        [InlineData("var n = 0 { var m = 10 while m ~= 0 { n = n + m m = m - 1 } } return  n", 55)]
        [InlineData("var result = 0 { for i = 0 to 10 {result = result + i} } return result", 55)]
        [InlineData("var result = 0 { do {result = result + 1} while(result < 10) } return result", 10)]
        [InlineData("\"foo\" + \"bar\"", "foobar")]
        [InlineData("\"foo\"", "foo")]
        [InlineData("\"foo\" == \"foo\"", true)]
        [InlineData("\"foo\" == \"foobar\"", false)]
        [InlineData("\"foo\" ~= \"foo\"", false)]
        [InlineData("\"foo\" ~= \"foobar\"", true)]
        [InlineData("\"idk\" == \"idk\"", true)]
        [InlineData("\"idk\" == \"test\"", false)]
        [InlineData("\"test\" ~= \"test\"", false)]
        [InlineData("\"test\" ~= \"idk\"", true)]
        [InlineData("var i = 0 { while i < 5 { i = i + 1 if i == 5 continue } } return i", 5)]
        [InlineData("var i = 0 { do { i = i + 1 if i == 5 continue } while i < 5 } return i", 5)]
        [InlineData("function add(x : int = 5, y : int = 10) : int { return x + y } return add()", 15)]
        [InlineData("function add(x : int = 5, y : int = 10) : int { return x + y } return add(7)", 17)]
        [InlineData("function add(x : int = 5, y : int = 10) : int { return x + y } return add(4, 2)", 6)]
        [InlineData("{ function add(x : int = 2, y : int = 2) : int { return x + y } return add() }", 4)]
        [InlineData("{ function add(x : int = 2, y : int = 2) : int { return x + y } return add(3) }", 5)]
        [InlineData("{ function add(x : int = 2, y : int = 2) : int { return x + y } return add(3, 10) }", 13)]
        [InlineData("{ function add(x : int = 2, y : int = 2) : int { return x + y } { return add(10, 10) } }", 20)]
        [InlineData("{ function add(x : int = 2, y : int = 2) : int { return x + y } { { return add(10, 10) } } }", 20)]
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
                A symbol with name 'x' is already declared.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Reports_All_Symbols_Declared_In_Single_Namespace()
        {
            var text = @"
                {
                    let print = 42
                    [print](""test"")
                }
            ";

            var diagnostics = @"
                'print' is not a function therefore, it cannot be used like one.
            ";

            AssertDiagnostics(text, diagnostics);
        }
        
        [Fact]
        public void Evaluator_CallExpression_Reports_Undefined()
        {
            var text = @"[test]()";

            var diagnostics = @"
                Function 'test' is not defined in the current scope.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_CallExpression_Reports_NotAFunction()
        {
            var text = @"
                {
                    let myVar = false
                    [myVar](true)
                }
            ";

            var diagnostics = @"
                'myVar' is not a function therefore, it cannot be used like one.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_AssignmentExpression_Reports_NotAVariable()
        {
            var text = @"[random] = 42";

            var diagnostics = @"
                'random' is not a variable therefore, it cannot be used like one.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_NameExpression_Reports_Undefined()
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
                Variable 'x' is not defined in the current scope.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_AssignedExpression_Reports_CannotAssign()
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
        public void Evaluator_AssignedExpression_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 10
                    x = false
                [}]
            ";

            var diagnostics = @"
                Cannot convert type 'bool' to type 'int'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_IfStatement_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 10
                    if [10]
                    {
                        x = 0
                    }
                }
            ";

            var diagnostics = @"
                Cannot convert type 'int' to type 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_WhileStatement_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 10
                    while [10]
                    {
                        x = 0
                    }
                }
            ";

            var diagnostics = @"
                Cannot convert type 'int' to type 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_ForStatement_Reports_CannotConvert_LowerBound()
        {
            var text = @"
                {
                    var x = 10
                    for index = false [to] 25
                    {
                        x = 0
                    }
                }
            ";

            var diagnostics = @"
                Cannot convert type 'bool' to type 'int'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_ForStatement_Reports_CannotConvert_UpperBound()
        {
            var text = @"
                    var result = 0 { for i = 0 to true [{]result = result + i} } result
            ";

            var diagnostics = @"
                Cannot convert type 'bool' to type 'int'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_UnaryExpression_Reports_Undefined()
        {
            var text = @"[+]true";

            var diagnostics = @"
                Unary operator '+' is not defined for type 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_BinaryExpression_Reports_Undefined()
        {
            var text = @"10 [%] false";

            var diagnostics = @"
                Binary operator '%' is not defined for type 'int' and 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_InvokeFunctionArguments_Reports_NoInfiniteLoop()
        {
            var text = @"
                print(""Hi""[[=]][)]
            ";

            var diagnostics = @"
                Unexpected token <EqualsToken>, expected <CloseParenthesesToken>.
                Unexpected token <EqualsToken>, expected <IdentifierToken>.
                Unexpected token <CloseParenthesesToken>, expected <IdentifierToken>.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_FunctionParameters_Reports_NoInfiniteLoop()
        {
            var text = @"
                function test(name: string[[[*]]][[)]]
                {
                    print(""Hi "" + name + ""!"" )
                }[]
            ";

            var diagnostics = @"
                Unexpected token <StarToken>, expected <CloseParenthesesToken>.
                Unexpected token <StarToken>, expected <OpenBraceToken>.
                Unexpected token <StarToken>, expected <IdentifierToken>.
                Unexpected token <CloseParenthesesToken>, expected <IdentifierToken>.
                Unexpected token <CloseParenthesesToken>, expected <IdentifierToken>.
                Unexpected token <EndOfFileToken>, expected <CloseBraceToken>.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_FunctionOptionalParameters_MustAppearLast()
        {
            var text = @"
                function test(x: int = 5, [y : int])
                {
                    print(tostring(x + y))
                }
            ";

            var diagnostics = @"
                Optional parameters must appear after required parameters.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_InvokeFunctionArgument_Missing()
        {
            var text = @"
                print([)]
            ";

            var diagnostics = @"
                Function 'print' expects 1 argument, got 0.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_InvokeFunctionArgument_Exceeding()
        {
            var text = @"
                print(""hello!""[, ""test"", ""foo""])
            ";

            var diagnostics = @"
                Function 'print' expects 1 argument, got 3.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_InvokeFunctionArguments_Exceeding()
        {
            var text = @"
                var num = random(0, 256[, 3000, 3000])
            ";

            var diagnostics = @"
                Function 'random' expects 2 arguments, got 4.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_InvokeFunctionArguments_Missing()
        {
            var text = @"
                var num = random(0[)]
            ";

            var diagnostics = @"
                Function 'random' expects 2 arguments, got 1.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_NotAllCodePathsReturn_1()
        {
            var text = @"
                function [foo](x : int) : int {
                    var sum = 0
                    var i = x
                    while true {
                        if i == 0
                            break
        
                        sum = sum + i
                        i = i - 1
                    }
                }
            ";

            var diagnostics = @"
                Function 'foo' doesn't return on all code paths.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_NotAllCodePathsReturn_2()
        {
            var text = @"
                function [foo](x : int) : int {
                    var sum = 0
                    var i = x
                    while true {
                        if i == 0
                            break
        
                        sum = sum + i
                        i = i - 1
                    }
                    print(tostring(x))
                }
            ";

            var diagnostics = @"
                Function 'foo' doesn't return on all code paths.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_NotAllCodePathsReturn_3()
        {
            var text = @"
                function [foobar](x : string) : bool 
                { }
            ";

            var diagnostics = @"
                Function 'foobar' doesn't return on all code paths.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_NestedFunctions1()
        {
            var text = @"
                {
                    {
                        function myFunc(text : string) {
                            print(text)
                        }
                    }

                    var txt = ""Test""
                    [myFunc](txt)
                }
            ";

            var diagnostics = @"
                Function 'myFunc' is not defined in the current scope.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        
        private void AssertDiagnostics(string text, string diagnosticText)
        {
            var annotatedText = AnnotatedText.Parse(text);
            var syntaxTree = SyntaxTree.Parse(annotatedText.Text);
            var compilation = Compilation.CreateScript(null, syntaxTree);
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
                var actualSpan = result.Diagnostics[i].Location.Span;
                Assert.Equal(actualSpan, expectedSpan);
            }
        }

        private static void AssertValue(string text, object expectedValue)
        {
            var syntaxTree = SyntaxTree.Parse(text);
            var compilation = Compilation.CreateScript(null, syntaxTree);
            var variables = new Dictionary<VariableSymbol, object>();
            var result = compilation.Evaluate(variables);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(expectedValue, result.Value);
        }
    }
}
