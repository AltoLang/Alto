namespace Alto.CodeAnalysis.Binding
{
    internal class BoundDoWhileStatement : BoundStatement
    {
        public BoundDoWhileStatement(BoundStatement body, BoundExpression condition)
        {
            Body = body;
            Condition = condition;
        }

        public BoundStatement Body { get; }
        public BoundExpression Condition { get; }

        public override BoundNodeKind Kind => BoundNodeKind.DoWhileStatement;
    }
}