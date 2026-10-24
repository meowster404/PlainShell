namespace PlainShell.Engine;

public enum SuggestionKind
{
    Command,
    Subcommand,
    Directory,
    File,
    Flag,
    Snippet
}

public class SuggestionItem
{
    public string DisplayText { get; set; } = string.Empty;
    public string InsertText { get; set; } = string.Empty;
    public SuggestionKind Kind { get; set; } = SuggestionKind.Command;
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }

    public string KindBadge => Kind switch
    {
        SuggestionKind.Command => "cmd",
        SuggestionKind.Subcommand => "sub",
        SuggestionKind.Directory => "dir",
        SuggestionKind.File => "file",
        SuggestionKind.Flag => "flag",
        SuggestionKind.Snippet => "snip",
        _ => "item"
    };

    public override string ToString() => $"[{KindBadge}] {DisplayText} - {Description}";
}
