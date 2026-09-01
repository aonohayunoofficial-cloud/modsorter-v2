using ModSorter.Clients;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // 翻訳対象のユニーク原文を入力順で集める。見積もり・翻訳・再検査で共用する。
    private static List<string> CollectUniqueSources(IEnumerable<NamespaceLang> targets)
    {
        var unique = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in targets)
            foreach (var kv in t.TranslationTargets())
                if (!string.IsNullOrEmpty(kv.Value) && seen.Add(kv.Value)) unique.Add(kv.Value);
        return unique;
    }

    // ===== 2) 文字数見積もり =====
    // 不足キーのユニーク原文の総文字数。キャッシュ済みも含む理論値。
    public static int EstimateChars(IEnumerable<NamespaceLang> targets)
        => CollectUniqueSources(targets).Sum(s => s.Length);

    // 実際に DeepL へ送る文字数。キャッシュ済みは送らないので除く。
    // 枠の残量と突き合わせるのはこちらでないと、2回目以降の生成で
    // 送らない分まで数えて「残量超過」の警告が出てしまう。
    public static int EstimateChars(IEnumerable<NamespaceLang> targets, string engine)
    {
        TranslationCache.Load(engine);
        int sum = 0;
        foreach (var src in CollectUniqueSources(targets))
            if (TranslationCache.Get(src) == null) sum += src.Length;
        return sum;
    }

    // ===== 3) 翻訳(キャッシュ + バッチ) =====
    // ユニーク原文をまとめて翻訳し、原文->訳文の辞書を返す。
    public static async Task<Dictionary<string, string>> TranslateAsync(
        IEnumerable<NamespaceLang> targets,
        string engine,
        LangPackResult result,
        ProgressHandler? progress,
        CancellationToken ct)
    {
        TranslationCache.Load(engine);

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var toTranslate = new List<string>();
        foreach (var src in CollectUniqueSources(targets))
        {
            var cached = TranslationCache.Get(src);
            if (cached != null) dict[src] = cached;
            else toTranslate.Add(src);
        }

        int total = toTranslate.Count;
        int done = 0;
        const int batchSize = 50; // DeepL の1リクエスト上限
        int consecutiveFailures = 0;
        int sinceSave = 0;

        try
        {
            for (int i = 0; i < toTranslate.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();

                var slice = toTranslate.GetRange(i, Math.Min(batchSize, toTranslate.Count - i));

                var protectedTexts = new List<string>(slice.Count);
                var tokenMaps = new List<Dictionary<string, string>>(slice.Count);
                foreach (var src in slice)
                {
                    var (masked, tokenMap) = ProtectXml(src);
                    protectedTexts.Add(masked);
                    tokenMaps.Add(tokenMap);
                }

                // 429(レート制限)や一時的な 5xx は待って再送すれば通る。
                // 456(枠切れ)は待っても回復しないので即座に打ち切る。
                List<string>? translated = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    translated = await DeepLClient.TranslateBatchXmlAsync(protectedTexts);
                    if (translated != null && translated.Count >= slice.Count) break;

                    translated = null;
                    result.LastApiError = DeepLClient.LastError;
                    if (DeepLClient.LastError.Contains("456")) break;
                    if (attempt < 2) await Task.Delay(1000 * (attempt + 1), ct);
                }

                if (translated == null)
                {
                    // 今回の出力だけ原文で埋める。キャッシュへは書かない。
                    // 原文をキャッシュへ書くと訳文＝原文で固定され、次回以降ヒットして
                    // 永久に英語のままになるため(この経路が英語固定の原因だった)。
                    result.FailedBatches++;
                    result.FailedEntries += slice.Count;
                    foreach (var src in slice) dict[src] = src;

                    consecutiveFailures++;
                    if (consecutiveFailures >= 3)
                    {
                        result.AbortedByApiError = true;
                        int restStart = i + slice.Count;
                        result.FailedEntries += toTranslate.Count - restStart;
                        foreach (var src in toTranslate.GetRange(
                            restStart, toTranslate.Count - restStart))
                            dict[src] = src;
                        break;
                    }
                }
                else
                {
                    consecutiveFailures = 0;
                    for (int j = 0; j < slice.Count; j++)
                    {
                        var outText = RestoreXml(translated[j], tokenMaps[j], result, slice[j]);
                        result.TranslatedChars += slice[j].Length;
                        dict[slice[j]] = outText;
                        TranslationCache.Put(slice[j], outText);
                    }
                    // 長時間の生成中に落ちても翻訳済み分を失わないよう、途中でも書き出す。
                    if (++sinceSave >= 20) { TranslationCache.Save(); sinceSave = 0; }
                }

                done += slice.Count;
                progress?.Invoke(done, total, $"翻訳中... {done}/{total}");
            }
        }
        finally
        {
            // 中断・例外でもここまでの分は必ず永続化する。
            // 以前は中断時に Save を通らず、UI の「翻訳済み分は保存済み」が実態と食い違っていた。
            TranslationCache.Save();
        }

        return dict;
    }
}
