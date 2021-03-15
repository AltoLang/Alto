using System;
using System.Collections.Generic;

using compiler.CodeAnalysis.Syntax;

namespace compiler.CodeAnalysis.Binding
{
    internal abstract class BoundNode
    {
        public abstract BoundNodeKind Kind {get;}
    }
}