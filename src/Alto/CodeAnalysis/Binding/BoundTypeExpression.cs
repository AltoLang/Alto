using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal class BoundTypeExpression : BoundExpression
    {
        public BoundTypeExpression(TypeSymbol type)
        {
            Type = type;
        }

        public override TypeSymbol Type { get; }
        public override BoundNodeKind Kind => BoundNodeKind.TypeExpression;
    }
}