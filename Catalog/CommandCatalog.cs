using System.Text.Json;

namespace PlainShell.Catalog;

public class CommandCatalog
{
    private readonly List<CommandItem> _commands = new();
    private readonly string _userConfigDir;
    private readonly string _customCommandsPath;

    public CommandCatalog()
    {
        _userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".plainshell"
        );
        _customCommandsPath = Path.Combine(_userConfigDir, "custom_commands.json");

        LoadBuiltInCommands();
        LoadCustomCommands();
    }

    private void LoadBuiltInCommands()
    {
        try
        {
            // First check local Assets directory or alongside executable
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(baseDir, "Assets", "commands.json");
            if (!File.Exists(localPath))
            {
                localPath = Path.Combine(baseDir, "commands.json");
            }

            if (File.Exists(localPath))
            {
                string json = File.ReadAllText(localPath);
                var items = JsonSerializer.Deserialize<List<CommandItem>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (items != null)
                {
                    _commands.AddRange(items);
                    return;
                }
            }
        }
        catch
        {
            // Fallback to in-memory defaults if file access fails
        }

        // Hardcoded safety fallback in case file is missing
        if (_commands.Count == 0)
        {
            _commands.AddRange(GetFallbackDefaults());
        }
    }

    private void LoadCustomCommands()
    {
        try
        {
            if (File.Exists(_customCommandsPath))
            {
                string json = File.ReadAllText(_customCommandsPath);
                var customItems = JsonSerializer.Deserialize<List<CommandItem>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (customItems != null)
                {
                    foreach (var item in customItems)
                    {
                        item.IsCustom = true;
                        if (string.IsNullOrWhiteSpace(item.Category))
                        {
                            item.Category = "Custom User Commands";
                        }
                        _commands.Add(item);
                    }
                }
            }
        }
        catch
        {
            // Ignore corrupted custom file, allow shell to continue
        }
    }

    public void AddCustomCommand(CommandItem item)
    {
        item.IsCustom = true;
        if (string.IsNullOrWhiteSpace(item.Category))
        {
            item.Category = "Custom User Commands";
        }

        _commands.Add(item);

        try
        {
            Directory.CreateDirectory(_userConfigDir);
            var customList = _commands.Where(c => c.IsCustom).ToList();
            string json = JsonSerializer.Serialize(customList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_customCommandsPath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    public IReadOnlyList<CommandItem> GetAll() => _commands.AsReadOnly();

    public List<string> GetCategories()
    {
        return _commands
            .Select(c => c.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    public List<CommandItem> GetByCategory(string category)
    {
        return _commands
            .Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .ToList();
    }

    public List<CommandItem> Search(string query, string? category = null)
    {
        IEnumerable<CommandItem> pool = _commands;

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
        {
            pool = pool.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return pool.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
        }

        string q = query.Trim().ToLowerInvariant();

        return pool
            .Select(c => new
            {
                Item = c,
                Score = ComputeMatchScore(c, q)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.Name.Length)
            .Select(x => x.Item)
            .ToList();
    }

    private static int ComputeMatchScore(CommandItem item, string q)
    {
        string name = item.Name.ToLowerInvariant();
        string desc = item.Description.ToLowerInvariant();
        string syntax = item.Syntax.ToLowerInvariant();
        string cat = item.Category.ToLowerInvariant();

        if (name == q) return 100;
        if (name.StartsWith(q)) return 80;
        if (name.Contains(q)) return 60;
        if (item.Tags.Any(t => t.Equals(q, StringComparison.OrdinalIgnoreCase))) return 50;
        if (item.Tags.Any(t => t.ToLowerInvariant().Contains(q))) return 40;
        if (cat.Contains(q)) return 30;
        if (desc.Contains(q)) return 20;
        if (syntax.Contains(q)) return 10;

        return 0;
    }

    private static List<CommandItem> GetFallbackDefaults()
    {
        return new List<CommandItem>
        {
            new() { Name = "cd", Category = "Navigation & Filesystem", Description = "Change current directory", Syntax = "cd <dir>", Example = "cd .." },
            new() { Name = "dir", Category = "Navigation & Filesystem", Description = "List files and directories", Syntax = "dir", Example = "dir" },
            new() { Name = "git status", Category = "Git Version Control", Description = "Show working tree status", Syntax = "git status", Example = "git status" },
            new() { Name = "cls", Category = "PlainShell Built-ins", Description = "Clear the terminal screen", Syntax = "cls", Example = "cls" },
            new() { Name = ":catalog", Category = "PlainShell Built-ins", Description = "Open Categorized Command Palette", Syntax = ":catalog", Example = ":catalog" },
            new() { Name = "exit", Category = "PlainShell Built-ins", Description = "Exit PlainShell", Syntax = "exit", Example = "exit" }
        };
    }
}
