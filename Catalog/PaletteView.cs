using System.Text;
using PlainShell.Core;
using Spectre.Console;

namespace PlainShell.Catalog;

public class PaletteResult
{
    public string? Command { get; set; }
    public bool AsAdmin { get; set; }
    public bool InsertOnly { get; set; }
}

public class PaletteView
{
    private readonly CommandCatalog _catalog;

    public PaletteView(CommandCatalog catalog)
    {
        _catalog = catalog;
    }

    public PaletteResult? Show()
    {
        var categories = new List<string> { "★ All" };
        categories.AddRange(_catalog.GetCategories());
        int selectedCategoryIndex = 0;

        var searchBuffer = new StringBuilder();
        int selectedCommandIndex = 0;

        while (true)
        {
            ConsoleHelper.SafeClear();

            string currentCategory = categories[selectedCategoryIndex];
            string? categoryFilter = currentCategory == "★ All" ? null : currentCategory;

            // Header Banner
            AnsiConsole.Write(
                new Rule("[bold cyan]PlainShell Live Command Palette & Catalog[/]")
                    .LeftJustified()
                    .RuleStyle("steelBlue1"));

            // Category Bar
            RenderCategoryTabs(categories, selectedCategoryIndex);

            // Live Search Query Bar
            AnsiConsole.Markup($"[bold cyan]🔍 Search:[/] [bold white]{Markup.Escape(searchBuffer.ToString())}[/][blink]█[/]\n");
            AnsiConsole.WriteLine();

            // Query matching commands live
            var matches = _catalog.Search(searchBuffer.ToString(), categoryFilter);

            // Clamp selection index
            if (matches.Count == 0)
            {
                selectedCommandIndex = 0;
            }
            else if (selectedCommandIndex >= matches.Count)
            {
                selectedCommandIndex = matches.Count - 1;
            }
            else if (selectedCommandIndex < 0)
            {
                selectedCommandIndex = 0;
            }

            // Render live commands table
            RenderLiveResults(matches, selectedCommandIndex);

            // Footer instructions
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Controls: [/][cyan][↑/↓][/][grey] Select command  [/][cyan][Tab / ← / →][/][grey] Switch category  [/][cyan][Enter][/][grey] Action menu  [/][cyan][Esc][/][grey] Exit[/]");

            // Wait for keypress
            var keyInfo = Console.ReadKey(intercept: true);

            // Handle Esc
            if (keyInfo.Key == ConsoleKey.Escape)
            {
                ConsoleHelper.SafeClear();
                return null;
            }

            // Handle Tab or RightArrow (switch category)
            if (keyInfo.Key == ConsoleKey.Tab)
            {
                if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                {
                    selectedCategoryIndex = (selectedCategoryIndex - 1 + categories.Count) % categories.Count;
                }
                else
                {
                    selectedCategoryIndex = (selectedCategoryIndex + 1) % categories.Count;
                }
                selectedCommandIndex = 0;
                continue;
            }

            // Handle Enter (Open action menu for selected command)
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                if (matches.Count > 0 && selectedCommandIndex >= 0 && selectedCommandIndex < matches.Count)
                {
                    var selected = matches[selectedCommandIndex];
                    var actionResult = ShowActionMenu(selected);
                    if (actionResult != null)
                    {
                        ConsoleHelper.SafeClear();
                        return actionResult;
                    }
                    // Else back to live search
                    continue;
                }
                continue;
            }

            // Handle Up / Down arrows (navigate live results)
            if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (matches.Count > 0)
                {
                    selectedCommandIndex = (selectedCommandIndex + 1) % matches.Count;
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (matches.Count > 0)
                {
                    selectedCommandIndex = (selectedCommandIndex - 1 + matches.Count) % matches.Count;
                }
                continue;
            }

            // Handle Left / Right arrow (category cycling if search empty)
            if (searchBuffer.Length == 0)
            {
                if (keyInfo.Key == ConsoleKey.RightArrow)
                {
                    selectedCategoryIndex = (selectedCategoryIndex + 1) % categories.Count;
                    selectedCommandIndex = 0;
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.LeftArrow)
                {
                    selectedCategoryIndex = (selectedCategoryIndex - 1 + categories.Count) % categories.Count;
                    selectedCommandIndex = 0;
                    continue;
                }
            }

            // Handle Backspace
            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (searchBuffer.Length > 0)
                {
                    searchBuffer.Remove(searchBuffer.Length - 1, 1);
                    selectedCommandIndex = 0;
                }
                continue;
            }

            // Printable character -> Append to search query
            if (!char.IsControl(keyInfo.KeyChar))
            {
                searchBuffer.Append(keyInfo.KeyChar);
                selectedCommandIndex = 0;
            }
        }
    }

    private static void RenderCategoryTabs(List<string> categories, int selectedIndex)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < categories.Count; i++)
        {
            if (i == selectedIndex)
            {
                sb.Append($"[bold black on cyan] {Markup.Escape(categories[i])} [/] ");
            }
            else
            {
                sb.Append($"[grey on grey15] {Markup.Escape(categories[i])} [/] ");
            }
        }
        AnsiConsole.MarkupLine(sb.ToString());
        AnsiConsole.WriteLine();
    }

    private static void RenderLiveResults(List<CommandItem> matches, int selectedIndex)
    {
        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No commands matched your search query in this category.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold grey]Category[/]").Width(22));
        table.AddColumn(new TableColumn("[bold cyan]Command[/]").Width(32));
        table.AddColumn(new TableColumn("[bold white]Description[/]"));

        int pageSize = 12;
        int startIndex = Math.Max(0, Math.Min(selectedIndex - (pageSize / 2), matches.Count - pageSize));
        int endIndex = Math.Min(matches.Count, startIndex + pageSize);

        for (int i = startIndex; i < endIndex; i++)
        {
            var cmd = matches[i];
            bool isSelected = i == selectedIndex;

            string catPart = Markup.Escape(cmd.Category);
            string namePart = Markup.Escape(cmd.Name);
            string descPart = Markup.Escape(cmd.Description);

            if (isSelected)
            {
                table.AddRow(
                    $"[bold cyan]▶ {catPart}[/]",
                    $"[bold black on cyan] {namePart} [/]",
                    $"[bold white]{descPart}[/]"
                );
            }
            else
            {
                table.AddRow(
                    $"[grey]{catPart}[/]",
                    $"[bold white]{namePart}[/]",
                    $"[grey]{descPart}[/]"
                );
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]Showing {startIndex + 1}-{endIndex} of {matches.Count} matching commands[/]");
    }

    private static PaletteResult? ShowActionMenu(CommandItem cmd)
    {
        ConsoleHelper.SafeClear();

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn(new GridColumn());

        grid.AddRow("[bold cyan]Command:[reset]", $"[bold white]{Markup.Escape(cmd.Name)}[/]");
        grid.AddRow("[bold cyan]Category:[reset]", $"[yellow]{Markup.Escape(cmd.Category)}[/]");
        grid.AddRow("[bold cyan]Description:[reset]", Markup.Escape(cmd.Description));
        grid.AddRow("[bold cyan]Syntax:[reset]", $"[green]{Markup.Escape(cmd.Syntax)}[/]");
        grid.AddRow("[bold cyan]Example:[reset]", $"[white]{Markup.Escape(cmd.Example)}[/]");

        if (cmd.Tags.Count > 0)
        {
            grid.AddRow("[bold cyan]Tags:[reset]", $"[grey]{string.Join(", ", cmd.Tags)}[/]");
        }

        var panel = new Panel(grid)
        {
            Header = new PanelHeader("[bold white] Command Selected [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.SteelBlue1),
            Padding = new Padding(1, 1, 1, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold white]Choose an action:[/]")
                .AddChoices(new[]
                {
                    "▶ Run Normally (CMD)",
                    "🛡️ Run as Administrator (Elevated UAC)",
                    "✎ Insert into Command Line for Editing",
                    "← Back to Search"
                }));

        if (action.StartsWith("▶"))
        {
            return new PaletteResult { Command = cmd.Name, AsAdmin = false, InsertOnly = false };
        }

        if (action.Contains("Administrator"))
        {
            return new PaletteResult { Command = cmd.Name, AsAdmin = true, InsertOnly = false };
        }

        if (action.StartsWith("✎"))
        {
            return new PaletteResult { Command = cmd.Name, AsAdmin = false, InsertOnly = true };
        }

        return null; // Back to search
    }
}
