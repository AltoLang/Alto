namespace Alto.CodeAnalysis
{
    public sealed class LabelSymbol
    {
        internal LabelSymbol(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}