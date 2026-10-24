using System.Diagnostics;

namespace PlainShell.Core;

public class ShellContext
{
    private string _currentDirectory;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public string CurrentDirectory
    {
        get => _currentDirectory;
        set
        {
            if (Directory.Exists(value))
            {
                _currentDirectory = Path.GetFullPath(value);
                Environment.CurrentDirectory = _currentDirectory;
            }
        }
    }

    public bool IsRunning { get; set; } = true;

    public IReadOnlyList<string> History => _history.AsReadOnly();

    public ShellContext()
    {
        _currentDirectory = Directory.GetCurrentDirectory();
    }

    public void AddHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        // Avoid immediate duplicate
        if (_history.Count > 0 && _history[^1] == command) return;

        _history.Add(command);
        _historyIndex = _history.Count;
    }

    public string? GetPreviousHistory()
    {
        if (_history.Count == 0) return null;

        if (_historyIndex > 0)
        {
            _historyIndex--;
            return _history[_historyIndex];
        }

        if (_historyIndex == 0)
        {
            return _history[0];
        }

        return null;
    }

    public string? GetNextHistory()
    {
        if (_history.Count == 0) return null;

        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            return _history[_historyIndex];
        }

        _historyIndex = _history.Count;
        return string.Empty;
    }

    public string? GetGitBranch()
    {
        try
        {
            string gitDir = Path.Combine(_currentDirectory, ".git");
            if (File.Exists(gitDir)) // Submodule or worktree
            {
                string content = File.ReadAllText(gitDir).Trim();
                if (content.StartsWith("gitdir:"))
                {
                    gitDir = Path.GetFullPath(Path.Combine(_currentDirectory, content.Substring(7).Trim()));
                }
            }

            if (!Directory.Exists(gitDir))
            {
                // Check parent directories for .git
                var parent = Directory.GetParent(_currentDirectory);
                while (parent != null)
                {
                    string candidate = Path.Combine(parent.FullName, ".git");
                    if (Directory.Exists(candidate))
                    {
                        gitDir = candidate;
                        break;
                    }
                    parent = parent.Parent;
                }
            }

            if (Directory.Exists(gitDir))
            {
                string headFile = Path.Combine(gitDir, "HEAD");
                if (File.Exists(headFile))
                {
                    string headContent = File.ReadAllText(headFile).Trim();
                    if (headContent.StartsWith("ref: refs/heads/"))
                    {
                        return headContent.Substring("ref: refs/heads/".Length);
                    }
                    if (headContent.Length >= 7)
                    {
                        return headContent.Substring(0, 7); // Detached commit
                    }
                }
            }
        }
        catch
        {
            // Silently ignore git inspection failures
        }

        return null;
    }
}
