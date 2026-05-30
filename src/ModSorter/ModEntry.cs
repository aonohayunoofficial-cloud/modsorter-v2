namespace ModSorter;

public class ModEntry
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ModId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Loader { get; set; } = "";
    public string DisplayName => string.IsNullOrEmpty(ModId) || ModId.StartsWith("(")
        ? FileName : ModId;

    public string ModrinthUrl { get; set; } = "";
    public string CurseForgeUrl { get; set; } = "";
    public string Body { get; set; } = "";        // MODページ本文(原文)
    public string TranslatedBody { get; set; } = ""; // 翻訳後(Day 3後半)

    // 旧コードとの互換用
    public string Url { get; set; } = "";
}
