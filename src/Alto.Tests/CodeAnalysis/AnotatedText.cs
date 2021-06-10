using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Alto.CodeAnalysis.Text;

namespace Alto.Tests.CodeAnalysis
{
    internal sealed class AnnotatedText
    {
        public AnnotatedText(string text, ImmutableArray<TextSpan> spans)
        {
            Text = text;
            Spans = spans;
        }

        public string Text { get; }
        public ImmutableArray<TextSpan> Spans { get; }

        public static AnnotatedText Parse(string text)
        {
            text = Unindent(text);

            var textBuilder = new StringBuilder();
            var spanBuilder = ImmutableArray.CreateBuilder<TextSpan>();
            var startStack = new Stack<int>();

            var position = 0;

            foreach (var c in text)
            {
                switch (c)
                {
                    case '[':
                        startStack.Push(position);
                        break;
                    case ']':
                        if (startStack.Count == 0)
                            throw new ArgumentException("Too many ']' in text.", nameof(text));

                        var start = startStack.Pop();
                        var end = position;
                        var span = TextSpan.FromBounds(start, end);
                        spanBuilder.Add(span);

                        break;
                    default:
                        position++;
                        textBuilder.Append(c);
                        break;
                }
            }

            if (startStack.Count != 0)
                throw new ArgumentException("Missing ']' in text.", nameof(text));

            return new AnnotatedText(textBuilder.ToString(), spanBuilder.ToImmutable());
        }

        private static string Unindent(string text)
        {
            var lines = UnindentLines(text);

            return string.Join(System.Environment.NewLine, lines);
        }

        public static string[] UnindentLines(string text)
        {
            var lines = new List<string>();
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    lines.Add(line);
            }

            var minIndent = int.MaxValue;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.Trim().Length == 0)
                {
                    lines[i] = string.Empty;
                    continue;
                }

                var indent = line.Length - line.TrimStart().Length;
                minIndent = Math.Min(minIndent, indent);
            }

            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Length == 0)
                    continue;
                lines[i] = lines[i].Substring(minIndent);
            }

            while (lines.Count > 0 && lines[0].Length == 0)
                lines.RemoveAt(0);

            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            
            return lines.ToArray();
        }
    }
}
