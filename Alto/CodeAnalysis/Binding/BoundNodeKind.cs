using System;
using System.Collections.Generic;

using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal enum BoundNodeKind
    {
        UnaryExpression,
        LiteralExpression,
        VariableExpression,
        AssignmentExpression,
        BinaryExpression
    }
}