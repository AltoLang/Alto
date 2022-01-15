using System;
using System.IO;
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

        public AssemblyImport(string path)
        {
            var assembly = AssemblyDefinition.ReadAssembly(path);
            _assembly = assembly;
        }

        public AssemblyImport(AssemblyDefinition assembly)
        {
            _assembly = assembly;
        }

        public AssemblyDefinition Assembly => _assembly;
        public ImmutableArray<MethodDefinition> Functions => GetFunctions();
        public Dictionary<FunctionSymbol, MethodDefinition> FunctionSymbols => _functionSymbols;
        public string Name => _assembly.Name.Name;

        public void MapFunctionSymbol(FunctionSymbol function, MethodDefinition method)
            => _functionSymbols.Add(function, method);

        public ImmutableArray<ModuleDefinition> GetModules()
            => _assembly.Modules.ToImmutableArray();

        public ModuleDefinition? GetModuleByName(string name)
        {   
            var modules = _assembly.Modules.Where(m => Path.GetFileNameWithoutExtension(m.Name) == name);
            return modules.FirstOrDefault();
        }

        public ImmutableArray<TypeDefinition> GetTypesInModule(ModuleDefinition module)
            => module.Types.ToImmutableArray();

        public ImmutableArray<MethodDefinition> GetMethodsInType(TypeDefinition type)
            => type.Methods.ToImmutableArray();

        private ImmutableArray<MethodDefinition> GetFunctions()
        {
            // TODO: Completely revamp all of this
            // Once we actually have OO-Support #55

            var methods = new List<MethodDefinition>();
            foreach (var module in _assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.IsConstructor || !method.IsPublic)
                            continue;

                        methods.Add(method);
                    }
                }
            }
            
            return methods.ToImmutableArray();
        }
    }
}