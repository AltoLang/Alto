using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis.Syntax
{
    /// <summary>
    /// Represents a node in the syntax tree.
    /// </summary>
    public abstract class SyntaxNode
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyntaxNode"/> class.
        /// </summary>
        /// <param name="syntaxTree"> the parent syntax tree. </param>
        public SyntaxNode(SyntaxTree syntaxTree)
        {
            SyntaxTree = syntaxTree;
        }
        
        public abstract SyntaxKind Kind {get;}

        /// <summary>
        /// Gets the tree that the node is a part of.
        /// </summary>
        public SyntaxTree SyntaxTree { get; }
        
        /// <summary>
        /// Gets or sets the node's position in the code being processed.
        /// </summary>
        public virtual TextSpan Span
        {
            get
            {
                var first = GetChildren().First().Span;
                var last = GetChildren().Last().Span;
                return TextSpan.FromBounds(first.Start, last.End);
            }
        }

        /// <summary>
        /// Gets the location of the node which also contains a information about what source the node is in.
        /// </summary>
        public TextLocation Location => new TextLocation(SyntaxTree.Text, Span);
        
        /// <summary>
        /// Gets all child nodes of this node.
        ///
        public IEnumerable<SyntaxNode> GetChildren()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (typeof(SyntaxNode).IsAssignableFrom(property.PropertyType))
                {
                    var child = (SyntaxNode) property.GetValue(this);
                    if (child != null)
                        yield return child;
                }
                else if (typeof(IEnumerable<SyntaxNode>).IsAssignableFrom(property.PropertyType))
                {
                    var children = (IEnumerable<SyntaxNode>) property.GetValue(this);
                    foreach (var child in children)
                        if (child != null)
                            yield return child;
                }
                else if (typeof(SeparatedSyntaxList).IsAssignableFrom(property.PropertyType))
                {
                    var list = (SeparatedSyntaxList) property.GetValue(this);
                    foreach (var child in list.GetWithSeparators())
                        yield return child;
                }
            }
        }

        /// <summary>
        /// Gets the last child token.
        /// </summary>
        public SyntaxToken GetLastToken()
        {
            if (this is SyntaxToken token)
                return token;

            return GetChildren().Last().GetLastToken();
        }

        /// <sumary>
        /// Writes the node into a text writer as a part of a syntax tree.
        /// </summary>
        public void WriteTo(TextWriter writer)
        {
            PrettyPrint(writer, this);
        }

        private static void PrettyPrint(TextWriter writer, SyntaxNode node, string indent = "", bool isLast = true)
        {
            // └──
            // │
            // ├──

            var isToConsole = writer == Console.Out;

            var marker = isLast ? "└──" : "├──";

            writer.Write(indent);

            if (isToConsole)
                Console.ForegroundColor = ConsoleColor.DarkGray;

            writer.Write(marker);

            WriteNode(writer, node);

            if (isToConsole)
                Console.ResetColor();

            writer.WriteLine();

            //indent += "    ";
            indent += isLast ? "   " : "│   ";

            var lastChild = node.GetChildren().LastOrDefault();

            foreach (var child in node.GetChildren())
                PrettyPrint(writer, child, indent, child == lastChild);
        }

        private static void WriteNode(TextWriter writer, SyntaxNode node)
        {
            // TODO: Handle unary & binary expressions
            // TODO: Change colors
            writer.Write(node.Kind);
        }

        public override string ToString()
        {
            using (var writer = new StringWriter())
            {
                WriteTo(writer);
                return writer.ToString();
            }
        }
    }
}