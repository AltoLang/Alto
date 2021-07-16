using System;
using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis
{
    public struct Diagnostic
    {
        public Diagnostic(TextLocation location, string message)
        {
            Location = location;
            Message = message;
        }

        public TextLocation Location { get; }
        public string Message { get; }

        public override string ToString() => Message;

    }
}