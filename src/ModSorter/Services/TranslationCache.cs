using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ModSorter.Services;

// lang翻訳用の「原文→訳文」永続キャッシュ。
// ModCache と同じく ApplicationData\ModSorter\cache 配下に JSON で保存する。
// エンジンごとに訳が異なるため、エンジン名でファイルを分ける（今回は "deepl"）。
// キーは原文そのものではなくハッシュにする（原文が長文・特殊文字を含むため）。
public static class TranslationCache
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ModSorter", "cache");

    // 現在ロード中のエンジン名とエントリ。エンジンを切り替えたらロードし直す。
    private static string _engine = "";
    private static Dictionary<string, string> _entries = new();

    private static string FilePathFor(string engine) =>
        Path.Combine(Dir, $"trans_cache_{engine}.json");

    // 原文からキャッシュキー(SHA1のBase64)を作る。
    private static string KeyOf(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var hash = SHA1.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    // 指定エンジンのキャッシュをロードする。既にロード済みなら何もしない。
    public static void Load(string engine)
    {
        if (_engine == engine && _entries.Count >= 0 && !string.IsNullOrEmpty(_engine))
            return;
        _engine = engine;
        _entries = new();
        try
        {
            var path = FilePathFor(engine);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                           ?? new();
            }
        }
        catch { _entries = new(); }
    }

    public static void Save()
    {
        if (string.IsNullOrEmpty(_engine)) return;
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(_entries,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePathFor(_engine), json);
        }
        catch { }
    }

    // 原文に対応する訳文を返す。無ければ null。
    public static string? Get(string source)
    {
        if (string.IsNullOrEmpty(source)) return null;
        return _entries.TryGetValue(KeyOf(source), out var v) ? v : null;
    }

    // 原文→訳文を登録する（メモリ上。永続化は Save で行う）。
    public static void Put(string source, string translated)
    {
        if (string.IsNullOrEmpty(source)) return;
        _entries[KeyOf(source)] = translated ?? "";
    }

    // 指定した原文のキャッシュエントリを削除する（メモリ上。永続化は Save で行う）。
    // 壊れた訳を消して再翻訳させるために使う。削除できたら true。
    public static bool Remove(string source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        return _entries.Remove(KeyOf(source));
    }

    // 指定エンジンのキャッシュを全削除する。
    public static int ClearAll(string engine)
    {
        int count = (_engine == engine) ? _entries.Count : 0;
        if (_engine == engine) _entries = new();
        try
        {
            var path = FilePathFor(engine);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
        return count;
    }
}
