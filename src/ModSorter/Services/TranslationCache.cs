using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
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

    // 保存オプション。既定のエンコーダだと日本語が "\u6625" になり目視できないため、
    // 緩和エンコーダでそのまま書き出す（キャッシュを手で覗くとき用）。
    // 出力先は自前の JSON ファイルのみで HTML に埋め込まないため、これで問題ない。
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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
        if (!string.IsNullOrEmpty(_engine) && _engine == engine)
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
            var json = JsonSerializer.Serialize(_entries, SaveOptions);
            File.WriteAllText(FilePathFor(_engine), json);
        }
        catch { }
    }

    // 現在ロード中エンジンのエントリ数(UI表示用)。
    public static int Count => _entries.Count;

    // キャッシュファイルの実パス(UI/ログ表示用)。
    public static string PathOf(string engine) => FilePathFor(engine);

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

    // 訳文に needle を含むエントリ数を数える（削除前の確認用）。
    // キーはハッシュなので原文からは引けないが、訳文なら走査で探せる。
    public static int CountWhereTranslatedContains(string engine, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        Load(engine);
        return _entries.Count(kv =>
            !string.IsNullOrEmpty(kv.Value) &&
            kv.Value.Contains(needle, StringComparison.Ordinal));
    }

    // 訳文に needle を含むエントリだけを削除する。戻り値は削除件数。
    // 「春」のような誤訳語を指定すると、その訳になった原文だけが次回再翻訳される。
    // 全消しと違い DeepL 枠の消費を最小に抑えられる。削除後は即座に永続化する。
    public static int RemoveWhereTranslatedContains(string engine, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        Load(engine);
        var keys = _entries
            .Where(kv => !string.IsNullOrEmpty(kv.Value) &&
                         kv.Value.Contains(needle, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var k in keys) _entries.Remove(k);
        if (keys.Count > 0) Save();
        return keys.Count;
    }

    // 指定エンジンのキャッシュを全削除する。
    // 未ロードのエンジンを指定された場合でも件数を正しく返すため、先にロードする。
    public static int ClearAll(string engine)
    {
        Load(engine);
        int count = _entries.Count;
        _entries = new();
        try
        {
            var path = FilePathFor(engine);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
        return count;
    }
}
