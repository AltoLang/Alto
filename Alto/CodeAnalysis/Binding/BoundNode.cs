using System;
using System.Collections.Generic;

using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal abstract class BoundNode
    {
        public abstract BoundNodeKind Kind {get;}
    }
}