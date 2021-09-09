using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Alto
{
    internal abstract class Repl
    {
        private readonly List<MetaCommand> _metaCommands = new List<MetaCommand>();
        private readonly List<string> _submissionHistory = new List<string>();
        private int _submissionHistoryIndex;
        private bool _done;

        protected Repl()
        {
            InitMetaCommands();
        }

        private void InitMetaCommands()
        {
            foreach (var method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                var attribute = method.GetCustomAttribute<MetaCommandAttribute>();
                if (attribute == null)
                    continue;

                var command = new MetaCommand(attribute.Name, attribute.Description, method);
                _metaCommands.Add(command);
            }
        }

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

                _submissionHistory.Add(text);
                _submissionHistoryIndex = 0;
            }
        }

        private sealed class SubmissionView
        {
            private readonly Action<string> _lineRenderer;
            private readonly ObservableCollection<string> _submissionDocument;
            private int _cursorTop;
            private int _renderedLineCount;
            private int _currentLineIndex;
            private int _currentCharacter;

            public SubmissionView(Action<string> lineRenderer, ObservableCollection<string> submissionDocument)
            {
                _lineRenderer = lineRenderer;
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
                    if (_cursorTop + lineCount >= Console.WindowHeight)
                    {
                        Console.SetCursorPosition(0, Console.WindowHeight - 1);
                        Console.WriteLine();
                        if (_cursorTop > 0)
                            _cursorTop--;
                    }

                    Console.SetCursorPosition(0, _cursorTop + lineCount);
                    Console.ForegroundColor = ConsoleColor.Green;
                    
                    if (lineCount == 0)
                        Console.Write("» ");
                    else
                        Console.Write("· ");

                    Console.ResetColor();
                    _lineRenderer(line);
                    Console.Write(new string(' ', Console.WindowWidth - line.Length - 2));
                    lineCount++;
                }

                var numberOfBlankLines = _renderedLineCount - lineCount;
                if (numberOfBlankLines > 0)
                {
                    var blankLine = new string(' ', Console.WindowWidth);
                    for (var i = 0; i < numberOfBlankLines; i++)
                    {
                        Console.SetCursorPosition(0, _cursorTop + lineCount + i);
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
                        _currentCharacter = Math.Min(_submissionDocument[_currentLineIndex].Length, _currentCharacter);
                        UpdateCursorPosition();
                    }
                }
            }
            
            public int CurrentCharacter
            {
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
            var view = new SubmissionView(RenderLine, document);

            while (!_done)
            {
                var key = Console.ReadKey(true);
                HandleKey(key, document, view);
            }

            view.CurrentLineIndex = document.Count - 1;
            view.CurrentCharacter = document[view.CurrentLineIndex].Length;

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
                    case ConsoleKey.Escape:
                        HandleEscape(document, view);
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
                    case ConsoleKey.PageUp:
                        HandlePageUp(document, view);
                        break;
                    case ConsoleKey.PageDown:
                        HandlePageDown(document, view);
                        break;
                    case ConsoleKey.F5:
                        HandleSubmitKey(document, view);
                        break;
                }
            }
            else if (key.Modifiers == ConsoleModifiers.Control)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        InsertLine(document, view);
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
            {
                // merge lines
                if (view.CurrentLineIndex == 0)
                    return;
                
                var currentLine = document[view.CurrentLineIndex];
                var previousLine = document[view.CurrentLineIndex - 1];
                document.RemoveAt(view.CurrentLineIndex);
                view.CurrentLineIndex--;
                document[view.CurrentLineIndex] = previousLine + currentLine;
                view.CurrentCharacter = previousLine.Length;

                view.CurrentLineIndex = document.Count  - previousLine.Length;
            }
            else
            {
                var lineIndex = view.CurrentLineIndex;
                var line = document[lineIndex];
            
                var before = line.Substring(0, start - 1);
                var after = line.Substring(start);
                document[lineIndex] = before + after;
                view.CurrentCharacter--;
            }
        }

        private void HandleDelete(ObservableCollection<string> document, SubmissionView view)
        {
            var lineIndex = view.CurrentLineIndex;
            var line = document[lineIndex];

            var start = view.CurrentCharacter;
            if (start > line.Length - 1)
            {
                if (view.CurrentLineIndex == document.Count - 1)
                    return;

                var nextLine = document[view.CurrentLineIndex + 1];
                document[view.CurrentLineIndex] += nextLine;
                document.RemoveAt(view.CurrentLineIndex + 1);

                return;
            }
            
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

            InsertLine(document, view);
        }

        private void HandleEscape(ObservableCollection<string> document, SubmissionView view)
        {
            document[view.CurrentLineIndex] = string.Empty;
            view.CurrentCharacter = 0;
        }

        private void HandlePageUp(ObservableCollection<string> document, SubmissionView view)
        {
            _submissionHistoryIndex--;
            if (_submissionHistoryIndex < 0)
                _submissionHistoryIndex = _submissionHistory.Count - 1;
            UpdateDocumentFromHistory(document, view);
        }

        private void HandlePageDown(ObservableCollection<string> document, SubmissionView view)
        {
            _submissionHistoryIndex++;
            if (_submissionHistoryIndex > _submissionHistory.Count - 1)
                _submissionHistoryIndex = 0;
            UpdateDocumentFromHistory(document, view);
        }

        private void HandleTab(ObservableCollection<string> document, SubmissionView view) => HandleTyping(document, view, "    ");

        private void HandleSubmitKey(ObservableCollection<string> document, SubmissionView view)
        {
            _done = true;
        }

        private static void InsertLine(ObservableCollection<string> document, SubmissionView view)
        {
            var remainder = document[view.CurrentLineIndex].Substring(view.CurrentCharacter);
            document[view.CurrentLineIndex] = document[view.CurrentLineIndex].Substring(0, view.CurrentCharacter);

            var lineIndex = view.CurrentLineIndex + 1;
            document.Insert(lineIndex, remainder);
            view.CurrentCharacter = 0;
            view.CurrentLineIndex = lineIndex;
        }

        private void UpdateDocumentFromHistory(ObservableCollection<string> document, SubmissionView view)
        {   
            if (_submissionHistory.Count == 0)
                return;
            
            document.Clear();
            var historyItem = _submissionHistory[_submissionHistoryIndex];
            var lines = historyItem.Split(System.Environment.NewLine);
            foreach (var line in lines)
                document.Add(line);

            view.CurrentLineIndex = document.Count - 1;
            view.CurrentCharacter = document[view.CurrentLineIndex].Length;
        }

        protected void ClearHistory()
        {
            _submissionHistory.Clear();
        }

        protected virtual void RenderLine(string line)
        {
            Console.Write(line);
        }

        private void EvaluateMetaCommand(string input)
        {
            // parse args
            var arguments = new List<string>();
            var quoted = false;
            var p = 1;
            var builder = new StringBuilder();
            while (p < input.Length)
            {
                var current = input[p];
                var lookahead = p + 1 >= input.Length ? '\0' : input[p + 1];
                if (char.IsWhiteSpace(current))
                {
                    if (!quoted)
                        CommitArg();
                    else
                        builder.Append(current);
                }
                else if (current == '\"')
                {
                    if (quoted)
                    {
                        quoted = false;
                    }
                    else if (lookahead == '\"')
                    {
                        builder.Append(current);
                        p++;
                    }
                    else
                    {
                        quoted = true;
                    }
                }
                else
                {
                    builder.Append(current);
                }

                p++;
            }

            CommitArg();

            void CommitArg()
            {
                var argument = builder.ToString();
                if (!string.IsNullOrWhiteSpace(argument))
                    arguments.Add(argument);

                builder.Clear();
            }

            var name = arguments[0];

            // removes the command name
            if (arguments.Count > 0)
                arguments.RemoveAt(0);
            
            var cmd = _metaCommands.SingleOrDefault(c => c.Name.ToLower() == name.ToLower()); 
            if (cmd == null)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Invalid meta command: {input}.");
                Console.ResetColor();
                return;
            }

            var parameters = cmd.Method.GetParameters();
            if (arguments.Count != parameters.Length)
            {
                var names = string.Join(", ", parameters.Select(p => $"<{p.Name}>"));
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Invalid argument count.");
                Console.WriteLine($"Usage: #{cmd.Name} {names}");
                Console.ResetColor();
                return;
            }
            
            var instance = cmd.Method.IsStatic ? null : this;
            cmd.Method.Invoke(instance, arguments.ToArray());
        }

        protected abstract void EvaluateSubmission(string text);

        protected abstract bool IsCompleteSubmission(string text);

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        protected sealed class MetaCommandAttribute : Attribute
        {
            public MetaCommandAttribute(string name, string description)
            {
                Name = name;
                Description = description;
            }

            public string Name { get; }
            public string Description { get; }
        }

        private sealed class MetaCommand
        {
            public MetaCommand(string name, string description, MethodInfo method)
            {
                Name = name;
                Description = description;
                Method = method;
            }

            public string Name { get; }
            public string Description { get; }
            public MethodInfo Method { get; }
        }

        [MetaCommand("help", description: "Shows the help menu.")]
        protected void EvaluateHelp()
        {
            int max = 0;
            foreach (var cmd in _metaCommands)
            {
                var length = cmd.Name.Length;
                var parameters = cmd.Method.GetParameters();
                foreach (var parameter in parameters)
                {
                    length += parameter.Name.Length + " <>,".Length;
                    if (parameter != parameters.Last())
                        length += ",".Length;
                }

                if (max < length)
                    max = length;
            }

            Console.WriteLine();
            foreach (var cmd in _metaCommands.OrderBy(c => c.Name))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("#" + cmd.Name);
                
                int paramLength = 0;
                var parameters = cmd.Method.GetParameters();
                foreach (var parameter in parameters)
                {
                    Console.Write(" <");
                    Console.Write(parameter.Name);
                    Console.Write(">");

                    if (parameter != parameters.Last())
                        Console.Write(",");

                    paramLength += parameter.Name.Length + " <>".Length;
                }

                Console.Write(new string(' ', max - cmd.Name.Length - paramLength));

                Console.ResetColor();
                Console.Write(" :  ");

                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.Write(cmd.Description);
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.ResetColor();
        }
    }
}