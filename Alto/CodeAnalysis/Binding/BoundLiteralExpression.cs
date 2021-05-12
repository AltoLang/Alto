using System;
using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundLiteralExpression : BoundExpression
    {
        public BoundLiteralExpression(object value)
        {
            Value = value;
            
            if (value is int)
                Type = TypeSymbol.Int;
            else if (value is bool)
                Type = TypeSymbol.Bool;
            else if (value is string)
                Type = TypeSymbol.Int;
            else
                throw new Exception($"Unexpected literal '{value} of type '{value.GetType()}'.");
        }
        public override BoundNodeKind Kind => BoundNodeKind.LiteralExpression;
        public override TypeSymbol Type { get; }
        public object Value { get; }
    }
}