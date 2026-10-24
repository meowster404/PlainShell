using PlainShell.Core;
using Spectre.Console;

namespace PlainShell.Catalog;

public class PaletteView
{
    private readonly CommandCatalog _catalog;

    public PaletteView(CommandCatalog catalog)
    {
        _catalog = catalog;
    }

    public string? Show()
    {
        ConsoleHelper.SafeClear();

        AnsiConsole.Write(
            new Rule("[bold cyan]PlainShell Categorized Command Palette[/]")
                .LeftJustified()
                .RuleStyle("steelBlue1"));

        var categories = new List<string> { "★ All Categories" };
        categories.AddRange(_catalog.GetCategories());
        categories.Add("+ Add Custom Command");
        categories.Add("← Back to Terminal");

        var selectedCategory = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold white]Choose a Category to explore or filter commands:[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Black, Color.Cyan1, Decoration.Bold))
                .AddChoices(categories));

        if (selectedCategory == "← Back to Terminal")
        {
            ConsoleHelper.SafeClear();
            return null;
        }

        if (selectedCategory == "+ Add Custom Command")
        {
            AddNewCustomCommand();
            return null;
        }

        string? categoryFilter = selectedCategory == "★ All Categories"
            ? null
            : selectedCategory;

        return ShowCommandList(categoryFilter);
    }

    private string? ShowCommandList(string? categoryFilter)
    {
        while (true)
        {
            ConsoleHelper.SafeClear();
            string headerText = categoryFilter != null
                ? $"Commands in [bold yellow]{categoryFilter}[/]"
                : "All Registered Commands";

            AnsiConsole.MarkupLine($"[bold cyan]{headerText}[/]");
            AnsiConsole.MarkupLine("[grey]Type to search by name, description, or keyword. Select to run or edit.[/]\n");

            // Option 1: Search query prompt or list
            string query = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Search query[/] [grey](press Enter to see all):[/]")
                    .AllowEmpty());

            var matches = _catalog.Search(query, categoryFilter);

            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No commands matched your search.[/]\n");
                if (AnsiConsole.Confirm("Try another search?", defaultValue: true))
                {
                    continue;
                }
                return null;
            }

            var prompt = new SelectionPrompt<CommandItem>()
                .Title("[bold white]Select a command from the catalog:[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Black, Color.Cyan1, Decoration.Bold))
                .UseConverter(c => $"[{c.Category}] {c.Name} - {c.Description}");

            foreach (var cmd in matches)
            {
                prompt.AddChoice(cmd);
            }

            var selectedCommand = AnsiConsole.Prompt(prompt);

            // Display rich command details
            ConsoleHelper.SafeClear();
            DisplayCommandDetails(selectedCommand);

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold white]What would you like to do with this command?[/]")
                    .AddChoices(new[]
                    {
                        "▶ Run Immediately",
                        "✎ Insert into Command Line for Editing",
                        "🔍 Search Again",
                        "← Back to Shell"
                    }));

            if (action.StartsWith("▶"))
            {
                ConsoleHelper.SafeClear();
                return selectedCommand.Name;
            }

            if (action.StartsWith("✎"))
            {
                ConsoleHelper.SafeClear();
                // Return marked command with prefix for insertion
                return "INSERT:" + selectedCommand.Name;
            }

            if (action.StartsWith("←"))
            {
                ConsoleHelper.SafeClear();
                return null;
            }
        }
    }

    private void DisplayCommandDetails(CommandItem cmd)
    {
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
            Header = new PanelHeader("[bold white] Command Information [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.SteelBlue1),
            Padding = new Padding(1, 1, 1, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private void AddNewCustomCommand()
    {
        ConsoleHelper.SafeClear();
        AnsiConsole.Write(new Rule("[bold green]Add Custom Command to PlainShell[/]").LeftJustified());

        string name = AnsiConsole.Ask<string>("Enter command [cyan]Name[/] (e.g. `docker compose logs`):");
        string category = AnsiConsole.Ask<string>("Enter [cyan]Category[/] (e.g. `Work Projects`, `Docker`, `Dev`):", "Custom User Commands");
        string desc = AnsiConsole.Ask<string>("Enter [cyan]Description[/]:");
        string syntax = AnsiConsole.Ask<string>("Enter [cyan]Syntax[/] (or press Enter to match name):", name);
        string example = AnsiConsole.Ask<string>("Enter [cyan]Example[/]:", name);
        string tagsStr = AnsiConsole.Ask<string>("Enter [cyan]Tags[/] (comma-separated):", "");

        var tags = tagsStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

        var newItem = new CommandItem
        {
            Name = name,
            Category = category,
            Description = desc,
            Syntax = syntax,
            Example = example,
            Tags = tags,
            IsCustom = true
        };

        _catalog.AddCustomCommand(newItem);
        AnsiConsole.MarkupLine("[bold green]✓ Custom command saved successfully![/]\n");
        Thread.Sleep(1200);
        ConsoleHelper.SafeClear();
    }
}
