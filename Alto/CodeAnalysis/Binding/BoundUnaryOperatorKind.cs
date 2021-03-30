using System;
using System.Collections.Generic;

using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal enum BoundUnaryOperatorKind
    {
        Indentity,
        Negation,
        LogicalNegation
    }
}