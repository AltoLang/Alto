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
        private static int Main(string[] args) 
        {
            if (args.Length == 0)
            {   
                Console.Error.WriteLine("Usage: ac <source-directory>");
                return 1;
            }
            else if (args.Length > 1)
            {
                Console.Error.WriteLine("ERR: Only single project paths are supported.");
                return 1;
            }

            var p = args[0];
            bool hasErrors = false;

            if (!Directory.Exists(p))
            {
                Console.Error.WriteLine("ERR: One or more directories do not exist.");
                 hasErrors = true;
            }

            var sourcePaths = GetSourcePath(p);
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var path in sourcePaths)
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
                return 1;
            
            var compilation = Compilation.Create(syntaxTrees.ToArray());
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());

            if (result.Diagnostics.Any())
            {
                DiagnosticsWriter.WriteDiagnostics(Console.Out, result.Diagnostics);
                return 1;
            }
            else
            {
                if (result.Value != null)
                {
                    Console.WriteLine(result.Value);
                }
            }

            return 0;
        }

        private static IEnumerable<string> GetSourcePath(string path)
        {
            var result = new List<string>();

            if (Directory.Exists(path))
                foreach (var file in Directory.EnumerateFiles(path, "*.ao", SearchOption.AllDirectories))
                    if (file != path)
                        result.Add(file);
        
            return result;
        }
    }
}
