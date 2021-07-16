using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Syntax;
using Alto.CodeAnalysis.Text;

namespace Alto.IO
{
    public static class DiagnosticsWriter
    {
        public static void WriteDiagnostics(TextWriter writer, IEnumerable<Diagnostic> diagnostics, SyntaxTree syntaxTree) {
            var isToConsole = writer.IsConsoleOut();

            foreach (var diagnostic in diagnostics.OrderBy(d => d.Location.Text.FileName)
                                                  .ThenBy(d => d.Location.Span.Start)
                                                  .ThenBy(d => d.Location.Span.Length))
            {
                var lineIndex = syntaxTree.Text.GetLineIndex(diagnostic.Location.Span.Start);
                var lineNumber = lineIndex + 1;
                var line = syntaxTree.Text.Lines[lineIndex];
                var character = diagnostic.Location.Span.Start - line.Start;

                writer.WriteLine();
                if (isToConsole)
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                writer.Write($"({lineNumber}, {character}): ");
                writer.WriteLine(diagnostic);
                writer.ResetColor();

                var prefixSpan = TextSpan.FromBounds(line.Start, diagnostic.Location.Span.Start);
                var suffixSpan = TextSpan.FromBounds(diagnostic.Location.Span.End, line.End);

                var prefix = syntaxTree.Text.ToString(prefixSpan);
                var error = syntaxTree.Text.ToString(diagnostic.Location.Span);
                var suffix = syntaxTree.Text.ToString(suffixSpan);

                writer.Write("    ");
                writer.Write(prefix);
                
                if (isToConsole)
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                writer.Write(error);
                writer.ResetColor();

                writer.Write(suffix);

                writer.WriteLine();
            }

            Console.WriteLine();
        }
    }
}