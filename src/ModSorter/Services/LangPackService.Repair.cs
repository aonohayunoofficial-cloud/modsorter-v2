using System.Text.RegularExpressions;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // printf系プレースホルダ。色コード §x は含めない。
    private static readonly Regex PrintfRegex = new(
        @"%(\d+\$)?\d*(\.\d+)?[sdf]", RegexOptions.Compiled);

    // ===== 再検査) キャッシュ再検査(DeepL枠を使わない) =====
    // 既にキャッシュ済みの訳文を対象に、プレースホルダが正しく復元できているかを
    // ローカルだけで再検査し、復元漏れした原文の一覧を返す。翻訳送信は一切しない。
    public static List<string> RecheckCache(IEnumerable<NamespaceLang> targets, string engine)
    {
        TranslationCache.Load(engine);
        var brokenSources = new List<string>();

        foreach (var src in CollectUniqueSources(targets))
        {
            var cachedTranslation = TranslationCache.Get(src);
            if (cachedTranslation == null) continue; // 未翻訳はここでは対象外

            // 原文のプレースホルダを実体(%s, {0}, §a 等)のまま数える。
            // 用語辞書の退避は訳語へ変わるため、この照合には掛けない。
            var srcPh = PlaceholderRegex.Matches(src);
            if (srcPh.Count == 0) continue;

            var srcCount = new Dictionary<string, int>();
            foreach (Match m in srcPh)
                srcCount[m.Value] = srcCount.TryGetValue(m.Value, out var c) ? c + 1 : 1;

            bool broken = false;
            foreach (var kv in srcCount)
            {
                if (CountOccurrences(cachedTranslation, kv.Key) < kv.Value) { broken = true; break; }
            }
            if (broken) brokenSources.Add(src);
        }

        return brokenSources;
    }

    // ===== 修復) printf系の復元漏れを原形維持でキャッシュ修復(枠を使わない) =====
    // 壊れた訳を「原文そのまま」で上書きし、プレースホルダを確実に揃える。
    public static List<string> RepairPrintfPlaceholders(
        IEnumerable<string> brokenSources, string engine)
    {
        TranslationCache.Load(engine);

        var repaired = new List<string>();
        foreach (var src in brokenSources)
        {
            if (string.IsNullOrEmpty(src)) continue;
            if (!PrintfRegex.IsMatch(src)) continue;

            TranslationCache.Put(src, src);
            repaired.Add(src);
        }

        TranslationCache.Save();
        return repaired;
    }

    // ===== 復旧) 英語のまま固定されたキャッシュを削除する(枠を使わない) =====
    // 送信失敗時に原文をそのままキャッシュへ書いていた時期のデータを掃除する。
    // printf系は RepairPrintfPlaceholders が意図的に原文へ揃えた結果なので対象外。
    public static int PurgeUntranslatedCache(
        IEnumerable<NamespaceLang> targets, string engine)
    {
        TranslationCache.Load(engine);

        int removed = 0;
        foreach (var src in CollectUniqueSources(targets))
        {
            if (PrintfRegex.IsMatch(src)) continue;

            var cached = TranslationCache.Get(src);
            if (cached == null) continue;
            if (!string.Equals(cached, src, StringComparison.Ordinal)) continue;

            if (TranslationCache.Remove(src)) removed++;
        }
        if (removed > 0) TranslationCache.Save();
        return removed;
    }

    // ===== 復旧) 用語辞書と食い違う訳をキャッシュから外す(枠を使わない) =====
    // 用語辞書は翻訳の直前に適用されるため、辞書を直しても既にキャッシュ済みの訳は
    // 古い訳語のまま残り、何度生成しても直らない。
    // 原文が用語に一致するのに訳文へその訳語が入っていないものだけを消し、
    // 次回の生成で新しい訳語が当たるようにする。全消しと違い枠の消費を最小にできる。
    public static int PurgeGlossaryMismatchCache(
        IEnumerable<NamespaceLang> targets, string engine)
    {
        TranslationCache.Load(engine);
        GlossaryService.Load(force: true);

        int removed = 0;
        foreach (var src in CollectUniqueSources(targets))
        {
            var hits = GlossaryService.FindMatches(src);
            if (hits.Count == 0) continue;

            var cached = TranslationCache.Get(src);
            if (cached == null) continue;

            bool ok = hits.All(h => cached.Contains(h.Target, StringComparison.Ordinal));
            if (ok) continue;

            if (TranslationCache.Remove(src)) removed++;
        }
        if (removed > 0) TranslationCache.Save();
        return removed;
    }
}
