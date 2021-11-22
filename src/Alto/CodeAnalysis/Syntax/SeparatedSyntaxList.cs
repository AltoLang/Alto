using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Alto.CodeAnalysis.Syntax
{
    /// <summary>
    /// Defines the core behavior of a collection of syntax nodes.
    /// <summary>
    public abstract class SeparatedSyntaxList
    {
        public abstract ImmutableArray<SyntaxNode> GetWithSeparators();
    }

    /// <summary>
    /// Represents a collection of syntax nodes. This class cannot be inherited.
    /// </summary>
    public sealed class SeparatedSyntaxList<T> : SeparatedSyntaxList, IEnumerable<T> where T: SyntaxNode
    {
        private readonly ImmutableArray<SyntaxNode> _nodesAndSeparators;

        /// <summary>
        /// Initializes a new <see cref="SeparatedSyntaxList">.
        /// </summary>
        /// <param name="nodesAndSeparators">The initial syntax nodes to initialize the collection for.</param>
        public SeparatedSyntaxList(ImmutableArray<SyntaxNode> nodesAndSeparators)
        {
            _nodesAndSeparators = nodesAndSeparators;
        }

        /// <summary>
        /// Gets the numbers of syntax nodes present in the collection.
        /// </summary>
        public int Count => (_nodesAndSeparators.Length + 1) / 2;
        public T this[int index] => (T)_nodesAndSeparators[index * 2];

        /// <summary>
        /// Gets all separators in the collection.
        /// </summary>
        public SyntaxToken GetSeparator(int index)
        {
            if (index == Count - 1)
                return null;
            
            return (SyntaxToken)_nodesAndSeparators[index * 2 + 1];
        }

        /// <summary>
        /// Gets all nodes and separators present in the collection.
        /// </summary>
        public override ImmutableArray<SyntaxNode> GetWithSeparators() => _nodesAndSeparators;

        /// <summary>
        /// Returns the enumerator for iteration over the collection.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}