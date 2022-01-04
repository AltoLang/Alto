using System.Collections.Immutable;
using Mono.Cecil;

namespace Alto.CodeAnalysis.Emit
{
    internal class AssemblyImport
    {
        private AssemblyDefinition _assembly;

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

        private ImmutableArray<MethodDefinition> GetFunctions()
        {
            // TODO: Completely revamp all of this
            // Once we actually have OO-Support #55

            var module = _assembly.Modules.First();
            var type = module.Types[0];
            var functions = type.Methods;
            
            return functions.ToImmutableArray();
        }
    }
}