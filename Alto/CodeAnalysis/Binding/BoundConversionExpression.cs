using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundConversionExpression : BoundExpression
    {
        public BoundConversionExpression(TypeSymbol type, BoundExpression expression)
        {
            Type = type;
            Expression = expression;
        }

        public BoundExpression Expression { get; }
        public override TypeSymbol Type { get; }
        public override BoundNodeKind Kind => BoundNodeKind.ConversionExpression;
    }
}