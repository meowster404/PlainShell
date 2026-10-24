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

            if (arg == "--version" || arg == "-v")
            {
                AnsiConsole.MarkupLine("[bold cyan]PlainShell[/] version [bold green]1.0.0[/] (win-x64)");
                AnsiConsole.MarkupLine("[grey]Intelligent Terminal Shell with VS Code-Style IntelliSense[/]");
                return 0;
            }

            if (arg == "--help" || arg == "-h")
            {
                AnsiConsole.MarkupLine("[bold cyan]PlainShell[/] - Modern Intelligent Windows Terminal Shell\n");
                AnsiConsole.MarkupLine("[bold white]Usage:[/]");
                AnsiConsole.MarkupLine("  PlainShell.exe            Launch interactive shell session");
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

        // Initialize shell components
        var context = new ShellContext();
        var catalog = new CommandCatalog();
        var suggestionEngine = new SuggestionEngine(catalog);
        var palette = new PaletteView(catalog);

        // Executor with callback to trigger command palette
        CommandExecutor? executor = null;
        executor = new CommandExecutor(context, catalog, () =>
        {
            var chosen = palette.Show();
            if (!string.IsNullOrEmpty(chosen))
            {
                if (chosen.StartsWith("INSERT:"))
                {
                    // Handled in caller
                }
                else
                {
                    executor?.Execute(chosen);
                }
            }
        });

        var lineEditor = new LineEditor(suggestionEngine, context);

        // Print initial banner
        ConsoleHelper.SafeClear();
        CommandExecutor.PrintWelcomeBanner();

        string? pendingInput = null;

        // Main Shell REPL loop
        while (context.IsRunning)
        {
            var result = lineEditor.ReadLine(pendingInput);
            pendingInput = null;

            if (result.OpenPaletteRequested)
            {
                var chosen = palette.Show();
                if (!string.IsNullOrEmpty(chosen))
                {
                    if (chosen.StartsWith("INSERT:"))
                    {
                        pendingInput = chosen.Substring("INSERT:".Length);
                    }
                    else
                    {
                        executor.Execute(chosen);
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
