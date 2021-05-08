using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;

namespace REPL
{
    internal abstract class Repl
    {
        private bool _done;

        public void Run()
        {
            while (true)
            {
                var text = EditSubmission();
                if (string.IsNullOrEmpty(text))
                    return;

                if (!text.Contains(System.Environment.NewLine) && text.StartsWith("#"))
                    EvaluateMetaCommand(text);
                else
                    EvaluateSubmission(text);
            }
        }

        private sealed class SubmissionView
        {
            private readonly ObservableCollection<string> _submissionDocument;
            private readonly int _cursorTop;
            private int _renderedLineCount;
            private int _currentLineIndex;
            private int _currentCharacter;

            public SubmissionView(ObservableCollection<string> submissionDocument)
            {
                _submissionDocument = submissionDocument;
                _submissionDocument.CollectionChanged += SubmissionDocumentChanged;
                _cursorTop = Console.CursorTop;
                Render();
            }

            private void SubmissionDocumentChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                Render();
            }

            private void Render()
            {
                Console.CursorVisible = false;

                var lineCount = 0;

                foreach (var line in _submissionDocument)
                {
                    Console.SetCursorPosition(0, _cursorTop + lineCount);
                    Console.ForegroundColor = ConsoleColor.Green;
                    if (lineCount == 0)
                        Console.Write("» ");
                    else
                        Console.Write("· ");
                    Console.ResetColor();

                    Console.WriteLine(line + new string(' ', Console.WindowWidth - line.Length));
                    lineCount++;
                }

                var numberOfBlankLines = _renderedLineCount - lineCount;
                if (numberOfBlankLines > 0)
                {
                    var blankLine = new string(' ', Console.WindowWidth);
                    while (numberOfBlankLines > 0)
                    {
                        Console.WriteLine(blankLine);
                    }
                }

                _renderedLineCount = lineCount;

                Console.CursorVisible = true;
                UpdateCursorPosition();
            }

            private void UpdateCursorPosition()
            {
                Console.CursorTop = _cursorTop + _currentLineIndex;
                Console.CursorLeft = 2 + _currentCharacter;
            }

            public int CurrentLineIndex 
            { 
                get => _currentLineIndex; 
                set 
                {
                    if (_currentLineIndex != value)
                    {
                        _currentLineIndex = value;
                        UpdateCursorPosition();
                    }
                }
            }
            
            public int CurrentCharacter { 
                get => _currentCharacter; 
                set 
                {
                    if (_currentCharacter != value)
                    {
                        _currentCharacter = value; 
                        UpdateCursorPosition();
                    }
                } 
            }
        }

        private string EditSubmission()
        {
            _done = false;
            var document = new ObservableCollection<string>() { "" };
            var view = new SubmissionView(document);

            while (!_done)
            {
                var key = Console.ReadKey(true);
                HandleKey(key, document, view);
            }

            Console.WriteLine();

            if (document.Count == 1 && document[0].Length == 0)
                return null;

            var result = string.Join(Environment.NewLine, document);
            return result;
        }

        private void HandleKey(ConsoleKeyInfo key, ObservableCollection<string> document, SubmissionView view)
        {
            if (key.Modifiers == default(ConsoleModifiers))
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        HandleEnter(document, view);
                        break;
                    case ConsoleKey.Tab:
                        HandleTab(document, view);
                        break;
                    case ConsoleKey.LeftArrow:
                        HandleLeftArrow(document, view);
                        break;
                    case ConsoleKey.RightArrow:
                        HandleRightArrow(document, view);
                        break;
                    case ConsoleKey.UpArrow:
                        HandleUpArrow(document, view);
                        break;
                    case ConsoleKey.DownArrow:
                        HandleDownArrow(document, view);
                        break;
                    case ConsoleKey.Backspace:
                        HandleBackspace(document, view);
                        break;
                    case ConsoleKey.Delete:
                        HandleDelete(document, view);
                        break;
                    case ConsoleKey.Home:
                        HandleHome(document, view);
                        break;
                    case ConsoleKey.End:
                        HandleEnd(document, view);
                        break;
                    case ConsoleKey.F5:
                        HandleRunKey(document, view);
                        break;
                }
            }
            else if (key.Modifiers == ConsoleModifiers.Control)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        HandleRunKey(document, view);
                        break;
                }
            }

            if (key.KeyChar >= ' ')
                HandleTyping(document, view, key.KeyChar.ToString());
        }

        private void HandleTyping(ObservableCollection<string> document, SubmissionView view, string text)
        {
            var lineIndex = view.CurrentLineIndex;
            var start = view.CurrentCharacter;
            document[lineIndex] = document[lineIndex].Insert(start, text);
            view.CurrentCharacter += text.Length;
        }

        private void HandleDownArrow(ObservableCollection<string> document, SubmissionView view)
        {
            if (view.CurrentLineIndex < document.Count -1)
                view.CurrentLineIndex++;
        }

        private void HandleUpArrow(ObservableCollection<string> document, SubmissionView view)
        {
            if (view.CurrentLineIndex > 0)
                view.CurrentLineIndex--;
        }

        private void HandleRightArrow(ObservableCollection<string> document, SubmissionView view)
        {
            var line = document[view.CurrentLineIndex];
            if (view.CurrentCharacter <= line.Length - 1)
                view.CurrentCharacter++;
        }

        private void HandleLeftArrow(ObservableCollection<string> document, SubmissionView view)
        {
            if (view.CurrentCharacter > 0)
                view.CurrentCharacter--;
        }

        private void HandleBackspace(ObservableCollection<string> document, SubmissionView view)
        {
            var start = view.CurrentCharacter;
            if (start == 0)
                return;
                
            var lineIndex = view.CurrentLineIndex;
            var line = document[lineIndex];
            
            var before = line.Substring(0, start - 1);
            var after = line.Substring(start);
            document[lineIndex] = before + after;
            view.CurrentCharacter--;
        }

        private void HandleDelete(ObservableCollection<string> document, SubmissionView view)
        {
            var lineIndex = view.CurrentLineIndex;
            var line = document[lineIndex];

            var start = view.CurrentCharacter;
            if (start > line.Length - 1)
                return;
            
            var before = line.Substring(0, start);
            var after = line.Substring(start + 1);
            document[lineIndex] = before + after;
        }

        private void HandleEnd(ObservableCollection<string> document, SubmissionView view)
        {
            view.CurrentCharacter = document[view.CurrentLineIndex].Length;
        }

        private void HandleHome(ObservableCollection<string> document, SubmissionView view)
        {
            view.CurrentCharacter = 0;
        }

        private void HandleEnter(ObservableCollection<string> document, SubmissionView view)
        {
            var submissionText = string.Join(Environment.NewLine, document);
            if (submissionText.StartsWith("#") || IsCompleteSubmission(submissionText))
            {
                _done = true;
                return;
            }

            document.Add(string.Empty);
            view.CurrentCharacter = 0;
            view.CurrentLineIndex = document.Count - 1;
        }

        private void HandleTab(ObservableCollection<string> document, SubmissionView view) => HandleTyping(document, view, "    ");

        private void HandleRunKey(ObservableCollection<string> document, SubmissionView view)
        {
            _done = true;
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