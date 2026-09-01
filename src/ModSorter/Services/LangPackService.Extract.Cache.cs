using System.IO;
using System.Text;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // 抽出は全 jar を展開するので重い。生成・再検査・printf修復・再翻訳・英語固定解除の
    // 各ボタンがそれぞれ ExtractTargets を呼んでおり、連続操作で同じ展開が4回走っていた。
    // jar の構成(パス・サイズ・更新時刻)と判定モードが同じ間は前回の結果を使い回す。
    private static readonly object ExtractLock = new();
    private static string _extractKey = "";
    private static List<NamespaceLang>? _extractTargets;
    private static LangPackResult? _extractCounters;

    private static string BuildExtractKey(IEnumerable<string> paths, bool fillMissingOnly)
    {
        var sb = new StringBuilder();
        sb.Append(fillMissingOnly ? '1' : '0');
        foreach (var p in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append('|').Append(p);
            try
            {
                var fi = new FileInfo(p);
                if (fi.Exists)
                    sb.Append(':').Append(fi.Length)
                      .Append(':').Append(fi.LastWriteTimeUtc.Ticks);
                else sb.Append(":none");
            }
            catch { sb.Append(":err"); }
        }
        return sb.ToString();
    }

    private static bool TryGetCachedExtract(
        List<string> paths, bool fillMissingOnly,
        LangPackResult result, out List<NamespaceLang> targets)
    {
        var key = BuildExtractKey(paths, fillMissingOnly);
        lock (ExtractLock)
        {
            if (_extractTargets != null && _extractCounters != null && _extractKey == key)
            {
                result.CopyExtractCountersFrom(_extractCounters);
                targets = _extractTargets;
                return true;
            }
        }
        targets = new List<NamespaceLang>();
        return false;
    }

    private static void StoreExtract(
        List<string> paths, bool fillMissingOnly,
        LangPackResult counters, List<NamespaceLang> targets)
    {
        var key = BuildExtractKey(paths, fillMissingOnly);
        lock (ExtractLock)
        {
            _extractKey = key;
            _extractTargets = targets;
            _extractCounters = counters;
        }
    }

    // jar の中身を差し替えたのに更新時刻が変わらない場合など、明示的に捨てたいとき用。
    public static void InvalidateExtractCache()
    {
        lock (ExtractLock)
        {
            _extractKey = "";
            _extractTargets = null;
            _extractCounters = null;
        }
    }
}
