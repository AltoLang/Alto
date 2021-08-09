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
                Console.Error.WriteLine("Usage: ac <source-path>");
                return 1;
            }
            else if (args.Length > 1)
            {
                Console.Error.WriteLine("ERR: Only single project paths are supported.");
                return 1;
            }

            var p = args[0];
            bool hasErrors = false;

            if (!File.Exists(p))
            {
                Console.Error.WriteLine("ERR: One or more files do not exist.");
                 hasErrors = true;
            }

            var coreSyntax = SyntaxTree.Load(p);

            var otherPaths = GetSourcePath(p);
            foreach (var path in otherPaths)
                Console.WriteLine("other path: " + path);

            var syntaxTrees = new List<SyntaxTree>();
            foreach (var path in otherPaths)
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
            
            var compilation = new Compilation(coreSyntax, syntaxTrees.ToArray());
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
            var dirPath = Directory.GetParent(path).FullName;

            if (Directory.Exists(dirPath))
                foreach (var file in Directory.EnumerateFiles(dirPath, "*.ao", SearchOption.AllDirectories))
                    if (file != path)
                        result.Add(file);
        
            return result;
        }
    }
}
