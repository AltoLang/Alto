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
        private Dictionary<TypeReference, (FunctionSymbol function, MethodDefinition method)> _functions = new();

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
        public string Name => _assembly.Name.Name;
        public IEnumerable<ModuleDefinition> Modules => Assembly.Modules;

        public ModuleDefinition? GetModuleByName(string name)
        {   
            var modules = _assembly.Modules.Where(m => Path.GetFileNameWithoutExtension(m.Name) == name);
            return modules.FirstOrDefault();
        }

        public static ImmutableArray<TypeDefinition> GetTypesInModule(ModuleDefinition module)
            => module.Types.ToImmutableArray();

        public static FunctionSymbol GetFunctionFromMethod(MethodDefinition method)
        {
            var parameters = new List<ParameterSymbol>();
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                var param = method.Parameters[i];
                var type = Emitter.GetTypeSymbol(param.ParameterType);
                var parameter = new ParameterSymbol(param.Name, type, i);
                parameters.Add(parameter);
            }

            var returnType = Emitter.GetTypeSymbol(method.ReturnType);
            var function = new FunctionSymbol(method.Name, parameters.ToImmutableArray(), returnType);
            return function;
        }
    }
}