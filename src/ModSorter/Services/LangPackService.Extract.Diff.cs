using System.Text.RegularExpressions;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // 名前空間ごとに「翻訳が必要なキー」を確定し、対象一覧を組み立てる。
    private static List<NamespaceLang> BuildTargets(
        Dictionary<string, NamespaceLang> map,
        Dictionary<string, Dictionary<string, string>> jaMap,
        bool fillMissingOnly,
        LangPackResult result)
    {
        var targets = new List<NamespaceLang>();

        foreach (var kv in map)
        {
            var nl = kv.Value;
            bool hasJa = jaMap.TryGetValue(nl.Namespace, out var ja)
                         && ja != null && ja.Count > 0;

            if (fillMissingOnly && hasJa)
            {
                foreach (var je in ja!) nl.Existing[je.Key] = je.Value;

                foreach (var en in nl.Entries)
                {
                    if (string.IsNullOrEmpty(en.Value)) continue; // 空文字は訳す必要がない
                    if (!nl.Existing.TryGetValue(en.Key, out var jaVal))
                    {
                        nl.TranslateKeys.Add(en.Key); // 同梱 ja_jp に無いキー＝未訳
                        continue;
                    }
                    // 値はあるが空白だけ、または英語のまま残っているキーも埋め直す。
                    if (string.IsNullOrWhiteSpace(jaVal) || LooksUntranslated(en.Value, jaVal))
                        nl.TranslateKeys.Add(en.Key);
                }
            }
            else
            {
                foreach (var en in nl.Entries)
                    if (!string.IsNullOrEmpty(en.Value)) nl.TranslateKeys.Add(en.Key);
            }

            if (nl.TranslateKeys.Count == 0)
            {
                // 不足なし＝同梱の日本語で完備。パックに載せる必要もない。
                result.SkippedJaExisting++;
                result.ExcludedNamespaces.Add(nl.Namespace);
                continue;
            }
            if (nl.Existing.Count > 0) result.PartialNamespaces++;
            targets.Add(nl);
        }

        result.NamespaceCount = targets.Count;
        result.EntryCount = targets.Sum(t => t.TranslateKeys.Count);
        result.PreservedEntries = targets.Sum(
            t => t.Existing.Count(e => !t.TranslateKeys.Contains(e.Key)));
        return targets;
    }

    // 同梱 ja_jp に値はあるが実質未訳(英語のまま)かを判定する。
    // 条件は「原文と完全一致」かつ「仮名・漢字を含まない」かつ「英字が2文字以上続く」。
    // "TNT" "OK" のように日英で同じ表記になる正当な訳を誤って翻訳対象にしないため、
    // 3条件をすべて満たす場合だけ未訳とみなす。
    private static bool LooksUntranslated(string src, string ja)
    {
        if (!string.Equals(src, ja, StringComparison.Ordinal)) return false;
        foreach (var c in ja)
        {
            // ひらがな・カタカナ(0x3040-0x30FF)・CJK統合漢字(0x4E00-0x9FFF)を含めば訳済み。
            if ((c >= 0x3040 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FFF)) return false;
        }
        return Regex.IsMatch(ja, "[A-Za-z]{2}");
    }
}
