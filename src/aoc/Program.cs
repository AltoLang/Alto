﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;
using Alto.IO;
using Mono.Options;

namespace Alto
{
    internal static class Program
    {
        private static string[] _requiredAssemblies = new string[] {"System.Runtime.dll", "System.Console.dll", "System.Runtime.Extensions.dll"};

        private static int Main(string[] args) 
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Invalid project path");
                return 1;
            }

            List<string> references = new List<string>();
            string outputPath = null;
            string moduleName = null;
            string dependenciesPath = null;
            string projectDirectoryPath = args[0];
            bool helpRequested = false;

            var options = new OptionSet 
            {
                "usage: aoc <project-directory-path> [options]",
                { "r=", "The {path} of an assembly to reference", r => references.Add(r) },
                { "o=", "The output {path} of the assembly to create", o => outputPath = o },
                { "m=", "The {name} of the module", m => moduleName = m },
                { "i=", "Dependencies folder path", i => dependenciesPath = i},
                { "help|h|?", "Help!!!", h => helpRequested = true }
            };

            var argsToParse = args.ToList();
            argsToParse.RemoveAt(0);    
            options.Parse(argsToParse.ToArray());

            // trim references
            var newReferences = new List<string>();
            foreach (var reference in references)
            {
                bool found = false;
                foreach (var required in _requiredAssemblies)
                {
                    if (reference.Contains(required))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                    newReferences.Add(reference);
            }

            references = newReferences;

            Console.WriteLine($"rl: {references.Count}");
            foreach (var r in references)
                Console.WriteLine($"reference: {r}");

            if (helpRequested)
            {
                options.WriteOptionDescriptions(Console.Out);
                return 0;
            } 

            bool hasErrors = false;
            if (!Directory.Exists(projectDirectoryPath))
            {
                Console.Error.WriteLine($"ERR: One or more project directories do not exist: {projectDirectoryPath}.");
                hasErrors = true;
            }   

            if (outputPath == null)
            {
                var di = new DirectoryInfo(projectDirectoryPath);
                outputPath = di.FullName + @"\" + di.Name + ".exe";
            }

            if (moduleName == null)
                moduleName = Path.GetFileNameWithoutExtension(outputPath);

            var sourcePaths = GetSourcePath(projectDirectoryPath);
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var path in sourcePaths)
            {
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine("ERR: One or more source files do not exist.");
                    hasErrors = true;
                    continue;
                }
    
                var syntaxTree = SyntaxTree.Load(path);
                syntaxTrees.Add(syntaxTree);
            }   

            if (hasErrors)
                return 1;

            var dependencies = new List<string>();
            foreach (var file in Directory.GetFiles(dependenciesPath))
            {
                var info = new FileInfo(file);
                if (info.Extension != ".dll")
                    continue;

                dependencies.Add(file);
            }
            
            var compilation = Compilation.Create(dependencies, syntaxTrees.ToArray());
            var diagnostics = compilation.Emit(moduleName, references.ToArray(), outputPath);
            if (diagnostics.Any())
            {
                DiagnosticsWriter.WriteDiagnostics(Console.Out, diagnostics);
                return 1;
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
