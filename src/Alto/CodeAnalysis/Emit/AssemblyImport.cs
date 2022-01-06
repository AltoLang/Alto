using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alto.CodeAnalysis.Symbols;
using Mono.Cecil;

namespace Alto.CodeAnalysis.Emit
{
    internal class AssemblyImport
    {
        private AssemblyDefinition _assembly;
        private Dictionary<FunctionSymbol, MethodDefinition> _functionSymbols = new Dictionary<FunctionSymbol, MethodDefinition>();
        private List<MethodReference> _importedMethods;
        private Dictionary<FunctionSymbol, MethodReference> _importedFunctionSymbols;

        public AssemblyImport(string path)
        {
            var assembly = AssemblyDefinition.ReadAssembly(path);
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                    }
                }
            }

            _assembly = assembly;
        }

        public AssemblyImport(AssemblyDefinition assembly)
        {
            _assembly = assembly;
        }

        public AssemblyDefinition Assembly => _assembly;
        public ImmutableArray<MethodDefinition> Functions => GetFunctions();
        public Dictionary<FunctionSymbol, MethodDefinition> FunctionSymbols => _functionSymbols;

        public void AddFunctionSymbol(FunctionSymbol function, MethodDefinition method)
            => _functionSymbols.Add(function, method);

        public MethodDefinition GetMethodDefinition(FunctionSymbol function) 
            => _functionSymbols[function];

        public MethodDefinition TryGetMethodDefinition(FunctionSymbol function)
        {
            if (!_functionSymbols.ContainsKey(function))
                return null;
               
            return _functionSymbols[function];
        }

        public FunctionSymbol GetFunctionSymbol(MethodDefinition method)
            => _functionSymbols.FirstOrDefault(x => x.Value == method).Key;

        public FunctionSymbol TryGetFunctionSymbol(MethodDefinition method)
        {
            if (!_functionSymbols.ContainsValue(method))
                return null;
               
            return _functionSymbols.FirstOrDefault(x => x.Value == method).Key;
        }

        public MethodReference GetImportedMethodReference(FunctionSymbol function) 
            => _importedFunctionSymbols[function];

        public MethodReference TryGetImportedMethodReference(FunctionSymbol function)
        {
            if (!_importedFunctionSymbols.ContainsKey(function))
                return null;
               
            return _importedFunctionSymbols[function];
        }

        public FunctionSymbol GetImportedFunctionSymbol(MethodReference method)
            => _importedFunctionSymbols.FirstOrDefault(x => x.Value == method).Key;

        public FunctionSymbol TryGetFunctionSymbol(MethodReference method)
        {
            if (!_importedFunctionSymbols.ContainsValue(method))
                return null;
               
            return _importedFunctionSymbols.FirstOrDefault(x => x.Value == method).Key;
        }

        public void ImportAndUpdateMethodDefinitions(AssemblyDefinition assembly)
        {
            var importedMethods = new List<MethodReference>();
            var importedFunctionSymbols = new Dictionary<FunctionSymbol, MethodReference>();

            foreach (var method in Functions)
            {
                var imported = assembly.MainModule.ImportReference(method);
                var symbol = TryGetFunctionSymbol(method);

                importedMethods.Add(imported);
                importedFunctionSymbols.Add(symbol, imported);
            }

            _importedMethods = importedMethods;
            _importedFunctionSymbols = importedFunctionSymbols;
        }

        private ImmutableArray<MethodDefinition> GetFunctions()
        {
            // TODO: Completely revamp all of this
            // Once we actually have OO-Support #55

            var methods = new List<MethodDefinition>();
            foreach (var module in _assembly.Modules)
            {
                Console.WriteLine($"module: {module.Name}");
                foreach (var type in module.Types)
                {
                    Console.WriteLine($"    type: {type.Name}");
                    foreach (var method in type.Methods)
                    {
                        Console.WriteLine($"        method: {method.Name}");
                        if (method.IsConstructor || method.IsStatic || !method.IsPublic)
                            continue;

                        methods.Add(method);
                    }
                }
            }
            
            return methods.ToImmutableArray();
        }
    }
}