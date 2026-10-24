using System.Diagnostics;
using PlainShell.Catalog;
using Spectre.Console;

namespace PlainShell.Core;

public class CommandExecutor
{
    private readonly ShellContext _context;
    private readonly CommandCatalog _catalog;
    private readonly Action _openCatalogAction;

    public CommandExecutor(ShellContext context, CommandCatalog catalog, Action openCatalogAction)
    {
        _context = context;
        _catalog = catalog;
        _openCatalogAction = openCatalogAction;
    }

    public void Execute(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return;

        string trimmed = commandLine.Trim();
        _context.AddHistory(trimmed);

        // Check for built-ins first
        if (HandleBuiltIns(trimmed))
        {
            return;
        }

        // Execute external command
        RunExternalCommand(trimmed);
    }

    private bool HandleBuiltIns(string commandLine)
    {
        string[] parts = commandLine.Split(' ', 2, StringSplitOptions.TrimEntries);
        string cmd = parts[0].ToLowerInvariant();
        string args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (cmd)
        {
            case "exit":
            case "quit":
                _context.IsRunning = false;
                AnsiConsole.MarkupLine("[grey]Exiting PlainShell. Goodbye![/]");
                return true;

            case "cls":
            case "clear":
                ConsoleHelper.SafeClear();
                PrintWelcomeBanner();
                return true;

            case ":catalog":
            case ":cmds":
            case ":menu":
                _openCatalogAction();
                return true;

            case ":help":
                PrintHelp();
                return true;

            case ":history":
                PrintHistory();
                return true;

            case "cd":
            case "chdir":
                HandleChangeDirectory(args);
                return true;

            default:
                // Check if it's a drive switch (e.g. "d:" or "c:")
                if (cmd.Length == 2 && cmd[1] == ':' && char.IsLetter(cmd[0]))
                {
                    HandleChangeDirectory(cmd);
                    return true;
                }
                return false;
        }
    }

    private void HandleChangeDirectory(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target == "~")
        {
            target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            // Remove enclosing quotes if user typed cd "path"
            if (target.StartsWith('"') && target.EndsWith('"') && target.Length >= 2)
            {
                target = target.Substring(1, target.Length - 2);
            }
        }

        try
        {
            string resolved = Path.GetFullPath(Path.Combine(_context.CurrentDirectory, target));
            if (Directory.Exists(resolved))
            {
                _context.CurrentDirectory = resolved;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Directory not found:[/] {Markup.Escape(target)}");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to change directory:[/] {Markup.Escape(ex.Message)}");
        }
    }

    private void RunExternalCommand(string commandLine)
    {
        try
        {
            // Use cmd.exe /c to seamlessly handle built-in shell commands (dir, copy, del, etc.)
            // and external executables (git, docker, npm, dotnet, etc.)
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {commandLine}",
                WorkingDirectory = _context.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    // Optionally note non-zero exit code if needed
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Execution error:[/] {Markup.Escape(ex.Message)}");
        }
    }

    public static void PrintWelcomeBanner()
    {
        AnsiConsole.Write(
            new FigletText("PlainShell")
                .LeftJustified()
                .Color(Color.Cyan1));

        var panel = new Panel(
            "[bold white]Intelligent Terminal Shell with VS Code-Style IntelliSense[/]\n" +
            "[grey]• Type [/][yellow]cd[/][grey], [/][yellow]git[/][grey], [/][yellow]docker[/][grey], [/][yellow]npm[/][grey] to see instant suggestions & paths[/]\n" +
            "[grey]• Press [/][cyan][[F1]][/][grey] or [/][cyan][[Ctrl+P]][/][grey] for the [/][bold cyan]Categorized Command Catalog[/]\n" +
            "[grey]• Press [/][cyan][[Tab]][/][grey] to accept suggestion, [/][cyan][[Up/Down]][/][grey] to navigate, [/][cyan][[Esc]][/][grey] to close popup[/]"
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.SteelBlue1),
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private void PrintHelp()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold cyan]Shortcut / Command[/]");
        table.AddColumn("[bold white]Action Description[/]");

        table.AddRow("[yellow]F1[/] or [yellow]Ctrl+P[/]", "Open Categorized Command Palette & Search");
        table.AddRow("[yellow]Tab[/]", "Insert / autocomplete active IntelliSense suggestion");
        table.AddRow("[yellow]Up / Down[/]", "Navigate suggestions list (or command history when popup is closed)");
        table.AddRow("[yellow]Esc[/]", "Dismiss IntelliSense suggestions overlay");
        table.AddRow("[yellow]Ctrl+C[/]", "Cancel current command input line");
        table.AddRow("[yellow]Ctrl+L[/] or [yellow]cls[/]", "Clear screen and redraw banner");
        table.AddRow("[yellow]:catalog[/]", "Open searchable command palette");
        table.AddRow("[yellow]:history[/]", "View history of entered commands in this session");
        table.AddRow("[yellow]exit[/] or [yellow]quit[/]", "Exit PlainShell");

        AnsiConsole.Write(table);
    }

    private void PrintHistory()
    {
        var history = _context.History;
        if (history.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No command history in this session yet.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn(new TableColumn("[bold grey]#[/]").RightAligned());
        table.AddColumn("[bold cyan]Command[/]");

        for (int i = 0; i < history.Count; i++)
        {
            table.AddRow($"[grey]{i + 1}[/]", Markup.Escape(history[i]));
        }

        AnsiConsole.Write(table);
    }
}
