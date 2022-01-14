namespace Alto.CodeAnalysis.Syntax
{
    public enum SyntaxKind
    {
        //Special Tokens
        BadToken,
        EndOfFileToken,
        WhitespaceToken,
        IdentifierToken,
        PreprocessorDirective,
        FullStopToken,

        //Data-Type Tokens
        NumberToken,
        StringToken,

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
        CommaToken,
        ColonToken,
        HashtagToken,
        QuestionMarkToken,

        //Keywords
        FalseKeyword,
        TrueKeyword,
        VarKeyword,
        FunctionKeyword,
        LetKeyword,
        IfKeyword,
        ElseKeyword,
        WhileKeyword,
        ForKeyword,
        ToKeyword,
        DoKeyword,
        BreakKeyword,
        ContinueKeyword,
        ReturnKeyword,
        NewKeyword,
        
        //Nodes
        CompilationUnit,
        GlobalStatement,
        FunctionDeclaration,
        ElseClause,
        TypeClause,
        Parameter,

        //Expression Tokens     
        LiteralExpression,
        BinaryExpression,
        ParenthesizedExpression,
        UnaryExpression,
        NameExpression,
        AssignmentExpression,
        CallExpression,
        ObjectCreationExpression,
        MemberAccessExpression,

        //Statements
        BlockStatement,
        ExpressionStatement,
        VariableDeclaration,
        IfStatement,
        WhileStatement,
        ForStatement,
        DoWhileStatement,
        BreakStatement,
        ContinueStatement,
        ReturnStatement
    }
}