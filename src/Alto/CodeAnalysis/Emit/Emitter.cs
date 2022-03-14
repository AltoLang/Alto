using System;
using System.IO;
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
    // Suppresses the warning about nullable reference types
#pragma warning disable CS8632

    internal sealed class Emitter
    {
        private DiagnosticBag _diagnostics = new DiagnosticBag();
        private BoundProgram _program;
        private readonly AssemblyDefinition _assembly;
        private readonly Dictionary<TypeSymbol, TypeReference> _knownTypes;
        private readonly MethodReference _consoleWriteLineReference;
        private readonly MethodReference _consoleReadLineReference;
        private readonly MethodReference _stringConcatReference;
        private readonly MethodReference _objectEqualsReference;
        private readonly MethodReference _convertToBooleanReference;
        private readonly MethodReference _convertToStringReference;
        private readonly MethodReference _convertToInt32Reference;
        private readonly TypeReference _randomReference;
        private readonly MethodReference _randomCtorReference;
        private readonly MethodReference _randomNextReference;
        private  readonly Dictionary<VariableSymbol, VariableDefinition> _locals = new Dictionary<VariableSymbol, VariableDefinition>();
        private readonly List<(int InstructionIndex, BoundLabel Target)> _fixups = new List<(int InstructionIndex, BoundLabel target)>();
        private readonly Dictionary<BoundLabel, int> _labels = new Dictionary<BoundLabel, int>();

        private readonly Dictionary<FunctionSymbol, MethodDefinition> _methods = new();
        private readonly List<TypeMap> _types = new();
        

        private TypeDefinition _typeDefinition;
        private FieldDefinition? _randomFieldDefinition;

        private Emitter(string moduleName, string[] references, ImmutableArray<AssemblyImport> imports)
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
                    // TOOD: Show exception text for debug info
                    _diagnostics.ReportInvalidReference(reference);
                }
            }

            // add import assemblies
            assemblies.AddRange(imports.Select(i => i.Assembly));
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

            _knownTypes = new Dictionary<TypeSymbol, TypeReference>();
            foreach (var (typeSymbol, metadataName) in builtinTypes)
            {
                var typeReference = ResolveType(typeSymbol.Name, metadataName, assemblies);
                if (typeReference is null)
                    Console.WriteLine($"Reference to type {typeSymbol.Name} is null!");
                _knownTypes.Add(typeSymbol, typeReference);
            }

            _consoleWriteLineReference = ResolveMethod("System.Console", "WriteLine", new string[] {"System.Object"}, assemblies);
            _consoleReadLineReference = ResolveMethod("System.Console", "ReadLine", Array.Empty<string>(), assemblies);
            _stringConcatReference = ResolveMethod("System.String", "Concat", new string[] {"System.String", "System.String"}, assemblies);
            _objectEqualsReference = ResolveMethod("System.Object", "Equals", new string[] {"System.Object", "System.Object"}, assemblies);

            _convertToBooleanReference = ResolveMethod("System.Convert", "ToBoolean", new string[] {"System.Object"}, assemblies);
            _convertToStringReference = ResolveMethod("System.Convert", "ToString", new string[] {"System.Object"}, assemblies);
            _convertToInt32Reference = ResolveMethod("System.Convert", "ToInt32", new string[] {"System.Object"}, assemblies);

            _randomReference = ResolveType(null, "System.Random", assemblies);
            _randomCtorReference = ResolveMethod("System.Random", ".ctor", Array.Empty<string>(), assemblies);
            _randomNextReference = ResolveMethod("System.Random", "Next", new [] {"System.Int32", "System.Int32"}, assemblies);

            foreach (var import in imports)
                ResolveImport(import);
        }

        private void ResolveImport(AssemblyImport import)
        {
            var assembly = import.Assembly;
            foreach (var module in assembly.Modules)
            {
                // convert methods to function symbols
                foreach (var type in module.Types)
                {
                    // TOOD: Allow 'Attribute' names
                    if (type.Name == "<Module>" || 
                        type.Name.Contains("Attribute"))
                    {
                        continue;
                    }
                    
                    var functions = new Dictionary<FunctionSymbol, MethodReference>();
                    foreach (var method in type.Methods)
                    {
                        var parameters = new List<ParameterSymbol>();
                        for (int i = 0; i < method.Parameters.Count; i++)
                        {
                            var param = method.Parameters[i];
                            var parameterType = GetTypeSymbolWithAllTypes(param.ParameterType);
                            var paramSymbol = new ParameterSymbol(param.Name, parameterType, i);
                            parameters.Add(paramSymbol);
                        }
                        
                        var retType = GetTypeSymbolWithAllTypes(method.ReturnType);
                        var symbol = new FunctionSymbol(method.Name,
                                                        parameters.ToImmutableArray(),
                                                        retType);
                        var resolved = ResolveMethod(method);
                        functions.Add(symbol, resolved);
                    }

                    var ctor = functions.Where(kvp => kvp.Key.Name == ".ctor").FirstOrDefault().Key;
                    var typeSymbol = new TypeSymbol(type.Name, 
                                                    functions.Select(kvp => kvp.Key).ToImmutableArray(), 
                                                    ImmutableArray<VariableSymbol>.Empty,
                                                    ctor);

                    var importedType = _assembly.MainModule.ImportReference(type);
                    var map = new TypeMap(typeSymbol, type, functions);
                    _types.Add(map);
                }
            }
        }

        internal static ImmutableArray<Diagnostic> Emit(BoundProgram program, string moduleName, string[]   references, string outPath)
        {
            var emitter = new Emitter(moduleName, references, program.Imports);
            return emitter.Emit(program, outPath);
        }
        
        public ImmutableArray<Diagnostic> Emit(BoundProgram program, string outPath)
        {
            if (program.Diagnostics.Any())
                return program.Diagnostics.ToImmutableArray();

            _program = program;

            var objectType = _knownTypes[TypeSymbol.Any];

            _typeDefinition = new TypeDefinition("", "Program", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
            _assembly.MainModule.Types.Add(_typeDefinition);

            foreach (var (function, body) in program.FunctionBodies)
            {
                // imported function
                if (body == null)
                    continue;
                
                EmitFunctionDeclaration(function);
                EmitFunctionBody(function, body);
            }

            if (program.MainFunction != null)
                _assembly.EntryPoint = _methods[program.MainFunction];
            else if (program.ScriptFunction != null)
                _assembly.EntryPoint = _methods[program.ScriptFunction];

            var directory = Directory.GetParent(outPath).FullName;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);     
                   
            _assembly.Write(outPath);

            return _diagnostics.ToImmutableArray();
        }

        private void EmitFunctionDeclaration(FunctionSymbol function)
        {   
            var functionType = _knownTypes[function.Type];
            var method = new MethodDefinition(function.Name, MethodAttributes.Static | MethodAttributes.Private, functionType);

            foreach (var parameter in function.Parameters)
            {
                var parameterType = _knownTypes[parameter.Type];
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
            _fixups.Clear();
            _labels.Clear();

            var ilProcessor = method.Body.GetILProcessor();
            EmitStatement(ilProcessor, body);

            foreach (var fixup in _fixups)
            {
                var targetLabel = fixup.Target;
                var targetInstructionIndex = _labels[targetLabel];
                var targetInstruction = ilProcessor.Body.Instructions[targetInstructionIndex];
                var instructionToFixup =  ilProcessor.Body.Instructions[fixup.InstructionIndex];

                instructionToFixup.Operand = targetInstruction;
            }

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
            _labels.Add(node.Label, ilProcessor.Body.Instructions.Count);
        }

        private void EmitGotoStatement(ILProcessor ilProcessor, BoundGotoStatement node)
        {
            _fixups.Add((ilProcessor.Body.Instructions.Count, node.Label));
            ilProcessor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
        }

        private void EmitConditionalGotoStatement(ILProcessor ilProcessor, BoundConditionalGotoStatement node)
        {
            EmitExpression(ilProcessor, node.Condition);

            var opCode = node.JumpIfTrue ? OpCodes.Brtrue : OpCodes.Brfalse;
            _fixups.Add((ilProcessor.Body.Instructions.Count, node.Label));
            ilProcessor.Emit(opCode, Instruction.Create(OpCodes.Nop));
        }

        private void EmitVariableDeclaration(ILProcessor ilProcessor, BoundVariableDeclaration node)
        {
            var typeReference = _knownTypes[node.Variable.Type];
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
                case BoundNodeKind.MemberAccessExpression:
                    EmitMemberAccessExpression(ilProcessor, (BoundMemberAccessExpression)node);
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
                ilProcessor.Emit(OpCodes.Box, _knownTypes[node.Expression.Type]);

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

        private void EmitMemberAccessExpression(ILProcessor ilProcessor, BoundMemberAccessExpression node)
        {
            // resolve type context
            TypeSymbol typeContext;
            switch (node.Left.Kind)
            {
                case BoundNodeKind.TypeExpression:
                    var typeExp = (BoundTypeExpression)node.Left;
                    typeContext = typeExp.Type;
                    break;
                default:
                    throw new NotImplementedException();
            }

            // TODO: Add contect
            switch (node.Right.Kind)
            {
                case BoundNodeKind.CallExpression:
                    // add type-space context
                    EmitCallExpression(ilProcessor, (BoundCallExpression)node.Right, typeContext);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        ///<param name="typeContext">Type Space to look for the method reference in</param>
        private void EmitCallExpression(ILProcessor ilProcessor, BoundCallExpression node, TypeSymbol typeContext = null)
        {
            if (node.Function == BuiltInFunctions.Random)
            {
                if (_randomFieldDefinition == null)
                    EmitRandomField();

                ilProcessor.Emit(OpCodes.Ldsfld, _randomFieldDefinition);

                foreach (var argument in node.Arguments)
                    EmitExpression(ilProcessor, argument);

                ilProcessor.Emit(OpCodes.Callvirt, _randomNextReference);
                return;
            }

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
            else
            {
                if (_methods.ContainsKey(node.Function))
                {
                    var methodDefinition = _methods[node.Function];
                    ilProcessor.Emit(OpCodes.Call, methodDefinition);
                }
                else
                {
                    if (typeContext == null)
                        throw new Exception("Type context null when calling an import function.");
                    
                    // Imported function
                    var method = GetImportedMethod(node.Function, typeContext);
                    ilProcessor.Emit(OpCodes.Call, method);
                }
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

        private void EmitRandomField()
        {
            _randomFieldDefinition = new FieldDefinition(
                "$rnd",
                FieldAttributes.Static | FieldAttributes.Private,
                _randomReference
            );
            _typeDefinition.Fields.Add(_randomFieldDefinition);

            var staticConstructor = new MethodDefinition(
                ".cctor",
                MethodAttributes.Static |
                MethodAttributes.Private |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                _knownTypes[TypeSymbol.Void]
            );
            _typeDefinition.Methods.Insert(0, staticConstructor);

            var ilProcessor = staticConstructor.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Newobj, _randomCtorReference);
            ilProcessor.Emit(OpCodes.Stsfld, _randomFieldDefinition);
            ilProcessor.Emit(OpCodes.Ret);
        }

        private MethodReference GetImportedMethod(FunctionSymbol function, TypeSymbol typeContext)
        {   
            foreach (var map in _types)
            {
                var method = map.GetMethod(function);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private TypeReference ResolveType(string altoName, string metadataName, List<AssemblyDefinition> assemblies)
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
                Console.WriteLine("foundTypes.Length == 0");
                foreach (var asm in assemblies)
                {
                    Console.WriteLine(asm.Name);
                    // only the temp assembly shows up in `assemblies`
                    // we should also see dotnet runtime assemblies
                    // fix this
                }

                _diagnostics.ReportRequiredTypeNotFound(altoName, metadataName);
            }
            else if (foundTypes.Length > 1)
            {
                Console.WriteLine("foundTypes.Length > 1");
                _diagnostics.ReportRequiredTypeAmbiguous(altoName, metadataName, foundTypes);
            }

            return null;
        }

        private MethodReference ResolveMethod(string typeName, string methodName, string[] parameterTypeNames, List<AssemblyDefinition> assemblies)
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

        private MethodReference ResolveMethod(TypeDefinition type, string methodName, string[] parameterTypeNames)
        {   
            var methods = type.Methods.Where(kvp => kvp.Name == methodName);
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
            
            _diagnostics.ReportRequiredMethodNotFound(type.Name, methodName, parameterTypeNames);
            return null;
        }

        private MethodReference ResolveMethod(MethodDefinition method)
        {   
            return _assembly.MainModule.ImportReference(method);
        }

        internal TypeSymbol GetTypeSymbolWithAllTypes(TypeReference typeRef, bool throwException = true)
        {
            var symbol = GetTypeSymbol(typeRef, throwException: false);
            if (symbol == null)
            {
                symbol = _knownTypes.Where(kvp => kvp.Value == typeRef)
                                        .FirstOrDefault()
                                        .Key;
            }

            if (symbol == null && throwException)
                throw new Exception($"Can't translate type '{typeRef.FullName}' to Alto type.");
            
            return symbol;
        }

        internal static TypeSymbol GetTypeSymbol(TypeReference typeRef, bool throwException = true)
        {
            switch (typeRef.FullName)
            {
                case "System.String":
                    return TypeSymbol.String;
                case "System.Int32":
                    return TypeSymbol.Int;
                case "System.Boolean":
                    return TypeSymbol.Bool;
                case "System.Object":
                    return TypeSymbol.Any;
                case "System.Void":
                    return TypeSymbol.Void;
                default:
                    if (throwException)
                        throw new Exception($"Can't translate C# type '{typeRef.FullName}' to Alto type.");
                    else
                        return null;
            }
        }
    }
}