namespace Alto.CodeAnalysis.Binding
{
    internal class BoundDoWhileStatement : BoundLoopStatement
    {
        public BoundDoWhileStatement(BoundStatement body, BoundExpression condition, 
            BoundLabel breakLabel, BoundLabel continueLabel) : base(breakLabel, continueLabel)
        {
            Body = body;
            Condition = condition;
        }

        public BoundStatement Body { get; }
        public BoundExpression Condition { get; }
        public override BoundNodeKind Kind => BoundNodeKind.DoWhileStatement;
    }
}