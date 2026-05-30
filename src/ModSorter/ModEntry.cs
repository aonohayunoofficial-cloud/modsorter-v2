namespace ModSorter;

public class ModEntry
{
    public string FileName { get; set; } = "";
    public string ModId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Loader { get; set; } = "";
    public string DisplayName => string.IsNullOrEmpty(ModId) || ModId.StartsWith("(")
        ? FileName : ModId;
    public string Url { get; set; } = "";
}
