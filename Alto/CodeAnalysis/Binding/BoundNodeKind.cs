using System;
using System.Collections.Generic;

using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal enum BoundNodeKind
    {
        // Expressions
        UnaryExpression,
        LiteralExpression,
        VariableExpression,
        AssignmentExpression,
        BinaryExpression,

        // Statements
        BlockStatement,
        ExpressionStatement,
        VariableDeclaration,
        IfStatement,
        WhileStatement,
        ForStatement,
        GotoStatement,
        ConditionalGotoStatement,
        LabelStatement,

        //TEMP
        PrintStatement,
    }
}