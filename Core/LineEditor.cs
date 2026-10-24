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
        bool suggestionsVisible = true;
        int lastRenderedPopupHeight = 0;

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
            lastRenderedPopupHeight = RenderIntelliSenseOverlay(suggestions, selectedSuggestionIndex, promptLeft, promptTop, buffer.ToString(), cursor, lastRenderedPopupHeight);

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
                Console.Clear();
                CommandExecutor.PrintWelcomeBanner();
                RenderPrompt();
                promptLeft = Console.CursorLeft;
                promptTop = Console.CursorTop;
                Console.Write(buffer.ToString());
                Console.SetCursorPosition(promptLeft + cursor, promptTop);
                continue;
            }

            // 4. Escape -> Dismiss suggestions popup
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

            // 7. Arrow Up / Down -> Navigate suggestions or history
            if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (suggestions.Count > 0 && suggestionsVisible)
                {
                    selectedSuggestionIndex = (selectedSuggestionIndex + 1) % suggestions.Count;
                }
                else
                {
                    // History forward
                    string? next = _context.GetNextHistory();
                    if (next != null)
                    {
                        buffer.Clear();
                        buffer.Append(next);
                        cursor = buffer.Length;
                        suggestionsVisible = false;
                        RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                    }
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (suggestions.Count > 0 && suggestionsVisible)
                {
                    selectedSuggestionIndex = (selectedSuggestionIndex - 1 + suggestions.Count) % suggestions.Count;
                }
                else
                {
                    // History back
                    string? prev = _context.GetPreviousHistory();
                    if (prev != null)
                    {
                        buffer.Clear();
                        buffer.Append(prev);
                        cursor = buffer.Length;
                        suggestionsVisible = false;
                        RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                    }
                }
                continue;
            }

            // 8. Navigation keys (Left, Right, Home, End)
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

            // 9. Backspace & Delete
            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    buffer.Remove(cursor - 1, 1);
                    cursor--;
                    suggestionsVisible = true;
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
                    suggestionsVisible = true;
                    selectedSuggestionIndex = 0;
                    RedrawBuffer(promptLeft, promptTop, buffer.ToString(), cursor);
                }
                continue;
            }

            // 10. Printable characters
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
            Console.Write(text + "   "); // Clear trailing chars
            Console.SetCursorPosition(promptLeft + cursor, promptTop);
        }
        catch
        {
            // Ignore console out of bounds if terminal resized
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
            // Not enough space below, avoid drawing off-screen
            ClearPopupArea(promptTop, previousHeight);
            return 0;
        }

        int boxWidth = Math.Min(75, Math.Max(45, bufferWidth - 4));
        int startRow = promptTop + 1;

        // Header
        Console.SetCursorPosition(0, startRow);
        Console.Write($"\u001b[90m┌─ IntelliSense Suggestions ────────────── [Tab] Insert  [↑/↓] Select ─┐\u001b[0m".PadRight(boxWidth));

        int rowCount = 1;

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

            string prefix = isSelected ? " \u001b[1;36m▶\u001b[0m " : "   ";
            string namePart = item.DisplayText;
            if (namePart.Length > 24) namePart = namePart.Substring(0, 21) + "...";

            string descPart = item.Description;
            int remainingDescSpace = boxWidth - 36;
            if (remainingDescSpace > 5 && descPart.Length > remainingDescSpace)
            {
                descPart = descPart.Substring(0, remainingDescSpace - 3) + "...";
            }

            string content;
            if (isSelected)
            {
                // Highlight line with dark cyan/blue background
                content = $"{prefix}{badgeColor}{badge,-6}\u001b[0m \u001b[44;1;37m{namePart,-24}\u001b[0m  \u001b[90m{descPart}\u001b[0m";
            }
            else
            {
                content = $"{prefix}{badgeColor}{badge,-6}\u001b[0m \u001b[37m{namePart,-24}\u001b[0m  \u001b[90m{descPart}\u001b[0m";
            }

            // Print with right border
            Console.Write($"\u001b[90m│\u001b[0m{content}");
            int currentLeft = Console.CursorLeft;
            if (currentLeft < boxWidth - 1)
            {
                Console.Write(new string(' ', (boxWidth - 1) - currentLeft));
            }
            Console.Write("\u001b[90m│\u001b[0m\u001b[K");
            rowCount++;
        }

        // Footer
        Console.SetCursorPosition(0, startRow + 1 + maxPopupHeight);
        Console.Write($"\u001b[90m└─ [F1 / Ctrl+P] All Commands Catalog ───────────────────────────────┘\u001b[0m".PadRight(boxWidth));
        rowCount++;

        // Clear any old rows if previous popup was taller
        if (previousHeight > rowCount)
        {
            for (int r = startRow + rowCount; r <= startRow + previousHeight; r++)
            {
                Console.SetCursorPosition(0, r);
                Console.Write("\u001b[2K");
            }
        }

        // Return cursor to input line
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
                    Console.Write("\u001b[2K"); // ANSI clear entire line
                }
            }
        }
        catch
        {
            // Ignore boundary errors
        }
    }
}
