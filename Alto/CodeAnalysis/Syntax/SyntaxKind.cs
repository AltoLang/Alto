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
        OpenBraceToken,
        CloseBraceToken,
        BangToken,
        AmpersandAmpersandToken,
        PipePipeToken,
        EqualsEqualsToken,
        BangEqualsToken,
        EqualsToken,
        LesserOrEqualsToken,
        LesserToken,
        GreaterToken,
        GreaterOrEqualsToken,
        TildeToken,
        AmpersandToken,
        PipeToken,
        HatToken,

        //Keywords
        FalseKeyword,
        TrueKeyword,
        VarKeyword,
        LetKeyword,
        IfKeyword,
        ElseKeyword,
        WhileKeyword,
        ForKeyword,
        ToKeyword,
        
        //Nodes
        CompilationUnit,
        ElseClause,

        //Expression Tokens     
        LiteralExpression,
        BinaryExpression,
        ParenthesizedExpression,
        UnaryExpression,
        NameExpression,
        AssignmentExpression,

        //Statements
        BlockStatement,
        ExpressionStatement,
        VariableDeclaration,
        IfStatement,
        WhileStatement,
        ForStatement,

        //Temp
        PrintKeyword,
        PrintStatement,
    }
}