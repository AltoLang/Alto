using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Syntax;
using Alto.CodeAnalysis.Text;

namespace REPL
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
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(result.Value);
                Console.ResetColor();

                _previous = compilation;
            }
            else
            {
                foreach (var diagnostic in diagnostics)
                {
                    var lineIndex = syntaxTree.Text.GetLineIndex(diagnostic.Span.Start);
                    var lineNumber = lineIndex + 1;
                    var line = syntaxTree.Text.Lines[lineIndex];
                    var character = diagnostic.Span.Start - line.Start;

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write($"({lineNumber}, {character}): ");
                    Console.WriteLine(diagnostic);
                    Console.ResetColor();

                    var prefixSpan = TextSpan.FromBounds(line.Start, diagnostic.Span.Start);
                    var suffixSpan = TextSpan.FromBounds(diagnostic.Span.End, line.End);

                    var prefix = syntaxTree.Text.ToString(prefixSpan);
                    var error = syntaxTree.Text.ToString(diagnostic.Span);
                    var suffix = syntaxTree.Text.ToString(suffixSpan);

                    Console.Write("    ");
                    Console.Write(prefix);

                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write(error);
                    Console.ResetColor();

                    Console.Write(suffix);

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
        }

        protected override void RenderLine(string line)
        {
            var tokens = SyntaxTree.ParseTokens(line);
            foreach (var token in tokens)
            {
                var isKeyword = token.Kind.ToString().EndsWith("Keyword");
                var isNumber = token.Kind == SyntaxKind.NumberToken;
                var isBool = token.Text.ToLower() == "true" || token.Text.ToLower() == "false";

                if (isKeyword && !isBool)
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                else if (isNumber)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (isBool)
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
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

            var syntaxTree = SyntaxTree.Parse(text);

            if (syntaxTree.Root.Statement.GetLastToken().IsMissing)
                return false;

            return true;
        }
    }
}