using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // MOD の lang JSON には末尾カンマやコメント入りのものが実在する。
    // 既定の厳格な解析だとファイル丸ごと解析失敗になるので緩めて読む。
    private static readonly JsonDocumentOptions LangJsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 64
    };

    // zip エントリをテキストとして読む。
    // UTF-8 として不正なら Latin-1 で読み直す。古い .lang には UTF-8 でない
    // バイト列が残っているものがあり、UTF-8 固定だと例外か文字化けになる。
    private static string ReadEntry(ZipArchiveEntry e)
    {
        byte[] bytes;
        using (var s = e.Open())
        using (var ms = new MemoryStream())
        {
            s.CopyTo(ms);
            bytes = ms.ToArray();
        }

        int offset = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            offset = 3; // BOM を落とす

        try
        {
            return new UTF8Encoding(false, true)
                .GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes, offset, bytes.Length - offset);
        }
    }

    // en_us.json: 文字列値のみ採用。配列/数値/ネストは lang の値ではないので対象外。
    private static Dictionary<string, string> ParseJson(string text)
    {
        var dict = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(text, LangJsonOptions);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return dict;
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String)
                dict[p.Name] = p.Value.GetString() ?? "";
        }
        return dict;
    }

    // en_us.lang: key=value 行。空行と # コメントは無視。
    private static Dictionary<string, string> ParseLang(string text)
    {
        var dict = new Dictionary<string, string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.TrimStart().StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line.Substring(0, eq);
            var val = line.Substring(eq + 1);
            dict[key] = val;
        }
        return dict;
    }

    // 文字列 s の中に部分文字列 sub が何回現れるかを数える(重なりなし)。
    private static int CountOccurrences(string s, string sub)
    {
        if (string.IsNullOrEmpty(sub)) return 0;
        int count = 0, idx = 0;
        while ((idx = s.IndexOf(sub, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += sub.Length;
        }
        return count;
    }
}
