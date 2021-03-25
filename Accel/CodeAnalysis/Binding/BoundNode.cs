using System;
using System.Collections.Generic;

using Accel.CodeAnalysis.Syntax;

namespace Accel.CodeAnalysis.Binding
{
    internal abstract class BoundNode
    {
        public abstract BoundNodeKind Kind {get;}
    }
}