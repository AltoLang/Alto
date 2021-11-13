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
        public static void WriteDiagnostics(TextWriter writer, IEnumerable<Diagnostic> diagnostics) {
            var isToConsole = writer.IsConsoleOut();

            foreach (var diagnostic in diagnostics.Where(d => d.Location.Text == null))
            {
                if (isToConsole)
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    
                writer.WriteLine(diagnostic.Message); 
                writer.ResetColor();
            }

            foreach (var diagnostic in diagnostics.Where(d => d.Location.Text != null)
                                                  .OrderBy(d => d.Location.FileName)
                                                  .ThenBy(d => d.Location.Span.Start)
                                                  .ThenBy(d => d.Location.Span.Length))
            {
                var text = diagnostic.Location.Text;
                var file = diagnostic.Location.FileName;
                var span = diagnostic.Location.Span;
                var lineIndex = text.GetLineIndex(span.Start);
                var lineNumber = lineIndex + 1;
                var line = text.Lines[lineIndex];
                var character = span.Start - line.Start;

                var startLine = diagnostic.Location.StartLine + 1;
                var startCharacter = diagnostic.Location.StartCharacter + 1;
                var endLine = diagnostic.Location.EndLine + 1;
                var endCharacter = diagnostic.Location.EndCharacter + 1;

                writer.WriteLine();
                if (isToConsole)
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                
                if (file == "")
                    writer.Write($"({startLine},{startCharacter},{endLine},{endCharacter}): ");
                else
                    writer.Write($"{file}({startLine},{startCharacter},{endLine},{endCharacter}): ");
                
                writer.WriteLine(diagnostic);
                writer.ResetColor();

                var prefixSpan = TextSpan.FromBounds(line.Start, span.Start);
                var suffixSpan = TextSpan.FromBounds(span.End, line.End);

                var prefix = text.ToString(prefixSpan);
                var error = text.ToString(span);
                var suffix = text.ToString(suffixSpan);

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