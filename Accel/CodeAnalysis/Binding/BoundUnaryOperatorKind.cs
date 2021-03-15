using System;
using System.Collections.Generic;

using compiler.CodeAnalysis.Syntax;

namespace compiler.CodeAnalysis.Binding
{
    internal enum BoundUnaryOperatorKind
    {
        Indentity,
        Negation,
        LogicalNegation
    }
}