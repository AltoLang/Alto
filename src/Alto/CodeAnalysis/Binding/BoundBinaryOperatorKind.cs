namespace Alto.CodeAnalysis.Binding
{
    internal enum BoundBinaryOperatorKind
    {
        Addition,
        Subtraction,
        Multiplication,
        Division,
        Modulus,
        BitwiseAND,
        LogicalAND,
        BitwiseOR,
        BitwiseXOR,
        LogicalOR,
        Equals,
        NotEquals,
        LesserThan,
        LesserOrEqualTo,
        GreaterThan,
        GreaterOrEqualTo,
    }
}