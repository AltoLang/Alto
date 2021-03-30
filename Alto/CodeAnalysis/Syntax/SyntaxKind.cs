namespace Alto.CodeAnalysis.Syntax
{
    public enum SyntaxKind
    {
        //Special Tokens
        BadToken,
        EndOfFileToken,
        WhitespaceToken,
        IdentifierToken,

        //Data-Type Tokens
        NumberToken,

        //Operator Tokens
        PlusToken,
        MinusToken,
        StarToken,
        SlashToken,
        PercentageToken,
        OpenParenthesesToken,
        CloseParenthesesToken,  
        BangToken,
        AmpersandAmpersandToken,
        PipePipeToken,
        EqualsEqualsToken,
        BangEqualsToken,
        EqualsToken,

        //Keywords
        FalseKeyword,
        TrueKeyword,

        //Nodes
        CompilationUnit,

        //Expression Tokens     
        LiteralExpression,
        BinaryExpression,
        ParenthesizedExpression,
        UnaryExpression,
        NameExpression,
        AssignmentExpression,
    }
}