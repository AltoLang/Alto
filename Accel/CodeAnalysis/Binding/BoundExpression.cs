using System;
using System.Collections.Generic;

using compiler.CodeAnalysis.Syntax;

namespace compiler.CodeAnalysis.Binding
{
    internal abstract class BoundExpression : BoundNode
    {
        public abstract Type Type {get;}
    }
}