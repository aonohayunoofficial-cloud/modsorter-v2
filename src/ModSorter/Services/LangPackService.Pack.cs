using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // pack_format は単一バージョンしか表せず、値が合わないと
    // リソースパック一覧で「非対応」に落ちる。lang だけのパックは
    // どのバージョンでも構造が同じなので、supported_formats で範囲を宣言して
    // 対象インスタンスのバージョンに関係なく読ませる。
    // supported_formats は 1.20.2(format 18)で追加され、それ以前のクライアントは
    // 未知フィールドとして無視する。出典: minecraft.wiki "Pack format"。
    private const int PackFormatMin = 15; // 1.20
    private const int PackFormatMax = 99; // 将来のバージョンまで許容する上限

    private static readonly JsonSerializerOptions McMetaOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 説明文の日本語をそのまま書く
    };

    // ===== 4) パック生成 =====
    // 翻訳辞書をもとに ja_jp.json を名前空間ごとに書き、1つの zip にまとめる。
    public static void BuildPack(
        IEnumerable<NamespaceLang> targets,
        Dictionary<string, string> translations,
        string outputZipPath,
        int packFormat,
        LangPackResult result)
    {
        var dir = Path.GetDirectoryName(outputZipPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // 既存ファイルがロックされている場合はリトライしながら削除する。
        // ロックが解けない場合でも FileMode.Create が上書きするため、削除失敗は無視してよい。
        for (int attempt = 0; File.Exists(outputZipPath) && attempt < 5; attempt++)
        {
            try { File.Delete(outputZipPath); break; }
            catch (IOException) { System.Threading.Thread.Sleep(200); }
        }

        using var fs = new FileStream(outputZipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        int declared = Math.Clamp(packFormat, PackFormatMin, PackFormatMax);
        var mcmeta = new
        {
            pack = new
            {
                pack_format = declared,
                supported_formats = new
                {
                    min_inclusive = PackFormatMin,
                    max_inclusive = PackFormatMax
                },
                description = "ModSorter 自動生成 日本語化パック"
            }
        };
        WriteZipText(zip, "pack.mcmeta", JsonSerializer.Serialize(mcmeta, McMetaOptions));

        // 各名前空間の ja_jp.json
        // パックは MOD 同梱の ja_jp を丸ごと置き換えるため、同梱の訳もここに載せ直す。
        foreach (var t in targets)
        {
            var outMap = new Dictionary<string, string>();
            foreach (var kv in t.Entries)
            {
                // 翻訳対象外で同梱の訳があるキーは、その訳をそのまま維持する。
                if (!t.TranslateKeys.Contains(kv.Key) &&
                    t.Existing.TryGetValue(kv.Key, out var keep))
                {
                    outMap[kv.Key] = keep;
                    continue;
                }
                var src = kv.Value;
                outMap[kv.Key] =
                    (!string.IsNullOrEmpty(src) && translations.TryGetValue(src, out var tr))
                        ? tr : src;
            }

            // en_us に無いキーが同梱 ja_jp にだけある場合も落とさない。
            foreach (var kv in t.Existing)
                if (!outMap.ContainsKey(kv.Key)) outMap[kv.Key] = kv.Value;

            WriteZipText(zip, $"assets/{t.Namespace}/lang/ja_jp.json",
                SerializeLangJson(outMap));
        }

        result.OutputPath = outputZipPath;
    }

    // ja_jp.json を UTF-8(エスケープなし)・入力順で直列化する。
    private static string SerializeLangJson(Dictionary<string, string> map)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        int i = 0;
        foreach (var kv in map)
        {
            sb.Append("  ");
            sb.Append(JsonEncode(kv.Key));
            sb.Append(": ");
            sb.Append(JsonEncode(kv.Value));
            if (i < map.Count - 1) sb.Append(',');
            sb.Append('\n');
            i++;
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    // 日本語をエスケープせず、必要な制御文字/引用符だけをエスケープする。
    private static string JsonEncode(string s)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static void WriteZipText(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var s = entry.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(false)); // BOMなし
        w.Write(content);
    }
}
