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
        // キー -> 原文(en_us)。名前空間内の en_us 全キーを入力順で保持する。
        // 出力する ja_jp.json のキー順もこの順に合わせる。
        public Dictionary<string, string> Entries = new();
        // MOD 同梱の ja_jp に既に入っている訳。キー -> 訳文。
        // リソースパックは同名の lang ファイルを丸ごと置き換えるため、
        // ここを出力にそのまま載せ直さないと同梱の日本語が英語に戻る。
        public Dictionary<string, string> Existing = new();
        // Entries のうち翻訳が必要なキー。
        // 同梱 ja_jp に無いキーと、値が英語のまま残っているキーだけが入る。
        public HashSet<string> TranslateKeys = new();
        // 抽出元(複数 jar にまたがる場合の記録用)
        public List<string> SourceJars = new();

        // 翻訳が必要なキーと原文だけを列挙する。見積もり・翻訳・再検査はこれを使う。
        public IEnumerable<KeyValuePair<string, string>> TranslationTargets()
        {
            foreach (var kv in Entries)
                if (TranslateKeys.Contains(kv.Key)) yield return kv;
        }
    }

    // 生成結果サマリ
    public sealed class LangPackResult
    {
        public int ModCount;              // 走査した jar 数
        public int NamespaceCount;        // 翻訳対象になった名前空間数
        public int EntryCount;            // 翻訳対象の総エントリ数(不足キーのみ)
        public int TranslatedChars;       // 実際に翻訳送信した文字数(キャッシュ未ヒット分)
        public int PreservedEntries;      // MOD 同梱 ja_jp から引き継いだエントリ数
        public int PartialNamespaces;     // 同梱 ja_jp があり不足分だけ補った名前空間数
        public int SkippedJaExisting;     // 同梱 ja_jp が完備で除外した名前空間数
        public int SkippedBroken;         // 解析失敗でスキップした jar 数
        public int RestoreWarnings;       // プレースホルダ復元漏れ件数
        // 復元漏れした原文の一覧(どの文でトークンが戻せなかったか)。
        // 原文が分かれば後から名前空間/キーを検索でき、再翻訳の対象も絞れる。
        public List<string> RestoreWarningSources = new();
        public List<string> ExcludedNamespaces = new(); // 除外した名前空間一覧
        public string OutputPath = "";    // 出力した zip のパス
        public bool Canceled;

        // DeepL 送信に失敗したバッチ数と、その巻き添えになったエントリ数。
        // 失敗分はキャッシュに書かないため、次回生成で自動的に再送信される。
        public int FailedBatches;
        public int FailedEntries;
        // 最後に発生した API エラー(HTTP コード等)。原因表示用。
        public string LastApiError = "";
        // 連続失敗で翻訳を打ち切ったか(枠切れ/キー不正等の恒久エラー想定)。
        public bool AbortedByApiError;
    }

    // ===== 1) 抽出 + 差分判定 =====
    // jar 群から en_us と ja_jp を両方読み、名前空間ごとに「翻訳が必要なキー」を確定する。
    // fillMissingOnly=true: 同梱 ja_jp を尊重し、そこに無いキーと英語のまま残っているキー
    //   だけを翻訳対象にする。同梱の訳は Existing に持ち、出力にもそのまま載せる。
    //   これで「同梱の古い ja_jp が半端に当たって残りが英語」の状態を埋められる。
    // fillMissingOnly=false: 同梱 ja_jp を無視し、en_us の全キーを翻訳対象にする。
    public static List<NamespaceLang> ExtractTargets(
        IEnumerable<string> jarPaths,
        bool fillMissingOnly,
        LangPackResult result)
    {
        // 名前空間 -> 統合データ(en_us 側)
        var map = new Dictionary<string, NamespaceLang>();
        // 名前空間 -> 同梱 ja_jp の内容(複数 jar にまたがる場合は後勝ちでマージ)
        var jaMap = new Dictionary<string, Dictionary<string, string>>();

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

                    if (kind == "ja_jp")
                    {
                        if (!jaMap.TryGetValue(ns, out var jl))
                        {
                            jl = new Dictionary<string, string>();
                            jaMap[ns] = jl;
                        }
                        foreach (var kv in parsed) jl[kv.Key] = kv.Value;
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

        var targets = new List<NamespaceLang>();
        foreach (var kv in map)
        {
            var nl = kv.Value;
            bool hasJa = jaMap.TryGetValue(nl.Namespace, out var ja) && ja != null && ja.Count > 0;

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


    // ===== 2) 文字数見積もり(不足キーのみ・重複排除後のユニーク原文) =====
    // 同梱 ja_jp から引き継ぐ分は送信しないので、見積もりからも除く。
    public static int EstimateChars(IEnumerable<NamespaceLang> targets)
    {
        var unique = new HashSet<string>();
        foreach (var t in targets)
            foreach (var kv in t.TranslationTargets())
                if (!string.IsNullOrEmpty(kv.Value)) unique.Add(kv.Value);
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

        // ユニーク原文を集める(翻訳が必要なキーの原文だけ)
        var unique = new List<string>();
        var seen = new HashSet<string>();
        foreach (var t in targets)
            foreach (var kv in t.TranslationTargets())
                if (!string.IsNullOrEmpty(kv.Value) && seen.Add(kv.Value)) unique.Add(kv.Value);

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
        int consecutiveFailures = 0;

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
                    // 恒久エラー(枠切れ・キー不正等)とみなして以降の送信を止める。
                    // 残りは未キャッシュのままなので、原因解消後の再生成で翻訳される。
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
                    // XMLタグ方式で復元(原文 slice[j] を渡し、復元漏れ時に原文を記録)
                    var outText = RestoreXml(translated[j], tokenMaps[j], result, slice[j]);
                    result.TranslatedChars += slice[j].Length;
                    dict[slice[j]] = outText;
                    TranslationCache.Put(slice[j], outText);
                }
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

        // 対象のユニーク原文を集める(翻訳が必要なキーの原文だけ)
        var unique = new List<string>();
        var seen = new HashSet<string>();
        foreach (var t in targets)
            foreach (var kv in t.TranslationTargets())
                if (!string.IsNullOrEmpty(kv.Value) && seen.Add(kv.Value)) unique.Add(kv.Value);

        var brokenSources = new List<string>();

        foreach (var src in unique)
        {
            var cachedTranslation = TranslationCache.Get(src);
            if (cachedTranslation == null) continue; // 未翻訳はここでは対象外

            // 原文に含まれるプレースホルダを実体(%s, {0}, §a 等)のまま数える。
            // 用語辞書の退避は「原文の語 -> 別の訳語」に変わるため、この照合には掛けない。
            // 用語が消えても表示は崩れないが、プレースホルダの欠落は表示を壊すので検査する。
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

    // ===== 復旧) 英語のまま固定されたキャッシュを削除する(DeepL枠を使わない) =====
    // 送信失敗時に原文をそのままキャッシュへ書いていた時期のデータを掃除する。
    // 訳文＝原文で固定されたエントリは以後キャッシュヒットし続け、
    // 何度生成しても英語のまま表示される。これを消して再翻訳可能に戻す。
    // printf系を含む原文は RepairPrintfPlaceholders が意図的に原文へ揃えた
    // 修復結果なので対象外にする(消すと表示崩れが再発するため)。
    // 戻り値は削除件数。
    public static int PurgeUntranslatedCache(
        IEnumerable<NamespaceLang> targets,
        string engine)
    {
        TranslationCache.Load(engine);
        var printfRegex = new Regex(@"%(\d+\$)?[sd]", RegexOptions.Compiled);

        var seen = new HashSet<string>();
        int removed = 0;
        foreach (var t in targets)
        {
            foreach (var kv in t.TranslationTargets())
            {
                var src = kv.Value;
                if (string.IsNullOrEmpty(src) || !seen.Add(src)) continue;
                if (printfRegex.IsMatch(src)) continue;

                var cached = TranslationCache.Get(src);
                if (cached == null) continue;
                if (!string.Equals(cached, src, StringComparison.Ordinal)) continue;

                if (TranslationCache.Remove(src)) removed++;
            }
        }
        if (removed > 0) TranslationCache.Save();
        return removed;
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

        // 既存ファイルがロックされている場合はリトライしながら削除する。
        // ロックが解けない場合でも FileMode.Create が上書きするため、削除失敗は無視してよい。
        for (int attempt = 0; File.Exists(outputZipPath) && attempt < 5; attempt++)
        {
            try { File.Delete(outputZipPath); break; }
            catch (IOException) { System.Threading.Thread.Sleep(200); }
        }

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
        // パックは MOD 同梱の ja_jp を丸ごと置き換えるため、同梱の訳もここに載せ直す。
        // 載せ直さないと、今まで日本語だったキーが英語に戻ってしまう。
        foreach (var t in targets)
        {
            // 入力順を保つため、原文辞書の列挙順で組み立てる
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

            var json = SerializeLangJson(outMap);
            WriteZipText(zip, $"assets/{t.Namespace}/lang/ja_jp.json", json);
        }

        result.OutputPath = outputZipPath;
    }

    // ファイルを削除する。ロック状態の場合は一定回数リトライする。
    private static void DeleteFileWithRetry(string filePath, int maxRetries = 5, int delayMs = 200)
    {
        if (!File.Exists(filePath)) return;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                File.Delete(filePath);
                return; // 成功
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                // ロック状態の場合は待機して再試行
                System.Threading.Thread.Sleep(delayMs);
            }
        }

        // すべてのリトライが失敗した場合、最後の例外を発生させる
        if (File.Exists(filePath))
            File.Delete(filePath);
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
        foreach (System.Text.RegularExpressions.Match m in PlaceholderRegex.Matches(src))
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
            // 直前の本文(エスケープ対象)
            if (sp.Start > pos)
                sb.Append(XmlEscape(src.Substring(pos, sp.Start - pos)));

            var id = idx.ToString();
            map[id] = sp.Restore;
            sb.Append($"<x id=\"{id}\"/>");
            idx++;
            pos = sp.Start + sp.Length;
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
        {
            var key = $"__k{k++}";
            tmp.Entries[key] = src;
            tmp.TranslateKeys.Add(key); // TranslateAsync は翻訳対象キーだけを見るため必須
        }

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
