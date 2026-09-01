using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // 復元しきれずに残った <x .../> を掃除するための一致。
    // 本文側の < > はエスケープ済みなので、ここに引っかかるのは退避タグだけ。
    private static readonly Regex LeftoverTagRegex = new(
        @"<\s*/?\s*x\b[^>]*>", RegexOptions.Compiled);

    // XML 実体参照。DeepL は &amp; 以外に &quot; &#39; を返すことがある。
    private static readonly Regex XmlEntityRegex = new(
        @"&(?:amp|lt|gt|quot|apos|#(\d+)|#[xX]([0-9a-fA-F]+));", RegexOptions.Compiled);

    // プレースホルダ/色コード/用語辞書の語を <x id="n"/> タグに退避し、
    // 本文の < > & はエスケープする。戻り値は(退避後テキスト, id -> 復元する文字列)。
    //
    // プレースホルダは「元の断片」を、用語は「日本語の訳語」を復元値に入れる。
    // どちらも DeepL からは同じ翻訳対象外タグに見えるため、用語は訳されずに位置だけ保たれ、
    // 復元時に指定の訳語へ置き換わる。これで "Spring" が「春」になる取り違えを断てる。
    //
    // 退避範囲が重なるとタグが入れ子になって壊れるので、プレースホルダを先に確定し、
    // それと重なる用語一致は捨てる。プレースホルダの保護を常に優先する。
    private static (string, Dictionary<string, string>) ProtectXml(string src)
    {
        var map = new Dictionary<string, string>();
        var sb = new StringBuilder();
        int idx = 0;
        int pos = 0;

        // 退避する範囲を (開始, 長さ, 復元値) で集める。
        var spans = new List<(int Start, int Length, string Restore)>();

        // プレースホルダ/色コード。復元値は元の断片そのもの。
        foreach (Match m in PlaceholderRegex.Matches(src))
            spans.Add((m.Index, m.Length, m.Value));

        // 用語辞書。復元値は日本語の訳語。
        foreach (var g in GlossaryService.FindMatches(src))
        {
            int gEnd = g.Start + g.Length;
            bool clash = spans.Any(s => g.Start < s.Start + s.Length && s.Start < gEnd);
            if (clash) continue; // プレースホルダと重なる用語は退避しない
            spans.Add((g.Start, g.Length, g.Target));
        }

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));

        foreach (var sp in spans)
        {
            if (sp.Start > pos)
                sb.Append(XmlEscape(src.Substring(pos, sp.Start - pos)));

            var id = idx.ToString();
            map[id] = sp.Restore;
            sb.Append($"<x id=\"{id}\"/>");
            idx++;
            pos = sp.Start + sp.Length;
        }
        if (pos < src.Length)
            sb.Append(XmlEscape(src.Substring(pos)));

        return (sb.ToString(), map);
    }

    // 訳文を元へ戻す。<x id="n"/> を復元値に、エスケープを元文字に戻す。
    // DeepL は自己終了タグを <x id="0"></x> の形へ書き換えることがあるため、
    // 閉じタグ付きも同じ一致で拾う。拾えないと復元漏れとして無駄に積み上がる。
    private static string RestoreXml(string translated, Dictionary<string, string> map,
        LangPackResult result, string source)
    {
        var outText = translated;
        bool warned = false;

        foreach (var kv in map)
        {
            var pattern =
                "<\\s*x\\s+id\\s*=\\s*[\"']?" + Regex.Escape(kv.Key) +
                "[\"']?\\s*/?\\s*>(?:\\s*<\\s*/\\s*x\\s*>)?";
            if (Regex.IsMatch(outText, pattern))
                outText = Regex.Replace(outText, pattern, kv.Value.Replace("$", "$$"));
            else
            {
                result.RestoreWarnings++;
                warned = true;
            }
        }

        // 対応する id が無いタグが残ることがある。そのまま出すと画面に
        // <x id="0"/> がそのまま見えるので取り除いたうえで警告に数える。
        if (LeftoverTagRegex.IsMatch(outText))
        {
            outText = LeftoverTagRegex.Replace(outText, "");
            result.RestoreWarnings++;
            warned = true;
        }

        // 本文側のエスケープを元に戻す(タグ復元後に行う)。
        outText = XmlUnescape(outText);

        if (warned) result.NoteRestoreWarningSource(source);
        return outText;
    }

    // 本文用の最小XMLエスケープ。順序重要(& を最初に)。
    private static string XmlEscape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // 実体参照を1回の走査で戻す。順に Replace すると &amp;lt; が < に化けるため、
    // また &quot; &#39; など自前で書いていない実体も DeepL が返すため、まとめて扱う。
    private static string XmlUnescape(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('&') < 0) return s;
        return XmlEntityRegex.Replace(s, m =>
        {
            if (m.Groups[1].Success &&
                int.TryParse(m.Groups[1].Value, out var dec) && IsSafeCodePoint(dec))
                return char.ConvertFromUtf32(dec);
            if (m.Groups[2].Success &&
                int.TryParse(m.Groups[2].Value, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var hex) && IsSafeCodePoint(hex))
                return char.ConvertFromUtf32(hex);
            return m.Value switch
            {
                "&amp;" => "&",
                "&lt;" => "<",
                "&gt;" => ">",
                "&quot;" => "\"",
                "&apos;" => "'",
                _ => m.Value
            };
        });
    }

    // サロゲート単独や範囲外は ConvertFromUtf32 が例外を投げるので弾く。
    private static bool IsSafeCodePoint(int cp)
        => cp > 0 && cp <= 0x10FFFF && (cp < 0xD800 || cp > 0xDFFF);
}
