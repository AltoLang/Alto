## AltoLang
Alto is a staticlly typed programming language in development.
It's currently only interpreted, but actual compilation is in progress.
It's based on the Minsk compiler.

## Start it up!
 - Fork the repo
 - Run .\build (Please drop us an issue if you encounter unexpected build errors)
 - Add the compiler to vscode tasks
 - Compile your project!

## Examples
```ts
function main() {
    print("Hello, Alto!")
    factorial(10, true)
}

function factorial(n : int, printToConsole : bool = false) : int {
    var total = 0
    var number = n
    while (number ~= 0) {
        number = number - 1
        total = total + number
    }

    if (printToConsole)
        print(tostring(total))

    return total
}
```

## Add it to vscode tasks
Add this to your `tasks.json` file. You can also associate a shortcut for building Alto files.
```json
{
    "label": "aoc",
    "command": "dotnet",
    "type": "shell",
    "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/aoc/aoc.csproj",
        "--",
        "${fileDirname}"
    ],
    "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": true,
        "panel": "shared",
        "showReuseMessage": false,
        "clear": true
    },
    "problemMatcher": {
        "fileLocation": "absolute",
        "pattern": [
            {
                "regexp": "^(.*)\\((\\d+,\\d+\\,\\d+\\,\\d+\\))\\: (.*)$",
                "file": 1,
                "location": 2,
                "message": 3
            }
        ]
    }
}
```
