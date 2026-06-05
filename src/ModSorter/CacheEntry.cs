namespace ModSorter;

public class CacheEntry
{
    public string Sha1 { get; set; } = "";
    public string ModId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Loader { get; set; } = "";
    public string ModrinthUrl { get; set; } = "";
    public string CurseForgeUrl { get; set; } = "";
    public string Body { get; set; } = "";
    public bool BodyIsHtml { get; set; }
    public string IconUrl { get; set; } = "";
    public string IconFile { get; set; } = ""; // ローカル保存したアイコンの絶対パス
    public List<string> Categories { get; set; } = new(); // API由来のカテゴリ
    public string CategorySource { get; set; } = "";       // "CurseForge" / "Modrinth"
    public DateTime CachedAt { get; set; }
}
