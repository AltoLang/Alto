using System;
using System.Collections.Generic;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal abstract class BoundExpression : BoundNode
    {
        public abstract TypeSymbol Type {get;}
    }
}