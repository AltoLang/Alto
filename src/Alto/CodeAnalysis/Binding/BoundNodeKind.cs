using System;
using System.Collections.Generic;

using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal enum BoundNodeKind
    {
        // Expressions
        ErrorExpression,
        UnaryExpression,
        LiteralExpression,
        VariableExpression,
        AssignmentExpression,
        BinaryExpression,
        CallExpression,
        ConversionExpression,

        // Statements
        BlockStatement,
        ExpressionStatement,
        VariableDeclaration,
        IfStatement,
        WhileStatement,
        DoWhileStatement,
        ForStatement,
        GotoStatement,
        ConditionalGotoStatement,
        LabelStatement,
        ReturnStatement,
        Statement,
    }
}