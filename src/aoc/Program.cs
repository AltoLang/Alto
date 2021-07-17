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

            var paths = GetSourcePath(args);
            var syntaxTrees = new List<SyntaxTree>(paths.Count());
            bool hasErrors = false;

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine("ERR: One or more files do not exist.");
                    hasErrors = true;
                    continue;
                }
                var syntaxTree = SyntaxTree.Load(path);
                syntaxTrees.Add(syntaxTree);
            }

            if (hasErrors)
                return;
            
            var compilation = new Compilation(syntaxTrees.ToArray());
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());

            if (result.Diagnostics.Any())
                DiagnosticsWriter.WriteDiagnostics(Console.Out, result.Diagnostics);
            else
                if (result.Value != null)
                    Console.WriteLine(result.Value);
        }

        private static IEnumerable<string> GetSourcePath(IEnumerable<string> paths)
        {
            var result = new SortedSet<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                    result.UnionWith(Directory.EnumerateFiles(path, "*.ao", SearchOption.AllDirectories));
                else
                    result.Add(path);
            }

            return result;
        }
    }
}
