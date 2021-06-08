namespace Alto.CodeAnalysis.Binding
{
    public sealed class BoundLabel
    {
        internal BoundLabel(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public override string ToString() => Name;
    }
}