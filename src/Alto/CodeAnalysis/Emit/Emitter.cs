using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alto.CodeAnalysis.Binding;
using Alto.CodeAnalysis.Symbols;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Alto.CodeAnalysis.Emit
{
    internal class Emitter
    {
        internal static ImmutableArray<Diagnostic> Emit(BoundProgram program, string moduleName, string[] references, string outPath)
        {
            if (program.Diagnostics.Any())
                return program.Diagnostics.ToImmutableArray();

            var result = new DiagnosticBag();

            var assemblies = new List<AssemblyDefinition>();
            foreach (var reference in references)
            {
                try
                {
                    var refAssembly = AssemblyDefinition.ReadAssembly(reference);
                    assemblies.Add(refAssembly);
                }
                catch
                {
                    result.ReportInvalidReference(reference);
                }
            }

            if (result.Any())
                return result.ToImmutableArray();

            var assemblyName = new AssemblyNameDefinition(moduleName, new Version(1, 0));
            var assembly = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);

            var builtinTypes = new List<(TypeSymbol type, string MetadataName)>()
            {
                (TypeSymbol.Any, "System.Object"),
                (TypeSymbol.Int, "System.Int32"),
                (TypeSymbol.String, "System.String"),
                (TypeSymbol.Bool, "System.Boolean"),
                (TypeSymbol.Void, "System.Void"),
            };

            var knowsTypes = new Dictionary<TypeSymbol, TypeReference>();
            foreach (var (typeSymbol, metadataName) in builtinTypes)
            {
                var typeReference = ResolveType(typeSymbol.Name, metadataName);
                knowsTypes.Add(typeSymbol, typeReference);
            }

            TypeReference ResolveType(string altoName, string metadataName)
            {
                var foundTypes = assemblies.SelectMany(a => a.Modules)
                                           .SelectMany(m => m.Types)
                                           .Where(t => t.FullName == metadataName)
                                           .ToArray();
                if (foundTypes.Length == 1)
                {
                    var typeReference = assembly.MainModule.ImportReference(foundTypes[0]);
                    return typeReference;
                }
                else if (foundTypes.Length == 0)
                {
                    result.ReportRequiredTypeNotFound(altoName, metadataName);
                }
                else if (foundTypes.Length > 1)
                {
                    result.ReportRequiredTypeAmbiguous(altoName, metadataName, foundTypes);
                }

                return null;
            }

            MethodReference ResolveMethod(string typeName, string methodName, string[] parameterTypeNames)
            {
                var foundTypes = assemblies.SelectMany(a => a.Modules)
                                           .SelectMany(m => m.Types)
                                           .Where(t => t.FullName == typeName)
                                           .ToArray();
                if (foundTypes.Length == 1)
                {
                    var type = foundTypes[0];
                    var methods = type.Methods.Where(m => m.Name == methodName);

                    foreach (var method in methods)
                    {
                        if (parameterTypeNames.Length != method.Parameters.Count)
                            continue;

                        var paramsMatching = true;

                        for (var i = 0; i < parameterTypeNames.Length; i++)
                        {
                            if (method.Parameters[i].ParameterType.FullName != parameterTypeNames[i])
                            {
                                paramsMatching = false;
                                break;
                            }
                        }

                        if (!paramsMatching)
                            continue;

                        return assembly.MainModule.ImportReference(method);
                    }

                    result.ReportRequiredMethodNotFound(typeName, methodName, parameterTypeNames);
                    return null!;
                }
                else if (foundTypes.Length == 0)
                {
                    result.ReportRequiredTypeNotFound(null, typeName);
                }
                else
                {
                    result.ReportRequiredTypeAmbiguous(null, typeName, foundTypes);
                }

                return null!;
            }

            var consoleWriteLineReference = ResolveMethod("System.Console", "WriteLine", new string[] {"System.String"});

            var voidType = knowsTypes[TypeSymbol.Void];
            var objectType = knowsTypes[TypeSymbol.Any];

            var typeDefinition = new TypeDefinition("", "Program", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
            assembly.MainModule.Types.Add(typeDefinition);

            var mainMethod = new MethodDefinition("Main", MethodAttributes.Static | MethodAttributes.Private, voidType);
            typeDefinition.Methods.Add(mainMethod);
            assembly.EntryPoint = mainMethod;

            var ilProcessor = mainMethod.Body.GetILProcessor();

            ilProcessor.Emit(OpCodes.Ldstr, "Hello, Alto!");
            ilProcessor.Emit(OpCodes.Call, consoleWriteLineReference);
            ilProcessor.Emit(OpCodes.Ret);

            assembly.Write(outPath);

            return result.ToImmutableArray();
        }
    }
}