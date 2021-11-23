using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alto.CodeAnalysis.Binding;
using Alto.CodeAnalysis.Symbols;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Alto.CodeAnalysis.Emit
{
    internal sealed class Emitter
    {
        private DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly AssemblyDefinition _assembly;
        private readonly Dictionary<TypeSymbol, TypeReference> _knowsTypes;
        private readonly MethodReference _consoleWriteLineReference;
        private readonly MethodReference _consoleReadLineReference;
        private readonly MethodReference _stringConcatReference;
        private readonly MethodReference _objectEqualsReference;
        private readonly MethodReference _convertToBooleanReference;
        private readonly MethodReference _convertToStringReference;
        private readonly MethodReference _convertToInt32Reference;
        private  readonly Dictionary<VariableSymbol, VariableDefinition> _locals = new Dictionary<VariableSymbol, VariableDefinition>();
        private readonly Dictionary<FunctionSymbol, MethodDefinition> _methods = new Dictionary<FunctionSymbol, MethodDefinition>();

        private TypeDefinition _typeDefinition;

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
            _consoleReadLineReference = ResolveMethod("System.Console", "ReadLine", Array.Empty<string>());
            _stringConcatReference = ResolveMethod("System.String", "Concat", new string[] {"System.String", "System.String"});
            _objectEqualsReference = ResolveMethod("System.Object", "Equals", new string[] {"System.Object", "System.Object"});

            _convertToBooleanReference = ResolveMethod("System.Convert", "ToBoolean", new string[] {"System.Object"});
            _convertToStringReference = ResolveMethod("System.Convert", "ToString", new string[] {"System.Object"});
            _convertToInt32Reference = ResolveMethod("System.Convert", "ToInt32", new string[] {"System.Object"});
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
            var functionType = _knowsTypes[function.Type];
            var method = new MethodDefinition(function.Name, MethodAttributes.Static | MethodAttributes.Private, functionType);

            foreach (var parameter in function.Parameters)
            {
                var parameterType = _knowsTypes[parameter.Type];
                var parameterAttributes = ParameterAttributes.None;
                var parameterDefinition = new ParameterDefinition(parameter.Name, parameterAttributes, parameterType);
                method.Parameters.Add(parameterDefinition);
            }

            _typeDefinition.Methods.Add(method);
            _methods.Add(function, method);
        }

        private void EmitFunctionBody(FunctionSymbol function, BoundBlockStatement body)
        {
            var method = _methods[function];
            _locals.Clear();

            var ilProcessor = method.Body.GetILProcessor();
            EmitStatement(ilProcessor, body);

            method.Body.OptimizeMacros();
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
                EmitStatement(ilProcessor, childStatement);
        }

        private void EmitReturnStatement(ILProcessor ilProcessor, BoundReturnStatement node)
        {
            if (node.ReturnExpression != null)
                EmitExpression(ilProcessor, node.ReturnExpression);
            
            ilProcessor.Emit(OpCodes.Ret);
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

        private void EmitVariableDeclaration(ILProcessor ilProcessor, BoundVariableDeclaration node)
        {
            var typeReference = _knowsTypes[node.Variable.Type];
            var variableDefinition = new VariableDefinition(typeReference);
            _locals.Add(node.Variable, variableDefinition);
            ilProcessor.Body.Variables.Add(variableDefinition);

            EmitExpression(ilProcessor, node.Initializer);
            ilProcessor.Emit(OpCodes.Stloc, variableDefinition.Index);
        }

        private void EmitExpressionStatement(ILProcessor ilProcessor, BoundExpressionStatement node)
        {
            EmitExpression(ilProcessor, node.Expression);

            if (node.Expression.Type != TypeSymbol.Void)
                ilProcessor.Emit(OpCodes.Pop);
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
            EmitExpression(ilProcessor, node.Expression);

            var needsBoxing = node.Expression.Type == TypeSymbol.Int || node.Expression.Type == TypeSymbol.Bool;
            if (needsBoxing)
                ilProcessor.Emit(OpCodes.Box, _knowsTypes[node.Expression.Type]);

            if (node.Type == TypeSymbol.Any)
            {
                // already handled
                return;
            }
            else if (node.Type == TypeSymbol.Bool)
            {
                ilProcessor.Emit(OpCodes.Call, _convertToBooleanReference);
            }
            else if (node.Type == TypeSymbol.Int)
            {
                ilProcessor.Emit(OpCodes.Call, _convertToInt32Reference);
            }
            else if (node.Type == TypeSymbol.String)
            {
                ilProcessor.Emit(OpCodes.Call, _convertToStringReference);
            }
            else
            {
                throw new Exception($"Unexpected conversion from '{node.Expression.Type}' to '{node.Type}'.");
            }
        }

        private void EmitCallExpression(ILProcessor ilProcessor, BoundCallExpression node)
        {
            foreach (var arg in node.Arguments)
                EmitExpression(ilProcessor, arg);
            
            if (node.Function == BuiltInFunctions.Print)
            {
                ilProcessor.Emit(OpCodes.Call, _consoleWriteLineReference);
            }
            else if (node.Function == BuiltInFunctions.ReadLine)
            {
                ilProcessor.Emit(OpCodes.Call, _consoleReadLineReference);
            }
            else if (node.Function == BuiltInFunctions.Random)
            {
                throw new NotImplementedException();
            }
            else
            {
                var methodDefinition = _methods[node.Function];
                ilProcessor.Emit(OpCodes.Call, methodDefinition);
            }
        }

        private void EmitBinaryExpression(ILProcessor ilProcessor, BoundBinaryExpression node)
        {
            EmitExpression(ilProcessor, node.Left);
            EmitExpression(ilProcessor, node.Right);

            // String concatenation
            if (node.Op.Kind == BoundBinaryOperatorKind.Addition)
            {
                if (node.Left.Type == TypeSymbol.String && node.Right.Type == TypeSymbol.String)
                {
                    ilProcessor.Emit(OpCodes.Call, _stringConcatReference);
                    return;
                }
            }

            if (node.Op.Kind == BoundBinaryOperatorKind.Equals)
            {
                // (String || Any) equality
                if (node.Left.Type == TypeSymbol.Any && node.Right.Type == TypeSymbol.Any ||
                    node.Left.Type == TypeSymbol.String && node.Right.Type == TypeSymbol.String)
                {
                    ilProcessor.Emit(OpCodes.Call, _objectEqualsReference);
                    return;
                }
            }

            if (node.Op.Kind == BoundBinaryOperatorKind.NotEquals) 
            {
                // (String || Any) equality
                if (node.Left.Type == TypeSymbol.Any && node.Right.Type == TypeSymbol.Any ||
                    node.Left.Type == TypeSymbol.String && node.Right.Type == TypeSymbol.String)
                {
                    ilProcessor.Emit(OpCodes.Call, _objectEqualsReference);
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    return;
                }
            }

            
            switch (node.Op.Kind)
            {
                case BoundBinaryOperatorKind.Addition:
                    ilProcessor.Emit(OpCodes.Add);
                    break;
                case BoundBinaryOperatorKind.Subtraction:
                    ilProcessor.Emit(OpCodes.Sub);
                    break;
                case BoundBinaryOperatorKind.Multiplication:
                    ilProcessor.Emit(OpCodes.Mul);
                    break;
                case BoundBinaryOperatorKind.Division:
                    ilProcessor.Emit(OpCodes.Div);
                    break;
                case BoundBinaryOperatorKind.Modulus:
                    ilProcessor.Emit(OpCodes.Rem);
                    break;
                // TODO: Short-circuit evaluation
                case BoundBinaryOperatorKind.BitwiseAND:
                case BoundBinaryOperatorKind.LogicalAND:
                    ilProcessor.Emit(OpCodes.And);
                    break;
                // TODO: Short-circuit evaluation
                case BoundBinaryOperatorKind.BitwiseOR:
                case BoundBinaryOperatorKind.LogicalOR:
                    ilProcessor.Emit(OpCodes.Or);
                    break;
                case BoundBinaryOperatorKind.BitwiseXOR:
                    ilProcessor.Emit(OpCodes.Xor);
                    break;
                case BoundBinaryOperatorKind.Equals:
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorKind.NotEquals:
                    ilProcessor.Emit(OpCodes.Or);
                    break;
                case BoundBinaryOperatorKind.LesserThan:
                    ilProcessor.Emit(OpCodes.Clt);
                    break;
                case BoundBinaryOperatorKind.LesserOrEqualTo:
                    ilProcessor.Emit(OpCodes.Cgt);
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorKind.GreaterThan:
                    ilProcessor.Emit(OpCodes.Cgt);
                    break;
                case BoundBinaryOperatorKind.GreaterOrEqualTo:
                    ilProcessor.Emit(OpCodes.Clt);
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                default:
                    throw new Exception($"Unexpected binary operator '{node.Op.Kind}'.");
            }
        }

        private void EmitAssignmentExpression(ILProcessor ilProcessor, BoundAssignmentExpression node)
        {
            var variableDefinition = _locals[node.Variable];
            EmitExpression(ilProcessor, node.Expression);
            ilProcessor.Emit(OpCodes.Dup);
            ilProcessor.Emit(OpCodes.Stloc, variableDefinition);
        }

        private void EmitVariableExpression(ILProcessor ilProcessor, BoundVariableExpression node)
        {
            if (node.Variable is ParameterSymbol p)
            {
                ilProcessor.Emit(OpCodes.Ldarg, p.Ordinal);
            }
            else
            {
                var variableDefinition = _locals[node.Variable];
                ilProcessor.Emit(OpCodes.Ldloc, variableDefinition);
            }
        }

        private void EmitLiteralExpression(ILProcessor ilProcessor, BoundLiteralExpression node)
        {
            
            if (node.Type == TypeSymbol.Int)
            {
                var value = (int)node.Value;
                ilProcessor.Emit(OpCodes.Ldc_I4, value);
            }
            else if (node.Type == TypeSymbol.Bool)
            {
                var value = (bool)node.Value;
                if (value == true)
                    ilProcessor.Emit(OpCodes.Ldc_I4_1);
                else
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
            }
            else if (node.Type == TypeSymbol.String)
            {
                var value = (string)node.Value;
                ilProcessor.Emit(OpCodes.Ldstr, value);
            }
            else
            {
                throw new Exception($"Unexpected literal expression type: '{node.Type}'");
            }
        }

        private void EmitUnaryExpression(ILProcessor ilProcessor, BoundUnaryExpression node)
        {   
            EmitExpression(ilProcessor, node.Operand);

            switch (node.Op.Kind)
            {
                case BoundUnaryOperatorKind.Indentity:
                    break;
                case BoundUnaryOperatorKind.LogicalNegation:
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                case BoundUnaryOperatorKind.Negation:
                    ilProcessor.Emit(OpCodes.Neg);
                    break;
                case BoundUnaryOperatorKind.OnesComplement:
                    ilProcessor.Emit(OpCodes.Not);
                    break;
                default:
                    throw new Exception($"Unexpected unary operator '{node.Op.Kind}'.");
            }
        }
    }
}