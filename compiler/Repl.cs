using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;

namespace REPL
{
    internal abstract class Repl
    {
        private string _submissionText;

        public void Run()
        {
            while (true)
            {
                var text = EditSubmission();
                if (text == null)
                    return;
                
                EvaluateSubmission(text);
            }
        }

        private sealed class SubmissionView
        {
            private readonly ObservableCollection<string> _submissionCocument;
            private readonly int _cursorTop;
            private int _renderedLineCount;
            private int _currentLineIndex;
            private int _currentCharacter;

            public SubmissionView(ObservableCollection<string> submissionCocument)
            {
                _submissionCocument = submissionCocument;
                _submissionCocument.CollectionChanged += SubmissionDocumentChanged;
                _cursorTop = Console.CursorTop;
                Render();
            }

            private void SubmissionDocumentChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                Render();
            }

            private void Render()
            {
                Console.SetCursorPosition(0, _cursorTop);
                Console.CursorVisible = false;

                var lineCount = 0;

                foreach (var line in _submissionCocument)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    if (lineCount == 0)
                        Console.Write("» ");
                    else
                        Console.Write("· ");
                    Console.ResetColor();


                    Console.WriteLine(line);
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
                    if (_currentCharacter != value)
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
            _submissionText = null;
            var document = new ObservableCollection<string>() { "" };
            var view = new SubmissionView(document);

            while (_submissionText == null)
            {
                var key = Console.ReadKey(true);
                HandleKey(key, document, view);
            }

            return _submissionText;
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
            if (view.CurrentCharacter < line.Length - 1)
                view.CurrentCharacter++;
        }

        private void HandleLeftArrow(ObservableCollection<string> document, SubmissionView view)
        {
            if (view.CurrentCharacter > 0)
                view.CurrentCharacter--;
        }

        private void HandleEnter(ObservableCollection<string> document, SubmissionView view)
        {
            var submissionText = string.Join(Environment.NewLine, document);
            if (IsCompleteSubmission(submissionText))
            {
                _submissionText = submissionText;
                return;
            }

            document.Add(string.Empty);
            view.CurrentCharacter = 0;
            view.CurrentLineIndex = document.Count - 1;
        }

        private void HandleRunKey(ObservableCollection<string> document, SubmissionView view)
        {
            _submissionText = string.Join(Environment.NewLine, document);
        }

        private string EditSubmissionOld()
        {        
            var textBuilder = new StringBuilder();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                if (textBuilder.Length == 0)
                    Console.Write("» ");
                else
                    Console.Write("· ");
                Console.ResetColor();

                textBuilder.Clear();
                
                string input = Console.ReadLine();
                var isBlank = string.IsNullOrWhiteSpace(input);

                if (textBuilder.Length == 0)
                {
                    if (isBlank)
                        return null;
                    
                    if (input.StartsWith("#"))
                    {
                        EvaluateMetaCommand(input);
                        return null;
                    }
                }

                textBuilder.AppendLine(input);
                var text = textBuilder.ToString();
                if (!IsCompleteSubmission(text))
                    continue;

                return text;
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