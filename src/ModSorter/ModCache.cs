using System.IO;
using System.Net.Http;
using System.Text.Json;

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

public static class ModCache
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ModSorter", "cache");
    private static readonly string IconDir = Path.Combine(Dir, "icons");
    private static readonly string FilePath = Path.Combine(Dir, "mod_cache.json");

    private static Dictionary<string, CacheEntry> _entries = new();

    private static readonly HttpClient Http = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _entries = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json)
                           ?? new();
            }
        }
        catch { _entries = new(); }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(_entries,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    // キャッシュエントリを返す。無ければnull(TTLは廃止: 一度取得したら無期限保持)
    public static CacheEntry? Get(string sha1)
    {
        if (_entries.TryGetValue(sha1, out var e))
            return e;
        return null;
    }

    public static void Put(CacheEntry e)
    {
        e.CachedAt = DateTime.UtcNow;
        _entries[e.Sha1] = e;
    }

    // アイコンをローカルに保存し、ファイルパスを返す。失敗時は空
    public static async Task<string> EnsureIconAsync(string sha1, string iconUrl)
    {
        if (string.IsNullOrEmpty(iconUrl)) return "";
        try
        {
            Directory.CreateDirectory(IconDir);
            // 拡張子をURLから推定(なければpng)
            var ext = Path.GetExtension(new Uri(iconUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".png";
            var path = Path.Combine(IconDir, sha1 + ext);

            if (File.Exists(path)) return path;

            var bytes = await Http.GetByteArrayAsync(iconUrl);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch
        {
            return "";
        }
    }
}
