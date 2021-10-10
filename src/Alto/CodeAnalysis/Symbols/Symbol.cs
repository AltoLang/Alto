using System.IO;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Symbols
{
    public abstract class Symbol
    {
        private protected Symbol(string name, SyntaxTree tree = null)
        {
            Name = name;
            Tree = tree;
        }

        public string Name { get; }
        public SyntaxTree Tree { get; }
        public abstract SymbolKind Kind { get; }
        public void WriteTo(TextWriter writer) => SymbolWriter.WriteTo(this, writer);

        public override string ToString()
        {
            using var writer = new StringWriter();
            WriteTo(writer);
            return writer.ToString();
        }
    }
}