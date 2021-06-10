using System;
using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis
{
    public struct Diagnostic
    {
        public Diagnostic(TextSpan span, string message)
        {
            Span = span;
            Message = message;
        }

        public TextSpan Span { get; }
        public string Message { get; }

        public override string ToString() => Message;

    }
}