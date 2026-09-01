namespace ModSorter.Services;

public static partial class LangPackService
{
    // ===== 再翻訳) 指定した原文だけキャッシュから消して翻訳し直す =====
    // 対象原文をキャッシュから削除してから TranslateAsync を呼ぶことで、
    // その原文だけが未キャッシュ扱いになり、新方式(XMLタグ)で翻訳し直される。
    // 正常な既存キャッシュはヒットするため再翻訳されない(枠消費は対象分のみ)。
    // 戻り値は再翻訳した原文数。
    public static async Task<int> RetranslateAsync(
        IReadOnlyList<string> sourcesToRetranslate,
        IEnumerable<NamespaceLang> targets,
        string engine,
        LangPackResult result,
        ProgressHandler? progress,
        CancellationToken ct)
    {
        if (sourcesToRetranslate == null || sourcesToRetranslate.Count == 0) return 0;

        TranslationCache.Load(engine);

        int removed = 0;
        foreach (var src in sourcesToRetranslate)
            if (TranslationCache.Remove(src)) removed++;
        TranslationCache.Save();

        // 対象原文だけを含む一時的な NamespaceLang を作り、TranslateAsync に渡す。
        // (TranslateAsync はユニーク原文単位でキャッシュ未ヒット分のみ翻訳する)
        var tmp = new NamespaceLang { Namespace = "__retranslate__" };
        int k = 0;
        foreach (var src in sourcesToRetranslate)
        {
            var key = $"__k{k++}";
            tmp.Entries[key] = src;
            tmp.TranslateKeys.Add(key); // TranslateAsync は翻訳対象キーだけを見るため必須
        }

        await TranslateAsync(new[] { tmp }, engine, result, progress, ct);
        return removed;
    }
}
