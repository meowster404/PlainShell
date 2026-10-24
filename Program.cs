using System.Text;
using PlainShell.Catalog;
using PlainShell.Core;
using PlainShell.Engine;
using PlainShell.Tests;
using Spectre.Console;

namespace PlainShell;

public static class Program
{
    public static int Main(string[] args)
    {
        // Configure terminal for UTF-8 support and ANSI escape sequences
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // Check command-line CLI arguments
        if (args.Length > 0)
        {
            string arg = args[0].ToLowerInvariant();
            if (arg == "--test" || arg == "-t")
            {
                return SelfTestRunner.RunAllTests();
            }

            if (arg == "--update" || arg == "-u")
            {
                UpdateManager.CheckAndPromptUpdate(interactive: true);
                return 0;
            }

            if (arg == "--version" || arg == "-v")
            {
                AnsiConsole.MarkupLine($"[bold cyan]PlainShell[/] version [bold green]{UpdateManager.CurrentVersion}[/] ({UpdateManager.CurrentTag}) [grey](win-x64)[/]");
                AnsiConsole.MarkupLine("[grey]Intelligent Terminal Shell with VS Code-Style IntelliSense[/]");
                return 0;
            }

            if (arg == "--help" || arg == "-h")
            {
                AnsiConsole.MarkupLine("[bold cyan]PlainShell[/] - Modern Intelligent Windows Terminal Shell\n");
                AnsiConsole.MarkupLine("[bold white]Usage:[/]");
                AnsiConsole.MarkupLine("  PlainShell.exe            Launch interactive shell session");
                AnsiConsole.MarkupLine("  PlainShell.exe --update   Check for updates and apply in-app");
                AnsiConsole.MarkupLine("  PlainShell.exe --test     Run automated self-tests & diagnostic suite");
                AnsiConsole.MarkupLine("  PlainShell.exe --version  Show version information");
                AnsiConsole.MarkupLine("  PlainShell.exe --help     Show this help message\n");
                AnsiConsole.MarkupLine("[bold white]Interactive Controls in Shell:[/]");
                AnsiConsole.MarkupLine("  [yellow]Typing[/]               Triggers floating IntelliSense suggestions & directory auto-complete");
                AnsiConsole.MarkupLine("  [yellow]Tab[/]                  Accept / autocomplete active suggestion");
                AnsiConsole.MarkupLine("  [yellow]Up / Down[/]            Navigate suggestion choices or command history");
                AnsiConsole.MarkupLine("  [yellow]Esc[/]                  Dismiss suggestions overlay");
                AnsiConsole.MarkupLine("  [yellow]F1[/] or [yellow]Ctrl+P[/]         Open Categorized Command Palette & Catalog");
                return 0;
            }
        }

        // Silent non-blocking update check in background
        _ = Task.Run(async () =>
        {
            var up = await UpdateManager.CheckForUpdateAsync(silent: true);
            if (up != null)
            {
                UpdateManager.PendingNotification = $"Update available: {up.TagName}! (Press F1 -> Check Updates)";
            }
        });

        // Initialize shell components
        var context = new ShellContext();
        var catalog = new CommandCatalog();
        var suggestionEngine = new SuggestionEngine(catalog);
        var palette = new PaletteView(catalog);

        // Executor with callback to trigger command palette
        string? pendingInput = null;
        CommandExecutor? executor = null;
        executor = new CommandExecutor(context, catalog, () =>
        {
            var chosen = palette.Show();
            if (chosen != null && !string.IsNullOrEmpty(chosen.Command))
            {
                if (chosen.InsertOnly)
                {
                    pendingInput = chosen.Command;
                }
                else
                {
                    executor?.Execute(chosen.Command, chosen.AsAdmin);
                }
            }
        });

        var lineEditor = new LineEditor(suggestionEngine, context);

        // Print initial banner
        ConsoleHelper.SafeClear();
        CommandExecutor.PrintWelcomeBanner();

        // Main Shell REPL loop
        while (context.IsRunning)
        {
            var result = lineEditor.ReadLine(pendingInput);
            pendingInput = null;

            if (result.OpenPaletteRequested)
            {
                var chosen = palette.Show();
                if (chosen != null && !string.IsNullOrEmpty(chosen.Command))
                {
                    if (chosen.InsertOnly)
                    {
                        pendingInput = chosen.Command;
                    }
                    else
                    {
                        executor.Execute(chosen.Command, chosen.AsAdmin);
                    }
                }
                continue;
            }

            if (result.Cancelled)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(result.Command))
            {
                executor.Execute(result.Command);
            }
        }

        return 0;
    }
}
