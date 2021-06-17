using System.IO;

namespace Alto.CodeAnalysis.Symbols
{
    public abstract class Symbol
    {
        private protected Symbol(string name)
        {
            Name = name;
        }

        public string Name { get; }
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