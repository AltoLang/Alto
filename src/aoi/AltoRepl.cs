using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;
using Alto.CodeAnalysis.Text;
using Alto.IO;

namespace Alto
{
    internal sealed class AltoRepl : Repl
    {
        private readonly StringBuilder _textBuilder = new StringBuilder();
        private Compilation _previous;
        private bool _showTree = false;
        private bool _showProgram = false;
        private readonly Dictionary<VariableSymbol, object> _variables = new Dictionary<VariableSymbol, object>();

        protected override void EvaluateSubmission(string text)
        {
            var syntaxTree = SyntaxTree.Parse(text);

            Compilation compilation = _previous == null
                                    ? new Compilation(syntaxTree)
                                    : _previous.ContinueWith(syntaxTree);

            if (_previous == null)
            {
                compilation = new Compilation(syntaxTree);
            }
            else
            {
                compilation = _previous.ContinueWith(syntaxTree);
            }

            if (_showTree)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                syntaxTree.Root.WriteTo(Console.Out);
                Console.ResetColor();
            }

            if (_showProgram)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                compilation.EmitTree(Console.Out);
                Console.ResetColor();
            }

            var result = compilation.Evaluate(_variables);

            var diagnostics = result.Diagnostics;

            if (!result.Diagnostics.Any())
            {
                if (result.Value != null)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(result.Value);
                    Console.ResetColor();
                }
                
                _previous = compilation;
            }
            else
            {
                DiagnosticsWriter.WriteDiagnostics(Console.Out, result.Diagnostics, syntaxTree);
            }
        }

        protected override void RenderLine(string line)
        {
            var tokens = SyntaxTree.ParseTokens(line);
            foreach (var token in tokens)
            {
                var isKeyword = token.Kind.ToString().EndsWith("Keyword");
                var isNumber = token.Kind == SyntaxKind.NumberToken;
                var isIdentifier = token.Kind == SyntaxKind.IdentifierToken;
                var isBool = token.Text.ToLower() == "true" || token.Text.ToLower() == "false";
                var isString = token.Kind == SyntaxKind.StringToken;

                if (isKeyword && !isBool)
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                else if (isNumber)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (isBool)
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                else if (isIdentifier)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                else if (isString)
                    Console.ForegroundColor = ConsoleColor.Magenta;
                else
                    Console.ForegroundColor = ConsoleColor.Gray;

                Console.Write(token.Text);
                Console.ResetColor();
            }
        }

        protected override void EvaluateMetaCommand(string input)
        {
            switch (input.ToLower())
            {
                case "#showtree":
                    _showTree = !_showTree;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(_showTree ? "Showing parse trees" : "Not showing parse trees");
                    Console.ResetColor();
                    break;
                case "#showprogram":
                    _showProgram = !_showProgram;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(_showProgram ? "Showing bound trees" : "Not showing bound trees");
                    Console.ResetColor();
                    break;
                case "#cls":
                    Console.Clear();
                    break;
                case "#reset":
                    _previous = null;
                    break;
                default:
                    base.EvaluateMetaCommand(input);
                    break;
            }
        }

        protected override bool IsCompleteSubmission(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            // checks if the 2 last lines are blank
            var forceComplete = text.Split(Environment.NewLine)
                                        .Reverse()
                                        .TakeWhile(s => string.IsNullOrEmpty(s))
                                        .Take(2)
                                        .Count() == 2;

            if (forceComplete)
                return true;

            var syntaxTree = SyntaxTree.Parse(text);

            if (syntaxTree.Root.Members.Last().GetLastToken().IsMissing)
                return false;

            return true;
        }
    }
}