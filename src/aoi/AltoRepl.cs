using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;
using Alto.IO;
using Newtonsoft.Json;

namespace Alto
{
    internal sealed class AltoRepl : Repl
    {
        private readonly StringBuilder _textBuilder = new StringBuilder();
        private Compilation _previous;
        private bool _showTree = false;
        private bool _showProgram = false;
        private bool _loadingSubmissions = false;
        private static readonly Compilation emptyCompilation = Compilation.CreateScript(null, null);
        private readonly Dictionary<VariableSymbol, object> _variables = new Dictionary<VariableSymbol, object>();

        public AltoRepl()
        {
            LoadSubmissions();
        }

        protected override void EvaluateSubmission(string text)
        {
            var config = GetConfig();

            Console.Title = text;
            var syntaxTree = SyntaxTree.Parse(text);
            Compilation compilation = Compilation.CreateScript(_previous, syntaxTree);

            if (_showTree)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                syntaxTree.Root.WriteTo(Console.Out);
                Console.ResetColor();
            }

            if (_showProgram)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                compilation.EmitTree(Console.Out);
                Console.ResetColor();
            }

            string programFolderPath = CreateBuildPrerequisites();

            var netCoreRefPath = config.NETCorePath;
            string[] references = new string[] {
                Path.Combine(netCoreRefPath, "ref/net6.0/System.Runtime.dll"),
                Path.Combine(netCoreRefPath, "ref/net6.0/System.Runtime.Extensions.dll"),
                Path.Combine(netCoreRefPath, "ref/net6.0/System.Console.dll"),
            };

           var diagnostics = compilation.Emit(moduleName: "Program", references, Path.Combine(programFolderPath, "obj/Debug/net6.0/Program.dll"));
           if (diagnostics.Any())
            {
                DiagnosticsWriter.WriteDiagnostics(Console.Out, diagnostics);
                return;
            }
            
            var projectPath = programFolderPath + @"/Program.aoproj";
            var dllPath = programFolderPath + @"/bin/Debug/net6.0/Program.dll";

            // dotnet build
            var buildCommand = $"/C dotnet build \"" + projectPath + "\" --nologo --interactive";
            var buildStartInfo = new ProcessStartInfo
            {            
                FileName = "cmd.exe",
                Arguments = buildCommand,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            
            var buildCli = Process.Start(buildStartInfo);
            buildCli.WaitForExit();

            // dotnet run
            var runCommand = $"/C dotnet " + dllPath;
            var runCli = Process.Start("cmd.exe", runCommand);
            runCli.WaitForExit();

            _previous = compilation;
            SaveSubmission(text);
        }

        private void LoadSubmissions()
        {
            var dir = GetSubmissionsDirectory();
            if (!Directory.Exists(dir))
                return;
            
            var submissions = Directory.GetFiles(dir).OrderBy(f => f);
            if (submissions.Count() == 0)
                return;

            var count = submissions.Count();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            if (count > 1 || count == 0)
                Console.WriteLine($"{count} submissions loaded.");
            else
                Console.WriteLine($"{count} submission loaded.");
            Console.ResetColor();

            _loadingSubmissions = true;
            foreach (var file in submissions)
            {
                var text = File.ReadAllText(file);
                EvaluateSubmission(text);
            }
            
            _loadingSubmissions = false;
        }

        private void ClearSubmissions() 
        {
            var dir = GetSubmissionsDirectory();
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        } 

        private void SaveSubmission(string text)
        {
            if (_loadingSubmissions)
                return;
            
            var submissionFolder = GetSubmissionsDirectory();
            Directory.CreateDirectory(submissionFolder);
            var count = Directory.GetFiles(submissionFolder).Length;
            var name = $"submission{count:0000}";
            var filename = Path.Combine(submissionFolder, name);
            File.WriteAllText(filename, text);
        }

        private string CreateBuildPrerequisites()
        {
            var localAppData = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var programFolder = Path.Combine(localAppData, "Alto", "Program");

            return programFolder;
        }

        private string GetSubmissionsDirectory()
        {
            var localAppData = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var submissionFolder = Path.Combine(localAppData, "Alto", "Submissions");
            return submissionFolder;
        }

        public Config GetConfig()
        {
            var path = GetConfigPath();
            var text = File.ReadAllText(path);
            Config config = JsonConvert.DeserializeObject<Config>(text);

            return config;
        }

        public string GetConfigPath()
        {
            var localAppData = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configPath = Path.Combine(localAppData, "Alto", "config.json");

            if (!File.Exists(configPath))
                CreateBaseConfig(configPath);

            return configPath;
        }

        private void CreateBaseConfig(string configPath)
        {
            var baseConfig = new Dictionary<string, string>
            {
                {"NETCorePath", "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.0-preview.7.21377.19/"}
            };

            var json = System.Text.Json.JsonSerializer.Serialize(baseConfig);
            File.WriteAllText(configPath, json);
        }

        protected override void RenderLine(string line)
        {
            var tokens = SyntaxTree.ParseTokens(line);
            foreach (var token in tokens)
            {
                var isKeyword = token.Kind.ToString().EndsWith("Keyword");
                var isNumber = token.Kind == SyntaxKind.NumberToken;
                var isIdentifier = token.Kind == SyntaxKind.IdentifierToken;
                var isBool = token.Text.ToLower() == "true" || token.Text.ToLower() == "false";
                var isString = token.Kind == SyntaxKind.StringToken;

                if (isKeyword && !isBool)
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                else if (isNumber)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (isBool)
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                else if (isIdentifier)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                else if (isString)
                    Console.ForegroundColor = ConsoleColor.Magenta;
                else
                    Console.ForegroundColor = ConsoleColor.Gray;

                Console.Write(token.Text);
                Console.ResetColor();
            }
        }

        [MetaCommand("showtree", description: "Shows the parse tree representation.")]
        private void EvaluateShowTree()
        {
            _showTree = !_showTree;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(_showTree ? "Showing parse trees" : "Not showing parse trees");
            Console.ResetColor();
        }

        [MetaCommand("showprogram", description: "Shows the bound tree representation.")]
        private void EvaluateShowProgram()
        {
            _showProgram = !_showProgram;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(_showProgram ? "Showing bound trees" : "Not showing bound trees");
            Console.ResetColor();
        }

        [MetaCommand("load", description: "Loads a file.")]
        private void EvaluateLoad(string path)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File '{path}' does not exist!");
                Console.ResetColor();
                return;
            }

            var txt = File.ReadAllText(path);
            EvaluateSubmission(txt);

            var tree = SyntaxTree.Load(path);
            _previous = Compilation.CreateScript(_previous, tree);
        }

        [MetaCommand("ls", description: "Lists all symbols.")]
        private void EvaluateListSymbols()
        {
            var compilation = _previous ?? emptyCompilation;
            var symbols = compilation.GetSymbols().OrderBy(s => s.Kind).ThenBy(s => s.Name);
            foreach (var symbol in symbols)
            {
                symbol.WriteTo(Console.Out);
                Console.WriteLine();
            }
        }

        [MetaCommand("dump", description: "Shows the bound tree representation of a given function.")]
        private void EvaluateDump(string functionName)  
        {   
            var compilation = _previous ?? emptyCompilation;
            var symbol = compilation.GetSymbols().OfType<FunctionSymbol>().SingleOrDefault(f => f.Name == functionName);
            if (symbol == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Function '{functionName}' not found!");
                Console.ResetColor();
                return;
            }

            compilation.EmitTree(symbol, Console.Out);

        }

        [MetaCommand("cls", description: "Clears the screen.")]
        private void EvaluateCls()
        {
            Console.Clear();
        }

        [MetaCommand("reset", description: "Resets chained compilations and all stored submissions.")]
        private void EvaluateReset()
        {
            _previous = null;
            ClearSubmissions();
        }

        [MetaCommand("config", description: "Lists all config items.")]
        private void EvaluateConfig()
        {
            var path = GetConfigPath();
            Config config = GetConfig();
            
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("NETCorePath: ");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{config.NETCorePath}");

            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine($"Config Path: { path }");
            Console.WriteLine();
        }

        [MetaCommand("calter", description: "Changes a config item")]
        private void EvaluateCAlter(string key, string newValue)
        {
            var config = GetConfig();
            var path = GetConfigPath();

            var configType = config.GetType();
            var properties = configType.GetProperties().ToList();

            var matchingProperties = properties.Where(p => p.Name == key);
            if (matchingProperties.Count() != 0)
            {
                var property = matchingProperties.First();
                property.SetValue(config, newValue);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Key {key} not present in the config.");
                Console.ResetColor();
            }

            var json = JsonConvert.SerializeObject(config);

            var stream = new FileStream(path, FileMode.Truncate);
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(json);
            }
        }

        protected override bool IsCompleteSubmission(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            // checks if the 2 last lines are blank
            var forceComplete = text.Split(Environment.NewLine)
                                    .Reverse()
                                    .TakeWhile(s => string.IsNullOrEmpty(s))
                                    .Take(2)
                                    .Count() == 2;

            if (forceComplete)
                return true;

            var syntaxTree = SyntaxTree.Parse(text);

            if (syntaxTree.Root.Members.Length == 0 || syntaxTree.Root.Members.Last().GetLastToken().IsMissing)
                return false;

            return true;
        }
    }
}