using ModSorter.Clients;
using ModSorter.Models;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModSorter.Services;

// ja_jp を持たない MOD の en_us を翻訳し、1つの日本語リソースパック(zip)を生成する。
// jar 内の実ディレクトリ assets/<ns>/lang/ を走査するため、宣言 modid に依存しない。
public static class LangPackService
{
    // 進捗通知用コールバック(現在値, 総数, メッセージ)。UI 側で受ける。
    public delegate void ProgressHandler(int done, int total, string message);

    // プレースホルダ退避用トークン。DeepL が改変しにくい英数字のみ。
    private static readonly Regex PlaceholderRegex = new(
        @"%(\d+\$)?[sd]" +      // %s %d %1$s
        @"|\{\d+\}" +           // {0} {1}
        @"|§.",                 // §a 等の書式コード
        RegexOptions.Compiled);

    // 抽出した1名前空間分のデータ
    public sealed class NamespaceLang
    {
        public string Namespace = "";
        // キー -> 原文(en_us)。ja_jp が既にあるものは対象に含めない。
        public Dictionary<string, string> Entries = new();
        // 抽出元(複数 jar にまたがる場合の記録用)
        public List<string> SourceJars = new();
    }

    // 生成結果サマリ
    public sealed class LangPackResult
    {
        public int ModCount;              // 走査した jar 数
        public int NamespaceCount;        // 翻訳対象になった名前空間数
        public int EntryCount;            // 翻訳対象の総エントリ数
        public int TranslatedChars;       // 実際に翻訳送信した文字数(キャッシュ未ヒット分)
        public int SkippedJaExisting;     // ja_jp 既存で除外した名前空間数
        public int SkippedBroken;         // 解析失敗でスキップした jar 数
        public int RestoreWarnings;       // プレースホルダ復元漏れ件数
        // 復元漏れした原文の一覧(どの文でトークンが戻せなかったか)。
        // 原文が分かれば後から名前空間/キーを検索でき、再翻訳の対象も絞れる。
        public List<string> RestoreWarningSources = new();
        public List<string> ExcludedNamespaces = new(); // 除外した名前空間一覧
        public string OutputPath = "";    // 出力した zip のパス
        public bool Canceled;
    }

    // ===== 1) 抽出 + 除外 =====
    // jar 群から、ja_jp を持たない名前空間の en_us エントリを集める。
    public static List<NamespaceLang> ExtractTargets(
        IEnumerable<string> jarPaths,
        bool skipIfJaExists,
        LangPackResult result)
    {
        // 名前空間 -> 統合データ
        var map = new Dictionary<string, NamespaceLang>();
        // ja_jp を持つ名前空間(除外判定用)
        var hasJa = new HashSet<string>();

        foreach (var jar in jarPaths)
        {
            result.ModCount++;
            try
            {
                using var zip = ZipFile.OpenRead(jar);
                foreach (var e in zip.Entries)
                {
                    // assets/<ns>/lang/<file> の形だけを対象にする
                    var m = Regex.Match(e.FullName,
                        @"^assets/([^/]+)/lang/(en_us|ja_jp)\.(json|lang)$",
                        RegexOptions.IgnoreCase);
                    if (!m.Success) continue;

                    var ns = m.Groups[1].Value;
                    var kind = m.Groups[2].Value.ToLowerInvariant();  // en_us / ja_jp
                    var ext = m.Groups[3].Value.ToLowerInvariant();   // json / lang

                    if (kind == "ja_jp")
                    {
                        hasJa.Add(ns);
                        continue;
                    }

                    // en_us を読み込む
                    Dictionary<string, string> parsed;
                    try
                    {
                        var text = ReadEntry(e);
                        parsed = ext == "json" ? ParseJson(text) : ParseLang(text);
                    }
                    catch
                    {
                        result.SkippedBroken++;
                        continue;
                    }

                    if (!map.TryGetValue(ns, out var nl))
                    {
                        nl = new NamespaceLang { Namespace = ns };
                        map[ns] = nl;
                    }
                    if (!nl.SourceJars.Contains(jar)) nl.SourceJars.Add(jar);
                    // 後勝ちマージ(仕様書9章)
                    foreach (var kv in parsed) nl.Entries[kv.Key] = kv.Value;
                }
            }
            catch
            {
                result.SkippedBroken++;
            }
        }

        // ja_jp を持つ名前空間を除外(トグル ON のとき)
        var targets = new List<NamespaceLang>();
        foreach (var kv in map)
        {
            if (skipIfJaExists && hasJa.Contains(kv.Key))
            {
                result.SkippedJaExisting++;
                result.ExcludedNamespaces.Add(kv.Key);
                continue;
            }
            targets.Add(kv.Value);
        }

        result.NamespaceCount = targets.Count;
        result.EntryCount = targets.Sum(t => t.Entries.Count);
        return targets;
    }

    // ===== 2) 文字数見積もり(除外後・重複排除後のユニーク原文) =====
    public static int EstimateChars(IEnumerable<NamespaceLang> targets)
    {
        var unique = new HashSet<string>();
        foreach (var t in targets)
            foreach (var v in t.Entries.Values)
                if (!string.IsNullOrEmpty(v)) unique.Add(v);
        return unique.Sum(s => s.Length);
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

        // ユニーク原文を集める
        var unique = new List<string>();
        var seen = new HashSet<string>();
        foreach (var t in targets)
            foreach (var v in t.Entries.Values)
                if (!string.IsNullOrEmpty(v) && seen.Add(v)) unique.Add(v);

        var dict = new Dictionary<string, string>();
        // キャッシュ未ヒットだけを翻訳対象にする
        var toTranslate = new List<string>();
        foreach (var src in unique)
        {
            var cached = TranslationCache.Get(src);
            if (cached != null) dict[src] = cached;
            else toTranslate.Add(src);
        }

        int total = toTranslate.Count;
        int done = 0;
        const int batchSize = 50; // DeepL の1リクエスト上限

        for (int i = 0; i < toTranslate.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();

            var slice = toTranslate.GetRange(i, Math.Min(batchSize, toTranslate.Count - i));

            // プレースホルダ/色コードをXMLタグに退避(案1-B)
            var protectedTexts = new List<string>(slice.Count);
            var tokenMaps = new List<Dictionary<string, string>>(slice.Count);
            foreach (var src in slice)
            {
                var (masked, tokenMap) = ProtectXml(src);
                protectedTexts.Add(masked);
                tokenMaps.Add(tokenMap);
            }

            var translated = await DeepLClient.TranslateBatchXmlAsync(protectedTexts);

            for (int j = 0; j < slice.Count; j++)
            {
                string outText;
                if (translated == null || j >= translated.Count)
                {
                    // 失敗時は原文フォールバック(仕様書10章)
                    outText = slice[j];
                }
                else
                {
                    // XMLタグ方式で復元(原文 slice[j] を渡し、復元漏れ時に原文を記録)
                    outText = RestoreXml(translated[j], tokenMaps[j], result, slice[j]);
                    result.TranslatedChars += slice[j].Length;
                }
                dict[slice[j]] = outText;
                TranslationCache.Put(slice[j], outText);
            }

            done += slice.Count;
            progress?.Invoke(done, total, $"翻訳中... {done}/{total}");
        }

        TranslationCache.Save();
        return dict;
    }


    // ===== 再検査) キャッシュ再検査(DeepL枠を使わない) =====
    // 既にキャッシュ済みの訳文を対象に、プレースホルダが正しく復元できるかを
    // ローカルだけで再検査し、復元漏れした原文の一覧を返す。翻訳送信は一切しない。
    // 前回の生成で壊れた訳(トークンが消えた訳)を、翻訳し直さずに洗い出すために使う。
    public static List<string> RecheckCache(
        IEnumerable<NamespaceLang> targets,
        string engine)
    {
        TranslationCache.Load(engine);

        // 対象のユニーク原文を集める
        var unique = new List<string>();
        var seen = new HashSet<string>();
        foreach (var t in targets)
            foreach (var v in t.Entries.Values)
                if (!string.IsNullOrEmpty(v) && seen.Add(v)) unique.Add(v);

        var brokenSources = new List<string>();

        foreach (var src in unique)
        {
            var cachedTranslation = TranslationCache.Get(src);
            if (cachedTranslation == null) continue; // 未翻訳はここでは対象外

            // 原文に含まれるプレースホルダを実体(%s, {0}, §a 等)のまま数える。
            var srcPh = PlaceholderRegex.Matches(src);
            if (srcPh.Count == 0) continue; // プレースホルダが無ければ漏れは起きない

            // 原文のプレースホルダ実体ごとの出現数を数える
            var srcCount = new Dictionary<string, int>();
            foreach (System.Text.RegularExpressions.Match m in srcPh)
                srcCount[m.Value] = srcCount.TryGetValue(m.Value, out var c) ? c + 1 : 1;

            // 訳文にも同じ実体が同数含まれているかを調べる。
            // 1つでも不足していれば復元漏れ(プレースホルダが欠けている)とみなす。
            bool broken = false;
            foreach (var kv in srcCount)
            {
                int inTranslated = CountOccurrences(cachedTranslation, kv.Key);
                if (inTranslated < kv.Value) { broken = true; break; }
            }
            if (broken) brokenSources.Add(src);
        }

        return brokenSources;
    }

    // ===== 修復) printf系の復元漏れを原形維持でキャッシュ修復(DeepL枠を使わない) =====
    // 復元漏れのうち、printf系プレースホルダ(%s %d %1$s 等)を含む原文について、
    // キャッシュの壊れた訳を「原文そのまま(原形維持)」で上書きする。
    // これによりプレースホルダが確実に揃い、表示崩れを防ぐ。翻訳送信はしない。
    // 色コード(§x)のみの復元漏れは対象外(案1で別途対応)。
    // 戻り値は修復した原文の一覧。
    public static List<string> RepairPrintfPlaceholders(
        IEnumerable<string> brokenSources,
        string engine)
    {
        TranslationCache.Load(engine);

        // printf系プレースホルダ(%s %d %1$s 等)。色コード §x は含めない。
        var printfRegex = new Regex(@"%(\d+\$)?[sd]", RegexOptions.Compiled);

        var repaired = new List<string>();
        foreach (var src in brokenSources)
        {
            if (string.IsNullOrEmpty(src)) continue;
            // printf系を含む原文だけを修復対象にする
            if (!printfRegex.IsMatch(src)) continue;

            // 原形維持: 原文をそのまま訳文としてキャッシュに上書き。
            // プレースホルダが原文どおり確実に含まれる。
            TranslationCache.Put(src, src);
            repaired.Add(src);
        }

        TranslationCache.Save();
        return repaired;
    }

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
        if (File.Exists(outputZipPath)) File.Delete(outputZipPath);

        using var fs = new FileStream(outputZipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        // pack.mcmeta
        var mcmeta = new
        {
            pack = new
            {
                pack_format = packFormat,
                description = "ModSorter 自動生成 日本語化パック"
            }
        };
        WriteZipText(zip, "pack.mcmeta",
            JsonSerializer.Serialize(mcmeta,
                new JsonSerializerOptions { WriteIndented = true }));

        // 各名前空間の ja_jp.json
        foreach (var t in targets)
        {
            // 入力順を保つため、原文辞書の列挙順で組み立てる
            var outMap = new Dictionary<string, string>();
            foreach (var kv in t.Entries)
            {
                var src = kv.Value;
                outMap[kv.Key] =
                    (!string.IsNullOrEmpty(src) && translations.TryGetValue(src, out var tr))
                        ? tr : src;
            }

            var json = SerializeLangJson(outMap);
            WriteZipText(zip, $"assets/{t.Namespace}/lang/ja_jp.json", json);
        }

        result.OutputPath = outputZipPath;
    }

    // ===== 内部ヘルパ =====

    private static string ReadEntry(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    // en_us.json: 文字列値のみ採用。配列/数値/ネストは対象外。
    private static Dictionary<string, string> ParseJson(string text)
    {
        var dict = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(text);
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

    // プレースホルダを X0X, X1X... に退避。戻り値は(退避後テキスト, トークン->原片)。
    private static (string, Dictionary<string, string>) Protect(string src)
    {
        var map = new Dictionary<string, string>();
        int idx = 0;
        var masked = PlaceholderRegex.Replace(src, m =>
        {
            var token = $"X{idx}X";
            map[token] = m.Value;
            idx++;
            return token;
        });
        return (masked, map);
    }

    // 退避トークンを元へ戻す。復元漏れ(トークン残存や欠落)を検出してカウント。
    // source は退避前の原文。復元漏れが起きた原文を記録し、後から特定できるようにする。
    private static string Restore(string translated, Dictionary<string, string> map,
        LangPackResult result, string source)
    {
        var outText = translated;
        bool warned = false;
        foreach (var kv in map)
        {
            if (outText.Contains(kv.Key))
                outText = outText.Replace(kv.Key, kv.Value);
            else
            {
                result.RestoreWarnings++; // トークンが消えた=復元漏れ
                warned = true;
            }
        }
        // 同一原文で複数トークンが漏れても、原文の記録は1回だけにする。
        if (warned && !result.RestoreWarningSources.Contains(source))
            result.RestoreWarningSources.Add(source);
        return outText;
    }

    // ===== XMLタグ方式(案1-B) =====

    // プレースホルダ/色コードを <x id="n"/> タグに退避し、本文の < > & はエスケープする。
    // 戻り値は(退避後テキスト, id -> 元の断片)。
    private static (string, Dictionary<string, string>) ProtectXml(string src)
    {
        var map = new Dictionary<string, string>();
        var sb = new StringBuilder();
        int idx = 0;
        int pos = 0;

        // プレースホルダ/色コードの位置を順に処理し、その間の本文はエスケープする。
        foreach (System.Text.RegularExpressions.Match m in PlaceholderRegex.Matches(src))
        {
            // 直前の本文(エスケープ対象)
            if (m.Index > pos)
                sb.Append(XmlEscape(src.Substring(pos, m.Index - pos)));

            // プレースホルダ/色コードをタグ化
            var id = idx.ToString();
            map[id] = m.Value;
            sb.Append($"<x id=\"{id}\"/>");
            idx++;
            pos = m.Index + m.Length;
        }
        // 末尾の残り本文
        if (pos < src.Length)
            sb.Append(XmlEscape(src.Substring(pos)));

        return (sb.ToString(), map);
    }

    // XMLタグ方式の訳文を元へ戻す。<x id="n"/> を元断片に、エスケープを元文字に戻す。
    // 復元漏れ(タグ欠落)を検出して記録する。
    private static string RestoreXml(string translated, Dictionary<string, string> map,
        LangPackResult result, string source)
    {
        var outText = translated;
        bool warned = false;

        foreach (var kv in map)
        {
            // DeepLが属性の空白や引用符を変える場合に備え、緩めに一致させる。
            var pattern = $"<x\\s+id=\"{System.Text.RegularExpressions.Regex.Escape(kv.Key)}\"\\s*/>";
            var re = new System.Text.RegularExpressions.Regex(pattern);
            if (re.IsMatch(outText))
                outText = re.Replace(outText, kv.Value.Replace("$", "$$")); // $ を保護
            else
            {
                result.RestoreWarnings++;
                warned = true;
            }
        }

        // 本文側のXMLエスケープを元に戻す(タグ復元後に行う)。
        outText = XmlUnescape(outText);

        if (warned && !result.RestoreWarningSources.Contains(source))
            result.RestoreWarningSources.Add(source);
        return outText;
    }

    // 本文用の最小XMLエスケープ。順序重要(& を最初に)。
    private static string XmlEscape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // XMLエスケープの復元。順序重要(&amp; を最後に)。
    private static string XmlUnescape(string s)
        => s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

    // ===== 再翻訳) 復元漏れ原文をキャッシュから消して新方式で翻訳し直す =====
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

        // 対象をキャッシュから削除(これで未キャッシュ扱いになる)
        int removed = 0;
        foreach (var src in sourcesToRetranslate)
            if (TranslationCache.Remove(src)) removed++;
        TranslationCache.Save();

        // 対象原文だけを含む一時的な NamespaceLang を作り、TranslateAsync に渡す。
        // (TranslateAsync はユニーク原文単位でキャッシュ未ヒット分のみ翻訳する)
        var tmp = new NamespaceLang { Namespace = "__retranslate__" };
        int k = 0;
        foreach (var src in sourcesToRetranslate)
            tmp.Entries[$"__k{k++}"] = src;

        await TranslateAsync(new[] { tmp }, engine, result, progress, ct);
        return removed;
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
