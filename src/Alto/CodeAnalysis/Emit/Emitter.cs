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
    internal sealed class Emitter
    {
        private DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly AssemblyDefinition _assembly;
        private readonly Dictionary<TypeSymbol, TypeReference> _knowsTypes;
        private readonly MethodReference _consoleWriteLineReference;
        private TypeDefinition _typeDefinition;
        private readonly Dictionary<FunctionSymbol, MethodDefinition> _methods = new Dictionary<FunctionSymbol, MethodDefinition>();

        private Emitter(string moduleName, string[] references)
        {   
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
                    _diagnostics.ReportInvalidReference(reference);
                }
            }

            var assemblyName = new AssemblyNameDefinition(moduleName, new Version(1, 0));
            _assembly = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);

            var builtinTypes = new List<(TypeSymbol type, string MetadataName)>()
            {
                (TypeSymbol.Any, "System.Object"),
                (TypeSymbol.Int, "System.Int32"),
                (TypeSymbol.String, "System.String"),
                (TypeSymbol.Bool, "System.Boolean"),
                (TypeSymbol.Void, "System.Void"),
            };

            _knowsTypes = new Dictionary<TypeSymbol, TypeReference>();
            foreach (var (typeSymbol, metadataName) in builtinTypes)
            {
                var typeReference = ResolveType(typeSymbol.Name, metadataName);
                _knowsTypes.Add(typeSymbol, typeReference);
            }

            TypeReference ResolveType(string altoName, string metadataName)
            {
                var foundTypes = assemblies.SelectMany(a => a.Modules)
                                           .SelectMany(m => m.Types)
                                           .Where(t => t.FullName == metadataName)
                                           .ToArray();

                if (foundTypes.Length == 1)
                {
                    var typeReference = _assembly.MainModule.ImportReference(foundTypes[0]);
                    return typeReference;
                }
                else if (foundTypes.Length == 0)
                {
                    _diagnostics.ReportRequiredTypeNotFound(altoName, metadataName);
                }
                else if (foundTypes.Length > 1)
                {
                    _diagnostics.ReportRequiredTypeAmbiguous(altoName, metadataName, foundTypes);
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

                        return _assembly.MainModule.ImportReference(method);
                    }
                    
                    _diagnostics.ReportRequiredMethodNotFound(typeName, methodName, parameterTypeNames);
                    return null;
                }
                else if (foundTypes.Length == 0)
                {
                    _diagnostics.ReportRequiredTypeNotFound(null, typeName);
                }
                else
                {
                    _diagnostics.ReportRequiredTypeAmbiguous(null, typeName, foundTypes);
                }

                return null;
            }

            _consoleWriteLineReference = ResolveMethod("System.Console", "WriteLine", new string[] {"System.String"});
        }
        
        internal static ImmutableArray<Diagnostic> Emit(BoundProgram program, string moduleName, string[] references, string outPath)
        {
            var emitter = new Emitter(moduleName, references);
            return emitter.Emit(program, outPath);
        }
        
        public ImmutableArray<Diagnostic> Emit(BoundProgram program, string outPath)
        {
            if (program.Diagnostics.Any())
                return program.Diagnostics.ToImmutableArray();

            var objectType = _knowsTypes[TypeSymbol.Any];

            _typeDefinition = new TypeDefinition("", "Program", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
            _assembly.MainModule.Types.Add(_typeDefinition);

            foreach (var (function, body) in program.FunctionBodies)
            {
                EmitFunctionDeclaration(function);
                EmitFunctionBody(function, body);
            }

            if (program.MainFunction != null)
                _assembly.EntryPoint = _methods[program.MainFunction];
            
            _assembly.Write(outPath);

            return _diagnostics.ToImmutableArray();
        }

        private void EmitFunctionDeclaration(FunctionSymbol function)
        {
            var voidType = _knowsTypes[TypeSymbol.Void];
            var method = new MethodDefinition("Main", MethodAttributes.Static | MethodAttributes.Private, voidType);
            _typeDefinition.Methods.Add(method);
            _methods.Add(function, method);
        }

        private void EmitFunctionBody(FunctionSymbol function, BoundBlockStatement body)
        {
            var method = _methods[function];
            var ilProcessor = method.Body.GetILProcessor();
            EmitStatement(ilProcessor, body);
        }

        private void EmitStatement(ILProcessor ilProcessor, BoundStatement node)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.BlockStatement:
                    EmitBlockStatement(ilProcessor, (BoundBlockStatement)node);
                    break;
                case BoundNodeKind.ExpressionStatement:
                    EmitExpressionStatement(ilProcessor, (BoundExpressionStatement)node);
                    break;
                case BoundNodeKind.VariableDeclaration:
                    EmitVariableDeclaration(ilProcessor, (BoundVariableDeclaration)node);
                    break;
              case BoundNodeKind.GotoStatement:
                    EmitGotoStatement(ilProcessor, (BoundGotoStatement)node);
                    break;
                case BoundNodeKind.ConditionalGotoStatement:
                    EmitConditionalGotoStatement(ilProcessor, (BoundConditionalGotoStatement)node);
                    break;
                case BoundNodeKind.LabelStatement:
                    EmitLabelStatement(ilProcessor, (BoundLabelStatement)node);
                    break;
                case BoundNodeKind.ReturnStatement:
                    EmitReturnStatement(ilProcessor, (BoundReturnStatement)node);
                    break;
                default:
                    throw new Exception($"Unexpected node: '{node.Kind}'");
            }
        }

        private void EmitBlockStatement(ILProcessor ilProcessor, BoundBlockStatement node)
        {
            foreach (var childStatement in node.Statements)
                EmitStatement(ilProcessor, node);
        }

        private void EmitReturnStatement(ILProcessor ilProcessor, BoundReturnStatement node)
        {
            throw new NotImplementedException();
        }

        private void EmitLabelStatement(ILProcessor ilProcessor, BoundLabelStatement node)
        {
            throw new NotImplementedException();
        }

        private void EmitConditionalGotoStatement(ILProcessor ilProcessor, BoundConditionalGotoStatement node)
        {
            throw new NotImplementedException();
        }

        private void EmitGotoStatement(ILProcessor ilProcessor, BoundGotoStatement node)
        {
            throw new NotImplementedException();
        }

        private void EmitVariableDeclaration(ILProcessor ilProcessor, BoundVariableDeclaration statement)
        {
            throw new NotImplementedException();
        }

        private void EmitExpressionStatement(ILProcessor ilProcessor, BoundExpressionStatement node)
        {
            EmitExpression(ilProcessor, node.Expression);
        }

        private void EmitExpression(ILProcessor ilProcessor, BoundExpression node)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.UnaryExpression:
                    EmitUnaryExpression(ilProcessor, (BoundUnaryExpression)node);
                    break;
                case BoundNodeKind.LiteralExpression:
                    EmitLiteralExpression(ilProcessor, (BoundLiteralExpression)node);
                    break;
                case BoundNodeKind.VariableExpression:
                    EmitVariableExpression(ilProcessor, (BoundVariableExpression)node);
                    break;
                case BoundNodeKind.AssignmentExpression:
                    EmitAssignmentExpression(ilProcessor, (BoundAssignmentExpression)node);
                    break;
                case BoundNodeKind.BinaryExpression:
                    EmitBinaryExpression(ilProcessor, (BoundBinaryExpression)node);
                    break;
                case BoundNodeKind.CallExpression:
                    EmitCallExpression(ilProcessor, (BoundCallExpression)node);
                    break;
                case BoundNodeKind.ConversionExpression:
                    EmitConversionExpression(ilProcessor, (BoundConversionExpression)node);
                    break;
                default:
                    throw new Exception($"Unexpected expression: '{node.Kind}'");
            }
        }

        private void EmitConversionExpression(ILProcessor ilProcessor, BoundConversionExpression node)
        {
            throw new NotImplementedException();
        }

        private void EmitCallExpression(ILProcessor ilProcessor, BoundCallExpression node)
        {
            throw new NotImplementedException();
        }

        private void EmitBinaryExpression(ILProcessor ilProcessor, BoundBinaryExpression node)
        {
            throw new NotImplementedException();
        }

        private void EmitAssignmentExpression(ILProcessor ilProcessor, BoundAssignmentExpression node)
        {
            throw new NotImplementedException();
        }

        private void EmitVariableExpression(ILProcessor ilProcessor, BoundVariableExpression node)
        {
            throw new NotImplementedException();
        }

        private void EmitLiteralExpression(ILProcessor ilProcessor, BoundLiteralExpression node)
        {
            throw new NotImplementedException();
        }

        private void EmitUnaryExpression(ILProcessor ilProcessor, BoundUnaryExpression node)
        {
            throw new NotImplementedException();
        }
    }
}