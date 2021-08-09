using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis.Syntax
{
    public abstract class SyntaxNode
    {
        public SyntaxNode(SyntaxTree syntaxTree)
        {
            SyntaxTree = syntaxTree;
        }

        public abstract SyntaxKind Kind {get;}
        public SyntaxTree SyntaxTree { get; }
        
        public virtual TextSpan Span
        {
            get
            {
                var first = GetChildren().First().Span;
                var last = GetChildren().Last().Span;
                return TextSpan.FromBounds(first.Start, last.End);
            }
        }

        public TextLocation Location => new TextLocation(SyntaxTree.Text, Span);

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

        public SyntaxToken GetLastToken()
        {
            if (this is SyntaxToken token)
                return token;

            return GetChildren().Last().GetLastToken();
        }

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