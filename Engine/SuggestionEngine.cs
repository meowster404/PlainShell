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
            ("config --global user.name", "Set global author name for all commits"),
            ("config --global user.email", "Set global author email address"),
            ("config --list --show-origin", "List all active Git configuration settings"),
            ("add .", "Stage all changes in current repository"),
            ("commit -m", "Commit staged changes with message"),
            ("push", "Push commits to remote repository"),
            ("pull", "Fetch and merge remote changes"),
            ("log --graph --oneline --all", "Visual commit history tree across branches"),
            ("diff", "Show unstaged changes in working tree"),
            ("diff --cached", "Show changes staged for next commit"),
            ("remote -v", "List remote repositories with URLs"),
            ("remote add origin", "Link local repository to remote Git URL"),
            ("checkout -b", "Create and switch to a new branch"),
            ("branch -a", "List local and remote branches"),
            ("restore --staged", "Unstage file changes without losing work"),
            ("reset --soft HEAD~1", "Undo last commit but keep changes staged"),
            ("stash", "Temporarily stash modified work"),
            ("stash pop", "Reapply most recent stashed changes"),
            ("cherry-pick", "Apply commit from another branch"),
            ("clean -fd", "Force remove untracked files and folders"),
            ("tag -a", "Create annotated release tag")
        },
        ["docker"] = new()
        {
            ("ps", "List active running containers"),
            ("ps -a", "List all containers including stopped"),
            ("compose up -d", "Start container stack in background"),
            ("compose down -v", "Stop and clean up containers and volumes"),
            ("logs -f", "Stream container logs live"),
            ("build -t", "Build container image from Dockerfile"),
            ("exec -it", "Run interactive shell inside container"),
            ("stop", "Stop one or more running containers"),
            ("system prune -a --volumes", "Remove all unused images, containers, volumes")
        },
        ["dotnet"] = new()
        {
            ("run", "Run application directly"),
            ("watch run", "Run application with Hot Reload"),
            ("build", "Build project and dependencies"),
            ("test", "Execute unit and integration tests"),
            ("publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true", "Publish single-file executable"),
            ("add package", "Add NuGet package to project"),
            ("new webapi", "Scaffold ASP.NET Core Web API project"),
            ("new console", "Scaffold new console application"),
            ("clean", "Clean project build outputs")
        },
        ["npm"] = new()
        {
            ("install", "Install package dependencies"),
            ("run dev", "Run development server script"),
            ("run build", "Compile production application bundle"),
            ("outdated", "Check for outdated dependencies"),
            ("test", "Execute configured test runner"),
            ("start", "Start application service"),
            ("init -y", "Initialize new package.json file")
        },
        ["winget"] = new()
        {
            ("search", "Search Windows Package Manager catalog"),
            ("install", "Install application via winget"),
            ("upgrade --all", "Upgrade all installed Windows apps"),
            ("list", "List installed applications")
        },
        ["netsh"] = new()
        {
            ("wlan show profiles", "List saved Wi-Fi networks"),
            ("wlan show profile name= key=clear", "Show plaintext Wi-Fi password"),
            ("int ip reset", "Reset TCP/IP network protocol stack"),
            ("winsock reset", "Reset Winsock network sockets catalog"),
            ("advfirewall set allprofiles state on", "Enable Windows Defender firewall")
        },
        ["powercfg"] = new()
        {
            ("/batteryreport", "Generate HTML laptop battery health report"),
            ("/hibernate off", "Disable hibernation and reclaim disk space"),
            ("/energy", "Analyze system power efficiency for 60s")
        },
        ["python"] = new()
        {
            ("-m venv .venv", "Create new virtual environment in .venv"),
            ("-m pip install -r requirements.txt", "Install requirements"),
            ("main.py", "Execute main Python script")
        }
    };

    private static readonly HashSet<string> PathCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cd", "chdir", "dir", "ls", "type", "cat", "code", "del", "rm", "rmdir", "copy", "move", "explorer", "notepad", "robocopy"
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

        // If line is empty, return NO suggestions (allows clean prompt and instant Up/Down history browsing!)
        if (string.IsNullOrWhiteSpace(textBeforeCursor))
        {
            return suggestions;
        }

        string trimmed = textBeforeCursor.TrimStart();
        string[] parts = textBeforeCursor.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool endsWithSpace = textBeforeCursor.EndsWith(' ');
        string activeToken = endsWithSpace ? string.Empty : (parts.Length > 0 ? parts[^1] : string.Empty);
        string firstToken = parts.Length > 0 ? parts[0] : string.Empty;

        // Special CASE: 'admin <command>' or 'sudo <command>'
        if (string.Equals(firstToken, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(firstToken, "sudo", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 1 && endsWithSpace)
            {
                return GetCommandSuggestions(string.Empty);
            }
            if (parts.Length > 1)
            {
                string remainder = textBeforeCursor.Substring(parts[0].Length).TrimStart();
                return GetSuggestions(remainder, remainder.Length, currentDirectory);
            }
        }

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

        // CASE 3: Git Config Subcommand: 'git config ...'
        if (string.Equals(firstToken, "git", StringComparison.OrdinalIgnoreCase) &&
            parts.Length >= 2 && string.Equals(parts[1], "config", StringComparison.OrdinalIgnoreCase))
        {
            var configSuggestions = new List<(string Sub, string Desc)>
            {
                ("user.name", "Configure Git author name"),
                ("user.email", "Configure Git author email address"),
                ("--global user.name", "Set global Git author name"),
                ("--global user.email", "Set global Git author email address"),
                ("--list --show-origin", "List all active settings with file locations"),
                ("core.autocrlf true", "Enable automatic CRLF line ending conversion")
            };

            string q = activeToken.ToLowerInvariant();
            foreach (var (sub, desc) in configSuggestions)
            {
                if (string.IsNullOrEmpty(q) || sub.ToLowerInvariant().Contains(q))
                {
                    suggestions.Add(new SuggestionItem
                    {
                        DisplayText = sub,
                        InsertText = sub,
                        Kind = SuggestionKind.Subcommand,
                        Description = desc,
                        Category = "Git Config"
                    });
                }
            }
            if (suggestions.Count > 0) return suggestions.Take(8).ToList();
        }

        // CASE 4: Known command with subcommands (git, docker, dotnet, npm, netsh, etc.)
        if (SubcommandMap.TryGetValue(firstToken, out var subcommands))
        {
            // If typing after primary command (e.g. 'git st' or 'git ')
            string searchPart = string.Join(" ", parts.Skip(1)) + (endsWithSpace ? " " : "");
            searchPart = searchPart.Trim();

            var query = searchPart.ToLowerInvariant();
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

        // CASE 5: Path commands (code, type, dir, robocopy, etc.) or active token looks like a path
        if (PathCommands.Contains(firstToken) || activeToken.Contains('/') || activeToken.Contains('\\') || activeToken.StartsWith('.'))
        {
            var pathSuggestions = GetFileSystemSuggestions(activeToken, currentDirectory, directoriesOnly: false);
            if (pathSuggestions.Count > 0)
            {
                return pathSuggestions;
            }
        }

        // CASE 6: General catalog search fallback
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

        // 1. Search catalog matches
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

        // 2. Built-in system commands
        var builtins = new[]
        {
            ("cd", "Change current working directory"),
            ("dir", "List directory contents"),
            ("cls", "Clear terminal screen"),
            ("admin", "Run command elevated as Administrator"),
            ("sudo", "Run command elevated as Administrator"),
            (":catalog", "Open live searchable Command Palette [F1 / Ctrl+P]"),
            (":help", "View keyboard shortcuts and controls"),
            (":history", "View session command history"),
            ("exit", "Exit PlainShell")
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
}
