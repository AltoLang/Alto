using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Alto.CodeAnalysis.Binding;
using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis
{
    internal sealed class Evaluator
    {
        private readonly ImmutableDictionary<FunctionSymbol, BoundBlockStatement> _functionBodies;
        private readonly Dictionary<VariableSymbol, object> _globals;
        private readonly Stack<Dictionary<VariableSymbol, object>> _locals = new Stack<Dictionary<VariableSymbol, object>>();
        public BoundBlockStatement _root { get; }
        private Random _random;

        private object _lastValue;

        public Evaluator(ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies, BoundBlockStatement root, Dictionary<VariableSymbol, object> variables)
        {
            _functionBodies = functionBodies;
            _root = root;
            _globals = variables;
            
            _locals.Push(new Dictionary<VariableSymbol, object>());
        }
        public object Evaluate()
        {
            var body = _root;

            return EvaluateStatement(body);
        }

        private object EvaluateStatement(BoundBlockStatement body)
        {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (var i = 0; i < body.Statements.Length; i++)
                if (body.Statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.Label, i + 1);

            var index = 0;
            while (index < body.Statements.Length)
            {
                var s = body.Statements[index];
                
                switch (s.Kind)
                {
                    case BoundNodeKind.VariableDeclaration:
                        EvaluateVariableDeclaration((BoundVariableDeclaration)s);
                        index++;
                        break;
                    case BoundNodeKind.ExpressionStatement:
                        EvaluateExpressionStatement((BoundExpressionStatement)s);
                        index++;
                        break;
                    case BoundNodeKind.GotoStatement:
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.Label];
                        break;
                    case BoundNodeKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = (bool)EvaluateExpression(cgs.Condition);
                        if (condition == cgs.JumpIfTrue)
                            index = labelToIndex[cgs.Label];
                        else
                            index++;
                        break;
                    case BoundNodeKind.LabelStatement:
                        index++;
                        break;
                    default:
                        throw new Exception($"Unexpected node {s.Kind}");
                }
            }
            return _lastValue;
        }

        private void EvaluateVariableDeclaration(BoundVariableDeclaration node)
        {
            var value = EvaluateExpression(node.Initializer);
            _lastValue = value;
            Assign(node.Variable, value);
        }

        private void EvaluateExpressionStatement(BoundExpressionStatement node)
        {
            _lastValue = EvaluateExpression(node.Expression);
        }

        private object EvaluateExpression(BoundExpression node)
        {
            // BinaryExpression
            // NumberExpression
            switch (node.Kind)
            {
                case BoundNodeKind.LiteralExpression:
                    return EvaluateLiteralExpression((BoundLiteralExpression)node);
                case BoundNodeKind.VariableExpression:
                    return EvaluateVariableExpression((BoundVariableExpression)node);
                case BoundNodeKind.AssignmentExpression:
                    return EvaluateAssignmentExpression((BoundAssignmentExpression)node);
                case BoundNodeKind.UnaryExpression:
                    return EvaluateUnaryExpression((BoundUnaryExpression)node);
                case BoundNodeKind.BinaryExpression:
                    return EvaluateBinaryExpression((BoundBinaryExpression)node);
                case BoundNodeKind.CallExpression:
                    return EvaluateCallExpression((BoundCallExpression)node);
                case BoundNodeKind.ConversionExpression:
                    return EvaluateConversionExpression((BoundConversionExpression)node);
            }

            throw new Exception($"Unexpected node {node.Kind}");
        }

        private object EvaluateUnaryExpression(BoundUnaryExpression u)
        {
            var operand = EvaluateExpression(u.Operand);
            switch (u.Op.Kind)
            {
                case BoundUnaryOperatorKind.Indentity:
                    return (int)operand;
                case BoundUnaryOperatorKind.Negation:
                    return -(int)operand;
                case BoundUnaryOperatorKind.LogicalNegation:
                    return !(bool)operand;
                case BoundUnaryOperatorKind.OnesComplement:
                    return ~(int)operand;
                default:
                    throw new Exception($"Unexpected unary operator {u.Op.Kind}");
            }
        }

        private object EvaluateBinaryExpression(BoundBinaryExpression b)
        {
            var left = EvaluateExpression(b.Left);
            var right = EvaluateExpression(b.Right);

            switch (b.Op.Kind)
            {
                case BoundBinaryOperatorKind.Addition:
                    if (b.Type == TypeSymbol.Int)
                        return (int)left + (int)right;
                    else
                        return (string)left + (string)right;
                case BoundBinaryOperatorKind.Subtraction:
                    return (int)left - (int)right;
                case BoundBinaryOperatorKind.Multiplication:
                    return (int)left * (int)right;
                case BoundBinaryOperatorKind.Division:
                    return (int)left / (int)right;
                case BoundBinaryOperatorKind.Modulus:
                    return (int)left % (int)right;
                case BoundBinaryOperatorKind.BitwiseAND:
                    if (b.Type == TypeSymbol.Int)
                        return (int)left & (int)right;
                    else
                        return (bool)left & (bool)right;
                case BoundBinaryOperatorKind.BitwiseOR:
                    if (b.Type == TypeSymbol.Int)
                        return (int)left | (int)right;
                    else
                        return (bool)left | (bool)right;
                case BoundBinaryOperatorKind.BitwiseXOR:
                    if (b.Type == TypeSymbol.Int)
                        return (int)left ^ (int)right;
                    else
                        return (bool)left ^ (bool)right; 
                case BoundBinaryOperatorKind.LogicalAND:
                    return (bool)left && (bool)right;
                case BoundBinaryOperatorKind.LogicalOR:
                    return (bool)left || (bool)right;
                case BoundBinaryOperatorKind.Equals:
                    return Equals(left, right);
                case BoundBinaryOperatorKind.NotEquals:
                    return !Equals(left, right);
                case BoundBinaryOperatorKind.LesserThan:
                    return (int)left < (int)right;
                case BoundBinaryOperatorKind.LesserOrEqualTo:
                    return (int)left <= (int)right;
                case BoundBinaryOperatorKind.GreaterThan:
                    return (int)left > (int)right;
                case BoundBinaryOperatorKind.GreaterOrEqualTo:
                    return (int)left >= (int)right;
                default:
                    throw new Exception($"Unexpected binary operator {b.Op.Kind}");
            }
        }

        private object EvaluateAssignmentExpression(BoundAssignmentExpression a)
        {
            var value = EvaluateExpression(a.Expression);
            Assign(a.Variable, value);

            return value;
        }

        private object EvaluateLiteralExpression(BoundLiteralExpression n)
        {
            return n.Value;
        }
        
        private object EvaluateVariableExpression(BoundVariableExpression v)
        {
            if (v.Variable.Kind == SymbolKind.GlobalVariable)
            {
                var global = _globals[v.Variable];
                return global;
            }
            else
            {
                var locals = _locals.Peek();
                return locals[v.Variable];
            }
        }

        private object EvaluateCallExpression(BoundCallExpression node)
        {
            if (node.Function == BuiltInFunctions.ReadLine)
            {
                return Console.ReadLine();
            }
            else if (node.Function == BuiltInFunctions.Print)
            {
                var expression = (string)EvaluateExpression(node.Arguments[0]);
                Console.WriteLine(expression);
                return null;
            }
            else if (node.Function == BuiltInFunctions.Random)
            {
                if (_random == null)
                    _random = new Random();
                
                var min = (int)EvaluateExpression(node.Arguments[0]);
                var max = (int)EvaluateExpression(node.Arguments[1]);

                return _random.Next(min, max);
            }
            else
            {
                var locals = new Dictionary<VariableSymbol, object>();

                for (var i = 0; i < node.Arguments.Length; i++)
                {
                    var parameter = node.Function.Parameters[i];
                    var value = EvaluateExpression(node.Arguments[i]);
                    locals.Add(parameter, value);
                }

                _locals.Push(locals); 

                var statement = _functionBodies[node.Function];
                var result = EvaluateStatement(statement);
                
                _locals.Pop();
                return result;         
            }
        }

        private object EvaluateConversionExpression(BoundConversionExpression node)
        {
            var value = EvaluateExpression(node.Expression);

            if (node.Type == TypeSymbol.Bool)
                return Convert.ToBoolean(value);
            else if (node.Type == TypeSymbol.Int)
                return Convert.ToInt32(value);
            else if (node.Type == TypeSymbol.String)
                return Convert.ToString(value);
            else
                throw new Exception($"Unexpected type {node.Type}");
        }

        private void Assign(VariableSymbol variable, object value)
        {
            if (variable.Kind == SymbolKind.GlobalVariable)
            {
                _globals[variable] = value;
            }
            else
            {
                var locals = _locals.Peek();
                locals[variable] = value;
            }
        }
    }
}