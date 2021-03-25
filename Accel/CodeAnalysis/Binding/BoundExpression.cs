using System;
using System.Collections.Generic;

using Accel.CodeAnalysis.Syntax;

namespace Accel.CodeAnalysis.Binding
{
    internal abstract class BoundExpression : BoundNode
    {
        public abstract Type Type {get;}
    }
}