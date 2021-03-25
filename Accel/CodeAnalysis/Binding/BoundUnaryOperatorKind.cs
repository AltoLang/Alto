using System;
using System.Collections.Generic;

using Accel.CodeAnalysis.Syntax;

namespace Accel.CodeAnalysis.Binding
{
    internal enum BoundUnaryOperatorKind
    {
        Indentity,
        Negation,
        LogicalNegation
    }
}