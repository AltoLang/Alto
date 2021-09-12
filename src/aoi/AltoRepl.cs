using System;
using System.Collections.Generic;
using System.IO;
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
        private bool _loadingSubmissions = false;
        private static readonly Compilation emptyCompilation = Compilation.CreateScript(null, null);
        private readonly Dictionary<VariableSymbol, object> _variables = new Dictionary<VariableSymbol, object>();

        public AltoRepl()
        {
            LoadSubmissions();
        }

        protected override void EvaluateSubmission(string text)
        {
            var syntaxTree = SyntaxTree.Parse(text);

            Compilation compilation = Compilation.CreateScript(_previous, syntaxTree);

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
                SaveSubmission(text);
            }
            else
            {
                DiagnosticsWriter.WriteDiagnostics(Console.Out, result.Diagnostics);
            }
        }

        private void LoadSubmissions()
        {
            var dir = GetSubmissionsDirectory();
            if (!Directory.Exists(dir))
                return;
            
            var submissions = Directory.GetFiles(dir).OrderBy(f => f);
            if (submissions.Count() == 0)
                return;

            var count = submissions.Count();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            if (count > 1 || count == 0)
                Console.WriteLine($"{count} submissions loaded.");
            else
                Console.WriteLine($"{count} submission loaded.");
            Console.ResetColor();

            _loadingSubmissions = true;
            foreach (var file in submissions)
            {
                var text = File.ReadAllText(file);
                EvaluateSubmission(text);
            }
            _loadingSubmissions = false;
        }

        private void ClearSubmissions() 
        {
            var dir = GetSubmissionsDirectory();
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        } 

        private void SaveSubmission(string text)
        {
            if (_loadingSubmissions)
                return;
            
            var submissionFolder = GetSubmissionsDirectory();
            Directory.CreateDirectory(submissionFolder);
            var count = Directory.GetFiles(submissionFolder).Length;
            var name = $"submission{count:0000}";
            var filename = Path.Combine(submissionFolder, name);
            File.WriteAllText(filename, text);
        }

        private string GetSubmissionsDirectory()
        {
            var localAppData = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var submissionFolder = Path.Combine(localAppData, "Alto", "Submissions");
            return submissionFolder;
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

        [MetaCommand("showtree", description: "Shows the parse tree representation.")]
        private void EvaluateShowTree()
        {
            _showTree = !_showTree;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(_showTree ? "Showing parse trees" : "Not showing parse trees");
            Console.ResetColor();
        }

        [MetaCommand("showprogram", description: "Shows the bound tree representation.")]
        private void EvaluateShowProgram()
        {
            _showProgram = !_showProgram;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(_showProgram ? "Showing bound trees" : "Not showing bound trees");
            Console.ResetColor();
        }

        [MetaCommand("load", description: "Loads a file.")]
        private void EvaluateLoad(string path)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File '{path}' does not exist!");
                Console.ResetColor();
                return;
            }

            var txt = File.ReadAllText(path);
            EvaluateSubmission(txt);

            var tree = SyntaxTree.Load(path);
            _previous = Compilation.CreateScript(_previous, tree);
        }

        [MetaCommand("ls", description: "Lists all symbols.")]
        private void EvaluateListSymbols()
        {
            var compilation = _previous ?? emptyCompilation;
            var symbols = compilation.GetSymbols().OrderBy(s => s.Kind).ThenBy(s => s.Name);
            foreach (var symbol in symbols)
            {
                symbol.WriteTo(Console.Out);
                Console.WriteLine();
            }
        }

        [MetaCommand("dump", description: "Shows the bound tree representation of a given function.")]
        private void EvaluateDump(string functionName)
        {   
            var compilation = _previous ?? emptyCompilation;
            var symbol = compilation.GetSymbols().OfType<FunctionSymbol>().SingleOrDefault(f => f.Name == functionName);
            if (symbol == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Function '{functionName}' not found!");
                Console.ResetColor();
                return;
            }

            compilation.EmitTree(symbol, Console.Out);

        }

        [MetaCommand("cls", description: "Clears the screen.")]
        private void EvaluateCls()
        {
            Console.Clear();
        }

        [MetaCommand("reset", description: "Resets chained compilations and all stored submissions.")]
        private void EvaluateReset()
        {
            _previous = null;
            ClearSubmissions();
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