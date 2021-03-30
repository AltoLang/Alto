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
        [InlineData("(a = 10) * 20", 200)]
        public void SyntaxFact_GetText_RoundTrips(string text, object expectedValue)
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
