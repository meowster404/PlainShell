using PlainShell.Catalog;

namespace PlainShell.Engine;

public class SuggestionEngine
{
    private readonly CommandCatalog _catalog;

    // Common subcommands dictionary
    private static readonly Dictionary<string, List<(string Sub, string Desc)>> SubcommandMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["git"] = new()
        {
            ("status", "Show working tree and modified files"),
            ("add .", "Stage all changes in current repository"),
            ("commit -m", "Commit staged changes with message"),
            ("push", "Push commits to remote repository"),
            ("pull", "Fetch and merge remote changes"),
            ("log --oneline -n 10", "View last 10 commits compactly"),
            ("diff", "Show unstaged changes in working tree"),
            ("checkout -b", "Create and switch to a new branch"),
            ("branch -a", "List local and remote branches"),
            ("stash", "Temporarily stash modified work"),
            ("stash pop", "Reapply most recent stashed changes"),
            ("fetch", "Download objects and refs from another repo"),
            ("reset --soft HEAD~1", "Undo last commit but keep changes staged")
        },
        ["docker"] = new()
        {
            ("ps", "List running containers"),
            ("ps -a", "List all containers including stopped"),
            ("compose up -d", "Start container stack in background"),
            ("compose down", "Stop and clean up container stack"),
            ("logs -f", "Stream container logs live"),
            ("build -t", "Build container image from Dockerfile"),
            ("exec -it", "Run interactive command inside container"),
            ("stop", "Stop one or more running containers"),
            ("system prune -a", "Remove unused images, containers, and networks")
        },
        ["dotnet"] = new()
        {
            ("run", "Run application directly"),
            ("build", "Build project and dependencies"),
            ("test", "Execute unit and integration tests"),
            ("publish -c Release", "Publish optimized self-contained app"),
            ("add package", "Add NuGet package to project"),
            ("new console", "Scaffold new console application"),
            ("clean", "Clean project build outputs")
        },
        ["npm"] = new()
        {
            ("install", "Install package dependencies"),
            ("run dev", "Run development server script"),
            ("run build", "Compile production application bundle"),
            ("test", "Execute configured test runner"),
            ("start", "Start application service"),
            ("init -y", "Initialize new package.json file")
        },
        ["python"] = new()
        {
            ("-m venv .venv", "Create new virtual environment in .venv"),
            ("main.py", "Execute main Python script"),
            ("-m pip install", "Install Python package")
        }
    };

    private static readonly HashSet<string> PathCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cd", "chdir", "dir", "ls", "type", "cat", "code", "del", "rm", "rmdir", "copy", "move", "explorer", "notepad"
    };

    public SuggestionEngine(CommandCatalog catalog)
    {
        _catalog = catalog;
    }

    public List<SuggestionItem> GetSuggestions(string input, int cursorPosition, string currentDirectory)
    {
        var suggestions = new List<SuggestionItem>();

        // Safe substring up to cursor
        string textBeforeCursor = cursorPosition <= input.Length
            ? input.Substring(0, cursorPosition)
            : input;

        // If line is empty, return top common commands
        if (string.IsNullOrWhiteSpace(textBeforeCursor))
        {
            return GetDefaultOverviewSuggestions();
        }

        string trimmed = textBeforeCursor.TrimStart();
        string[] parts = textBeforeCursor.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool endsWithSpace = textBeforeCursor.EndsWith(' ');
        string activeToken = endsWithSpace ? string.Empty : (parts.Length > 0 ? parts[^1] : string.Empty);
        string firstToken = parts.Length > 0 ? parts[0] : string.Empty;

        // CASE 1: Typing the primary command (first token, no trailing space yet)
        if (parts.Length == 1 && !endsWithSpace)
        {
            return GetCommandSuggestions(firstToken);
        }

        // CASE 2: Command is 'cd' or another directory-only command -> suggest directories
        if (string.Equals(firstToken, "cd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(firstToken, "chdir", StringComparison.OrdinalIgnoreCase))
        {
            return GetFileSystemSuggestions(activeToken, currentDirectory, directoriesOnly: true);
        }

        // CASE 3: Known command with subcommands (git, docker, dotnet, npm, etc.)
        if (SubcommandMap.TryGetValue(firstToken, out var subcommands))
        {
            // If typing the subcommand (e.g. 'git st' or 'git ')
            if ((parts.Length == 1 && endsWithSpace) || (parts.Length == 2 && !endsWithSpace))
            {
                var query = activeToken.ToLowerInvariant();
                foreach (var (sub, desc) in subcommands)
                {
                    if (string.IsNullOrEmpty(query) || sub.ToLowerInvariant().Contains(query))
                    {
                        suggestions.Add(new SuggestionItem
                        {
                            DisplayText = sub,
                            InsertText = sub,
                            Kind = SuggestionKind.Subcommand,
                            Description = desc,
                            Category = firstToken.ToUpperInvariant()
                        });
                    }
                }

                if (suggestions.Count > 0)
                {
                    return suggestions.Take(8).ToList();
                }
            }
        }

        // CASE 4: Path commands (code, type, dir, etc.) or active token looks like a path
        if (PathCommands.Contains(firstToken) || activeToken.Contains('/') || activeToken.Contains('\\') || activeToken.StartsWith('.'))
        {
            var pathSuggestions = GetFileSystemSuggestions(activeToken, currentDirectory, directoriesOnly: false);
            if (pathSuggestions.Count > 0)
            {
                return pathSuggestions;
            }
        }

        // CASE 5: General fallback - search catalog for matching commands/flags
        var catalogMatches = _catalog.Search(trimmed);
        foreach (var cmd in catalogMatches.Take(6))
        {
            suggestions.Add(new SuggestionItem
            {
                DisplayText = cmd.Name,
                InsertText = cmd.Name,
                Kind = SuggestionKind.Command,
                Description = cmd.Description,
                Category = cmd.Category
            });
        }

        return suggestions;
    }

    private List<SuggestionItem> GetCommandSuggestions(string prefix)
    {
        var results = new List<SuggestionItem>();
        string q = prefix.ToLowerInvariant();

        // 1. Check catalog
        var matches = _catalog.Search(q);
        foreach (var cmd in matches)
        {
            results.Add(new SuggestionItem
            {
                DisplayText = cmd.Name,
                InsertText = cmd.Name,
                Kind = SuggestionKind.Command,
                Description = cmd.Description,
                Category = cmd.Category
            });
        }

        // 2. Also check common executables or short aliases
        var builtins = new[]
        {
            ("cd", "Change current directory"),
            ("dir", "List directory contents"),
            ("cls", "Clear terminal screen"),
            ("exit", "Exit PlainShell"),
            (":catalog", "Open Categorized Command Palette [F1 / Ctrl+P]"),
            (":help", "View keyboard shortcuts & tips"),
            (":history", "View command history")
        };

        foreach (var (name, desc) in builtins)
        {
            if (name.StartsWith(q) && results.All(r => !r.DisplayText.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                results.Insert(0, new SuggestionItem
                {
                    DisplayText = name,
                    InsertText = name,
                    Kind = SuggestionKind.Command,
                    Description = desc,
                    Category = "Built-in"
                });
            }
        }

        return results.Take(8).ToList();
    }

    private List<SuggestionItem> GetFileSystemSuggestions(string token, string currentDirectory, bool directoriesOnly)
    {
        var suggestions = new List<SuggestionItem>();

        try
        {
            string searchPath = currentDirectory;
            string prefix = token;

            // Handle parent directory traversal
            if (token.StartsWith("..") || token.StartsWith("."))
            {
                int lastSlash = Math.Max(token.LastIndexOf('/'), token.LastIndexOf('\\'));
                if (lastSlash >= 0)
                {
                    string dirPart = token.Substring(0, lastSlash + 1);
                    searchPath = Path.GetFullPath(Path.Combine(currentDirectory, dirPart));
                    prefix = token.Substring(lastSlash + 1);
                }
            }
            else if (token.Contains('/') || token.Contains('\\'))
            {
                int lastSlash = Math.Max(token.LastIndexOf('/'), token.LastIndexOf('\\'));
                string dirPart = token.Substring(0, lastSlash + 1);
                searchPath = Path.GetFullPath(Path.Combine(currentDirectory, dirPart));
                prefix = token.Substring(lastSlash + 1);
            }

            if (!Directory.Exists(searchPath))
            {
                return suggestions;
            }

            // Always add parent directory if token starts with '..'
            if (token == ".." || token.StartsWith(".."))
            {
                suggestions.Add(new SuggestionItem
                {
                    DisplayText = "../",
                    InsertText = "../",
                    Kind = SuggestionKind.Directory,
                    Description = "Parent directory",
                    Category = "Navigation"
                });
            }

            // Enumerate directories
            var dirs = Directory.GetDirectories(searchPath);
            foreach (var dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.StartsWith(".") && !prefix.StartsWith(".")) continue;

                if (string.IsNullOrEmpty(prefix) || dirName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string completion = dirName.Contains(' ') ? $"\"{dirName}/\"" : $"{dirName}/";
                    suggestions.Add(new SuggestionItem
                    {
                        DisplayText = dirName + "/",
                        InsertText = completion,
                        Kind = SuggestionKind.Directory,
                        Description = "Folder in " + Path.GetFileName(searchPath),
                        Category = "Directory"
                    });
                }
            }

            // Enumerate files (unless directory-only)
            if (!directoriesOnly)
            {
                var files = Directory.GetFiles(searchPath);
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.StartsWith(".") && !prefix.StartsWith(".")) continue;

                    if (string.IsNullOrEmpty(prefix) || fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string completion = fileName.Contains(' ') ? $"\"{fileName}\"" : fileName;
                        suggestions.Add(new SuggestionItem
                        {
                            DisplayText = fileName,
                            InsertText = completion,
                            Kind = SuggestionKind.File,
                            Description = FormatFileSize(file),
                            Category = "File"
                        });
                    }
                }
            }
        }
        catch
        {
            // Silently handle permission/path errors
        }

        return suggestions.Take(8).ToList();
    }

    private static string FormatFileSize(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            long bytes = info.Length;
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
        catch
        {
            return "File";
        }
    }

    private List<SuggestionItem> GetDefaultOverviewSuggestions()
    {
        return new List<SuggestionItem>
        {
            new() { DisplayText = "cd <dir>", InsertText = "cd ", Kind = SuggestionKind.Command, Description = "Navigate to a directory", Category = "Navigation" },
            new() { DisplayText = "dir", InsertText = "dir", Kind = SuggestionKind.Command, Description = "List files and directories", Category = "Navigation" },
            new() { DisplayText = "git status", InsertText = "git status", Kind = SuggestionKind.Command, Description = "Check git repository status", Category = "Git" },
            new() { DisplayText = "docker ps", InsertText = "docker ps", Kind = SuggestionKind.Command, Description = "List running docker containers", Category = "Docker" },
            new() { DisplayText = ":catalog", InsertText = ":catalog", Kind = SuggestionKind.Command, Description = "Open Categorized Command Palette [F1 / Ctrl+P]", Category = "PlainShell" },
            new() { DisplayText = ":help", InsertText = ":help", Kind = SuggestionKind.Command, Description = "View keyboard shortcuts & tips", Category = "PlainShell" }
        };
    }
}
