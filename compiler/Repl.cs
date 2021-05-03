using System;
using System.Text;

namespace REPL
{
    internal abstract class Repl
    {
        private readonly StringBuilder _textBuilder = new StringBuilder();

        public void Run()
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                if (_textBuilder.Length == 0)
                    Console.Write("» ");
                else
                    Console.Write("· ");
                Console.ResetColor();

                string input = Console.ReadLine();
                var isBlank = string.IsNullOrWhiteSpace(input);

                if (_textBuilder.Length == 0)
                {
                    if (isBlank)
                    {
                        break;
                    }
                    else if (input.StartsWith("#"))
                    {
                        EvaluateMetaCommand(input);
                    }
                }

                _textBuilder.AppendLine(input);
                var text = _textBuilder.ToString();
                if (!IsCompleteSubmission(text))
                    continue;

                EvaluateSubmission(text);

                _textBuilder.Clear();
            }
        }

        protected virtual void EvaluateMetaCommand(string input)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"Invalid meta command: {input}.");
            Console.ResetColor();
        }

        protected abstract void EvaluateSubmission(string text);

        protected abstract bool IsCompleteSubmission(string text);
    }
}