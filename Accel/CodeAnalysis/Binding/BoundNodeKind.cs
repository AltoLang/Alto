using System;
using System.Collections.Generic;

using Accel.CodeAnalysis.Syntax;

namespace Accel.CodeAnalysis.Binding
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