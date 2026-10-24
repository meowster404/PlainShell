namespace PlainShell.Catalog;

public class CommandItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string Syntax { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool IsCustom { get; set; } = false;

    public override string ToString() => $"{Name} ({Category}): {Description}";
}
