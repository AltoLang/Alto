namespace Alto.CodeAnalysis.Binding
{
    internal class BoundPrintStatement : BoundStatement
    {
        public BoundExpression Print {get;}

        public override BoundNodeKind Kind => BoundNodeKind.PrintStatement;

        public BoundPrintStatement(BoundExpression print)
        {
            Print = print;
        }
    }
}