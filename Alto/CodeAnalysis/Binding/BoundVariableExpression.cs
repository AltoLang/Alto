using System;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class BoundVariableExpression : BoundExpression
    {
        public BoundVariableExpression(VariableSymbol variable)
        {
            Variable = variable;
        }

        public VariableSymbol Variable { get; }
        public override Type Type => Variable.Type;
        public override BoundNodeKind Kind => BoundNodeKind.VariableExpression;
    }
}