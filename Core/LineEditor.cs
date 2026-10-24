using System.Text;
using PlainShell.Engine;
using Spectre.Console;

namespace PlainShell.Core;

public class LineEditorResult
{
    public string? Command { get; set; }
    public bool OpenPaletteRequested { get; set; }
    public bool Cancelled { get; set; }
}

public class LineEditor
{
    private readonly SuggestionEngine _suggestionEngine;
    private readonly ShellContext _context;

    public LineEditor(SuggestionEngine suggestionEngine, ShellContext context)
    {
        _suggestionEngine = suggestionEngine;
        _context = context;
    }

    public LineEditorResult ReadLine(string? initialText = null)
    {
        var buffer = new StringBuilder(initialText ?? string.Empty);
        int cursor = buffer.Length;
        int selectedSuggestionIndex = 0;
        bool suggestionsVisible = buffer.Length > 0;
        int lastRenderedPopupHeight = 0;

        // Reset history pointer for each prompt line
        _context.ResetHistoryIndex();

        // Render prompt and record starting position
        RenderPrompt();
        int promptLeft = Console.CursorLeft;
        int promptTop = Console.CursorTop;

        // Initial render if text provided
        if (buffer.Length > 0)
        {
            Console.Write(buffer.ToString());
            Console.SetCursorPosition(promptLeft + cursor, promptTop);
        }

        while (true)
        {
            // Query suggestions based on current buffer state
            var suggestions = suggestionsVisible
                ? _suggestionEngine.GetSuggestions(buffer.ToString(), cursor, _context.CurrentDirectory)
                : new List<SuggestionItem>();

            // Clamp selection
            if (suggestions.Count == 0)
            {
                selectedSuggestionIndex = 0;
            }
            else if (selectedSuggestionIndex >= suggestions.Count)
            {
                selectedSuggestionIndex = suggestions.Count - 1;
            }
            else if (selectedSuggestionIndex < 0)
            {
                selectedSuggestionIndex = 0;
            }

            // Render suggestion popup overlay
            lastRenderedPopupHeight = RenderIntelliSenseOverlay(
                suggestions,
                selectedSuggestionIndex,
                promptLeft,
                promptTop,
                buffer.ToString(),
                cursor,
                lastRenderedPopupHeight);

            // Read keypress
            var keyInfo = Console.ReadKey(intercept: true);

            // 1. Hotkey: F1 or Ctrl+P -> Open Categorized Command Palette
            if (keyInfo.Key == ConsoleKey.F1 || (keyInfo.Key == ConsoleKey.P && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control)))
            {
                ClearPopupArea(promptTop, lastRenderedPopupHeight);
                return new LineEditorResult { OpenPaletteRequested = true };
            }

            // 2. Ctrl+C -> Cancel current line
            if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                ClearPopupArea(promptTop, lastRenderedPopupHeight);
                Console.WriteLine("^C");
                return new LineEditorResult { Cancelled = true };
            }

            // 3. Ctrl+L -> Clear screen
            if (keyInfo.Key == ConsoleKey.L && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                ClearPopupArea(promptTop, lastRenderedPopupHeight);
                ConsoleHelper.SafeClear();
                CommandExecutor.PrintWelcomeBanner();
                RenderPrompt();
                promptLeft = Console.CursorLeft;
                promptTop = Console.CursorTop;
                Console.Write(buffer.ToString());
                Console.SetCursorPosition(promptLeft + cursor, promptTop);
                continue;
            }

            // 4. Escape -> Dismiss suggestions popup and allow free history navigation
            if (keyInfo.Key == ConsoleKey.Escape)
            {
                suggestionsVisible = false;
                ClearPopupArea(promptTop, lastRenderedPopupHeight);
                lastRenderedPopupHeight = 0;
                continue;
            }

            // 5. Enter -> Submit command or apply suggestion
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                ClearPopupArea(promptTop, lastRenderedPopupHeight);
                Console.WriteLine();
                return new LineEditorResult { Command = buffer.ToString() };
            }

            // 6. Tab -> Autocomplete active suggestion
            if (keyInfo.Key == ConsoleKey.Tab)
            {
                if (suggestions.Count > 0 && selectedSuggestionIndex >= 0 && selectedSuggestionIndex < suggestions.Count)
                {
                    var selected = suggestions[selectedSuggestionIndex];
                    ApplySuggestion(buffer, ref cursor, selected);
                    suggestionsVisible = true;
                    selectedSuggestionIndex = 0;
                    RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                }
                continue;
            }

            // 7. Arrow Up -> Previous Command History or Suggestion Up
            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                // If suggestions are visible and user is not at top item, move up in suggestions
                if (suggestions.Count > 0 && suggestionsVisible && selectedSuggestionIndex > 0)
                {
                    selectedSuggestionIndex--;
                }
                else
                {
                    // History backward (walk previous commands)
                    string? prev = _context.GetPreviousHistory();
                    if (prev != null)
                    {
                        ClearPopupArea(promptTop, lastRenderedPopupHeight);
                        lastRenderedPopupHeight = 0;
                        buffer.Clear();
                        buffer.Append(prev);
                        cursor = buffer.Length;
                        suggestionsVisible = false; // Keep closed while cycling history
                        RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                    }
                }
                continue;
            }

            // 8. Arrow Down -> Next Command History or Suggestion Down
            if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                // If suggestions are visible and not at the end, move down in suggestions
                if (suggestions.Count > 0 && suggestionsVisible && selectedSuggestionIndex < suggestions.Count - 1)
                {
                    selectedSuggestionIndex++;
                }
                else if (suggestions.Count > 0 && suggestionsVisible && selectedSuggestionIndex == suggestions.Count - 1)
                {
                    // Loop back to top
                    selectedSuggestionIndex = 0;
                }
                else
                {
                    // History forward (walk toward current)
                    string? next = _context.GetNextHistory();
                    if (next != null)
                    {
                        ClearPopupArea(promptTop, lastRenderedPopupHeight);
                        lastRenderedPopupHeight = 0;
                        buffer.Clear();
                        buffer.Append(next);
                        cursor = buffer.Length;
                        suggestionsVisible = false;
                        RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                    }
                }
                continue;
            }

            // 9. Navigation keys (Left, Right, Home, End)
            if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                if (cursor > 0)
                {
                    cursor--;
                    Console.SetCursorPosition(promptLeft + cursor, promptTop);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                if (cursor < buffer.Length)
                {
                    cursor++;
                    Console.SetCursorPosition(promptLeft + cursor, promptTop);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Home)
            {
                cursor = 0;
                Console.SetCursorPosition(promptLeft, promptTop);
                continue;
            }

            if (keyInfo.Key == ConsoleKey.End)
            {
                cursor = buffer.Length;
                Console.SetCursorPosition(promptLeft + cursor, promptTop);
                continue;
            }

            // 10. Backspace & Delete
            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    buffer.Remove(cursor - 1, 1);
                    cursor--;
                    suggestionsVisible = buffer.Length > 0;
                    selectedSuggestionIndex = 0;
                    RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Delete)
            {
                if (cursor < buffer.Length)
                {
                    buffer.Remove(cursor, 1);
                    suggestionsVisible = buffer.Length > 0;
                    selectedSuggestionIndex = 0;
                    RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                }
                continue;
            }

            // 11. Printable characters -> insert and open suggestions for active token
            if (!char.IsControl(keyInfo.KeyChar))
            {
                buffer.Insert(cursor, keyInfo.KeyChar);
                cursor++;
                suggestionsVisible = true;
                selectedSuggestionIndex = 0;
                RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
            }
        }
    }

    private void ApplySuggestion(StringBuilder buffer, ref int cursor, SuggestionItem selected)
    {
        string currentText = buffer.ToString().Substring(0, cursor);
        string remainder = cursor < buffer.Length ? buffer.ToString().Substring(cursor) : string.Empty;

        int lastSpace = currentText.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            // Replacing first command token
            buffer.Clear();
            buffer.Append(selected.InsertText);
            if (!selected.InsertText.EndsWith(' ') && selected.Kind == SuggestionKind.Command)
            {
                buffer.Append(' ');
            }
            buffer.Append(remainder);
            cursor = selected.InsertText.Length + (selected.Kind == SuggestionKind.Command && !selected.InsertText.EndsWith(' ') ? 1 : 0);
        }
        else
        {
            // Replacing active argument token (e.g. 'cd Ass' -> 'cd Assets/')
            string prefix = currentText.Substring(0, lastSpace + 1);
            buffer.Clear();
            buffer.Append(prefix);
            buffer.Append(selected.InsertText);
            buffer.Append(remainder);
            cursor = prefix.Length + selected.InsertText.Length;
        }
    }

    private void RenderPrompt()
    {
        string dir = _context.CurrentDirectory;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string displayDir = dir.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + dir.Substring(home.Length)
            : dir;

        string? branch = _context.GetGitBranch();

        // Modern stylized prompt with branch badge
        AnsiConsole.Markup($"[bold deepskyblue1]{Markup.Escape(displayDir)}[/]");
        if (!string.IsNullOrEmpty(branch))
        {
            AnsiConsole.Markup($" [bold darkorange3]({Markup.Escape(branch)})[/]");
        }
        AnsiConsole.Markup(" [bold cyan]❯[/] ");
    }

    private static void RedrawBuffer(int promptLeft, int promptTop, string text, int cursor)
    {
        try
        {
            Console.SetCursorPosition(promptLeft, promptTop);
            Console.Write(text + "        "); // Clear trailing characters
            Console.SetCursorPosition(promptLeft + cursor, promptTop);
        }
        catch
        {
            // Ignore boundary errors if terminal is small
        }
    }

    private int RenderIntelliSenseOverlay(
        List<SuggestionItem> suggestions,
        int selectedIndex,
        int promptLeft,
        int promptTop,
        string bufferText,
        int cursor,
        int previousHeight)
    {
        if (suggestions.Count == 0)
        {
            ClearPopupArea(promptTop, previousHeight);
            return 0;
        }

        int bufferHeight = Console.BufferHeight;
        int bufferWidth = Console.BufferWidth;
        int maxPopupHeight = Math.Min(6, suggestions.Count);
        int availableRowsBelow = bufferHeight - promptTop - 2;

        if (availableRowsBelow < maxPopupHeight + 2)
        {
            ClearPopupArea(promptTop, previousHeight);
            return 0;
        }

        // Exact box width calculation
        int boxWidth = Math.Min(78, Math.Max(50, bufferWidth - 2));
        int innerContentWidth = boxWidth - 2; // Width inside the vertical borders
        int startRow = promptTop + 1;

        // 1. Build Top Header with mathematically exact width
        string title = "┌─ IntelliSense Suggestions ─";
        string hint = " [Tab] Insert  [↑/↓] Select ─┐";
        int fillDashes = boxWidth - title.Length - hint.Length;
        if (fillDashes < 1) fillDashes = 1;
        string topBorder = $"\u001b[90m{title}{new string('─', fillDashes)}{hint}\u001b[0m";

        Console.SetCursorPosition(0, startRow);
        Console.Write(topBorder + "\u001b[K");

        int rowCount = 1;

        // 2. Render each suggestion row
        for (int i = 0; i < maxPopupHeight; i++)
        {
            int row = startRow + 1 + i;
            var item = suggestions[i];
            bool isSelected = i == selectedIndex;

            Console.SetCursorPosition(0, row);

            string badge = $"[{item.KindBadge}]";
            string badgeColor = item.Kind switch
            {
                SuggestionKind.Command => "\u001b[36m",    // Cyan
                SuggestionKind.Subcommand => "\u001b[33m", // Yellow
                SuggestionKind.Directory => "\u001b[32m",  // Green
                SuggestionKind.File => "\u001b[37m",       // White
                SuggestionKind.Flag => "\u001b[35m",       // Magenta
                _ => "\u001b[90m"
            };

            string prefix = isSelected ? " ▶ " : "   ";
            string namePart = item.DisplayText;
            if (namePart.Length > 24) namePart = namePart.Substring(0, 21) + "...";
            namePart = namePart.PadRight(24);

            // Compute exact space remaining for description
            // Prefix: 3, Badge: 6, Space: 1, Name: 24, Space: 2 = 36 characters
            int descSpace = innerContentWidth - 36;
            string descPart = item.Description;
            if (descSpace > 3 && descPart.Length > descSpace)
            {
                descPart = descPart.Substring(0, descSpace - 3) + "...";
            }
            if (descSpace > 0)
            {
                descPart = descPart.PadRight(descSpace);
            }

            // Build colored line
            string coloredLine;
            if (isSelected)
            {
                coloredLine = $"\u001b[1;36m{prefix}\u001b[0m{badgeColor}{badge,-6}\u001b[0m \u001b[44;1;37m{namePart}\u001b[0m  \u001b[90m{descPart}\u001b[0m";
            }
            else
            {
                coloredLine = $"{prefix}{badgeColor}{badge,-6}\u001b[0m \u001b[37m{namePart}\u001b[0m  \u001b[90m{descPart}\u001b[0m";
            }

            // Print with borders aligned to exact column
            Console.Write($"\u001b[90m│\u001b[0m{coloredLine}\u001b[90m│\u001b[0m\u001b[K");
            rowCount++;
        }

        // 3. Build Bottom Footer with mathematically exact width
        string footerTitle = "└─ [F1 / Ctrl+P] Command Catalog ";
        int footerFill = boxWidth - footerTitle.Length - 1;
        if (footerFill < 1) footerFill = 1;
        string bottomBorder = $"\u001b[90m{footerTitle}{new string('─', footerFill)}┘\u001b[0m";

        Console.SetCursorPosition(0, startRow + 1 + maxPopupHeight);
        Console.Write(bottomBorder + "\u001b[K");
        rowCount++;

        // Clear any leftover lines if previous popup was taller
        if (previousHeight > rowCount)
        {
            for (int r = startRow + rowCount; r <= startRow + previousHeight; r++)
            {
                Console.SetCursorPosition(0, r);
                Console.Write("\u001b[2K");
            }
        }

        // Return cursor to input position
        Console.SetCursorPosition(promptLeft + cursor, promptTop);

        return rowCount;
    }

    private static void ClearPopupArea(int promptTop, int rowCount)
    {
        if (rowCount <= 0) return;

        try
        {
            for (int i = 1; i <= rowCount + 1; i++)
            {
                int row = promptTop + i;
                if (row < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, row);
                    Console.Write("\u001b[2K"); // Clear entire line
                }
            }
        }
        catch
        {
            // Ignore boundary errors
        }
    }
}
