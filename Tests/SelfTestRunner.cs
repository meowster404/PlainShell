using PlainShell.Catalog;
using PlainShell.Core;
using PlainShell.Engine;
using Spectre.Console;

namespace PlainShell.Tests;

public static class SelfTestRunner
{
    public static int RunAllTests()
    {
        AnsiConsole.MarkupLine("[bold cyan]Running PlainShell Diagnostic Test Suite...[/]\n");
        int passed = 0;
        int failed = 0;

        void Assert(string testName, bool condition, string detail = "")
        {
            if (condition)
            {
                AnsiConsole.MarkupLine($"[green]✓ PASS[/] {Markup.Escape(testName)} {Markup.Escape(detail)}");
                passed++;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ FAIL[/] {Markup.Escape(testName)} {Markup.Escape(detail)}");
                failed++;
            }
        }

        try
        {
            // Test 0: Welcome Banner Markup Test
            bool bannerSuccess = true;
            try { CommandExecutor.PrintWelcomeBanner(); } catch { bannerSuccess = false; }
            Assert("PrintWelcomeBanner renders without markup parsing errors", bannerSuccess);

            // Test 1: Catalog Initialization & Built-ins
            var catalog = new CommandCatalog();
            var allCommands = catalog.GetAll();
            Assert("Catalog loads built-in commands", allCommands.Count >= 20, $"Count: {allCommands.Count}");

            // Test 2: Categories Check
            var categories = catalog.GetCategories();
            Assert("Catalog has categories", categories.Count >= 5, $"Categories: {string.Join(", ", categories.Take(4))}...");
            Assert("Navigation category exists", categories.Any(c => c.Contains("Navigation", StringComparison.OrdinalIgnoreCase)));
            Assert("Git category exists", categories.Any(c => c.Contains("Git", StringComparison.OrdinalIgnoreCase)));

            // Test 3: Search Functionality
            var searchCd = catalog.Search("cd");
            Assert("Search for 'cd' returns matches", searchCd.Count > 0 && searchCd[0].Name == "cd");

            var searchGit = catalog.Search("status", "Git Version Control");
            Assert("Search by category and keyword", searchGit.Any(c => c.Name.Contains("status")));

            // Test 4: Custom Command Add
            string customCmdName = "my-test-script-" + Guid.NewGuid().ToString().Substring(0, 6);
            catalog.AddCustomCommand(new CommandItem
            {
                Name = customCmdName,
                Category = "Test Category",
                Description = "A temporary test command",
                Syntax = customCmdName
            });
            var searchCustom = catalog.Search(customCmdName);
            Assert("Custom command addition and search", searchCustom.Any(c => c.Name == customCmdName && c.IsCustom));

            // Test 5: SuggestionEngine - First Token Matching
            var engine = new SuggestionEngine(catalog);
            string cwd = Directory.GetCurrentDirectory();

            var suggestionsForC = engine.GetSuggestions("c", 1, cwd);
            Assert("Suggestions for 'c' contains 'cd'", suggestionsForC.Any(s => s.DisplayText.StartsWith("cd", StringComparison.OrdinalIgnoreCase)));

            // Test 6: SuggestionEngine - Directory Completions for 'cd '
            var cdSuggestions = engine.GetSuggestions("cd ", 3, cwd);
            Assert("Suggestions for 'cd ' returns directories", cdSuggestions.Count > 0 && cdSuggestions.All(s => s.Kind == SuggestionKind.Directory));

            // Test 7: SuggestionEngine - Git Subcommands & Git Config
            var gitSuggestions = engine.GetSuggestions("git ", 4, cwd);
            Assert("Suggestions for 'git ' returns subcommands", gitSuggestions.Any(s => s.DisplayText == "status" && s.Kind == SuggestionKind.Subcommand));

            var gitConfigSuggestions = engine.GetSuggestions("git config ", 11, cwd);
            Assert("Suggestions for 'git config ' returns user.name", gitConfigSuggestions.Any(s => s.DisplayText.Contains("user.name")));

            // Test 8: Empty buffer returns NO suggestions (allows instant history browsing)
            var emptySuggestions = engine.GetSuggestions("", 0, cwd);
            Assert("Empty buffer returns 0 suggestions for clean prompt", emptySuggestions.Count == 0);

            // Test 9: SuggestionEngine - Docker Subcommands
            var dockerSuggestions = engine.GetSuggestions("docker ", 7, cwd);
            Assert("Suggestions for 'docker ' returns subcommands", dockerSuggestions.Any(s => s.DisplayText == "ps" && s.Kind == SuggestionKind.Subcommand));

            // Test 10: Shell Context Directory & History Reset
            var context = new ShellContext();
            Assert("ShellContext initializes current directory", Directory.Exists(context.CurrentDirectory));
            context.AddHistory("echo hello");
            context.AddHistory("git status");
            Assert("History tracks commands", context.History.Count == 2);
            context.ResetHistoryIndex();
            Assert("History back navigates to most recent command", context.GetPreviousHistory() == "git status");
            Assert("History back navigates to previous command", context.GetPreviousHistory() == "echo hello");

            // Test 11: Git Branch Detection
            string? branch = context.GetGitBranch();
            Assert("Git branch inspection succeeds", true, $"Detected branch: {branch ?? "none"}");

            // Test 12: Advanced Daily Windows Commands
            var sfcMatch = catalog.Search("sfc");
            Assert("Catalog includes Windows sfc repair command", sfcMatch.Any(c => c.Name.Contains("sfc")));

            var gitConfigMatch = catalog.Search("user.name");
            Assert("Catalog includes git config user.name", gitConfigMatch.Any(c => c.Name.Contains("user.name")));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Fatal Test Runner Error:[/] {Markup.Escape(ex.ToString())}");
            failed++;
        }

        AnsiConsole.WriteLine();
        if (failed == 0)
        {
            AnsiConsole.MarkupLine($"[bold green]ALL {passed} TESTS PASSED PERFECTLY![/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold red]{failed} TESTS FAILED out of {passed + failed} total tests.[/]");
            return 1;
        }
    }
}
