using System;
using Accel.CodeAnalysis.Text;

namespace Accel.CodeAnalysis
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