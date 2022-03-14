using Alto.CodeAnalysis.Symbols;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundMemberAccessExpression : BoundExpression
    {
        public BoundMemberAccessExpression(BoundExpression left, BoundExpression right)
        {
            Left = left;
            Right = right;
        }

        public override TypeSymbol Type => Right.Type;
        public override BoundNodeKind Kind => BoundNodeKind.MemberAccessExpression;
        public BoundExpression Left { get; }
        public BoundExpression Right { get; }
    }
}