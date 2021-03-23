using System;
using compiler.CodeAnalysis.Text;

namespace compiler.CodeAnalysis
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