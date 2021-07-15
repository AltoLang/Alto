using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;
using Alto.IO;

namespace Alto
{
    internal static class Program
    {
        private static void Main(string[] args) 
        {
            if (args.Length == 0)
            {   
                Console.Error.WriteLine("Usage: ac <source-paths>");
                return;
            }
    
            if (args.Length > 1)
            {
                Console.Error.WriteLine("Currently, only one path is supported.");
                return;
            }

            var path = args.Single();
            var text = File.ReadAllText(path);

            var syntaxTree = SyntaxTree.Parse(text);
            var compilation = new Compilation(syntaxTree);
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());

            if (result.Diagnostics.Any())
                DiagnosticsWriter.WriteDiagnostics(Console.Out, result.Diagnostics, syntaxTree);
            else
                if (result.Value != null)
                    Console.WriteLine(result.Value);
        }
    }
}
