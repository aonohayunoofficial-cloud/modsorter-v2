using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // assets/<ns>/lang/<en_us|ja_jp>.<json|lang> だけを対象にする。
    private static readonly Regex LangEntryRegex = new(
        @"^assets/([^/]+)/lang/(en_us|ja_jp)\.(json|lang)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ===== 1) 抽出 + 差分判定 =====
    // jar 群から en_us と ja_jp を両方読み、名前空間ごとに「翻訳が必要なキー」を確定する。
    // fillMissingOnly=true: 同梱 ja_jp を尊重し、そこに無いキーと英語のまま残っているキー
    //   だけを翻訳対象にする。同梱の訳は Existing に持ち、出力にもそのまま載せる。
    // fillMissingOnly=false: 同梱 ja_jp を無視し、en_us の全キーを翻訳対象にする。
    //
    // 同じ jar 構成で繰り返し呼ばれたときは展開し直さず、前回の結果を返す。
    public static List<NamespaceLang> ExtractTargets(
        IEnumerable<string> jarPaths,
        bool fillMissingOnly,
        LangPackResult result)
    {
        var paths = jarPaths?.ToList() ?? new List<string>();
        if (TryGetCachedExtract(paths, fillMissingOnly, result, out var cached))
            return cached;

        // 集計はいったん作業用へ取り、確定後に呼び出し側へ写す。
        // こうしておくと、次回のキャッシュヒット時に同じ数字を再現できる。
        var work = new LangPackResult();
        var map = new Dictionary<string, NamespaceLang>();
        var jaMap = new Dictionary<string, Dictionary<string, string>>();

        foreach (var jar in paths)
        {
            work.ModCount++;
            // この jar を走査する前の失敗件数。走査後に増えていなければ
            // 「開けたが lang が無い」と「途中で失敗した」を区別できる。
            var skipsBefore = work.SkippedBroken;
            try
            {
                using var zip = ZipFile.OpenRead(jar);
                var found = ScanArchive(zip, Path.GetFileName(jar), jar, map, jaMap, work, 0);
                // lang が1件も無く、かつ失敗も出ていない jar。
                // 失敗ではないが翻訳対象も無いので、消えずに残るよう記録する。
                if (found == 0 && work.SkippedBroken == skipsBefore)
                    work.NoteNoLang(Path.GetFileName(jar));
            }
            catch (Exception ex)
            {
                // zip として開けない。壊れている・別プロセスがロックしている等。
                work.NoteSkip(Path.GetFileName(jar),
                    $"{ex.GetType().Name}: {ex.Message}", isJar: true);
            }
        }

        var targets = BuildTargets(map, jaMap, fillMissingOnly, work);
        StoreExtract(paths, fillMissingOnly, work, targets);
        result.CopyExtractCountersFrom(work);
        return targets;
    }

    // 1つの zip を走査する。同梱 jar(jar-in-jar)にも同じ手順で潜る。
    // Forge の META-INF/jarjar 配下に本体を抱える MOD は、潜らないと lang が
    // 1件も見つからず、失敗として数えられないまま丸ごと未翻訳になる。
    //
    // 戻り値は見つかった lang ファイルの数(同梱 jar の中の分も含む)。
    // 解析に成功したかではなく「対象パスに合致したか」を数える。
    // 0 なら lang を持たない jar であり、失敗とは別物として扱う。
    private static int ScanArchive(
        ZipArchive zip, string label, string sourceJar,
        Dictionary<string, NamespaceLang> map,
        Dictionary<string, Dictionary<string, string>> jaMap,
        LangPackResult result, int depth)
    {
        int found = 0;

        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                // 2段まで。これ以上の入れ子は実在しないうえ展開コストが跳ねる。
                if (depth >= 2 || e.Length <= 0) continue;
                var innerLabel = label + "!" + e.FullName;
                try
                {
                    using var ms = new MemoryStream();
                    using (var es = e.Open()) es.CopyTo(ms);
                    ms.Position = 0;
                    using var inner = new ZipArchive(ms, ZipArchiveMode.Read);
                    result.NestedJars++;
                    found += ScanArchive(
                        inner, innerLabel, sourceJar, map, jaMap, result, depth + 1);
                }
                catch (Exception ex)
                {
                    result.NoteSkip(innerLabel,
                        $"{ex.GetType().Name}: {ex.Message}", isJar: true);
                }
                continue;
            }

            var m = LangEntryRegex.Match(e.FullName);
            if (!m.Success) continue;

            found++;

            var ns = m.Groups[1].Value;
            var kind = m.Groups[2].Value.ToLowerInvariant();  // en_us / ja_jp
            var ext = m.Groups[3].Value.ToLowerInvariant();   // json / lang
            var where = $"{label}!{e.FullName}";

            Dictionary<string, string> parsed;
            try
            {
                var text = ReadEntry(e);
                if (ext == "json")
                {
                    parsed = ParseJsonFlexible(text, out var mode, out var note);
                    if (mode == LangParseMode.Repaired)
                        result.NoteRepair(where, $"{parsed.Count} キーを救済 / {note}");
                    else if (mode == LangParseMode.Empty)
                        result.NoteEmpty(where);
                }
                else parsed = ParseLang(text);
            }
            catch (Exception ex)
            {
                result.NoteSkip(where, $"{ex.GetType().Name}: {ex.Message}", isJar: false);
                continue;
            }

            if (parsed.Count == 0) continue;

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
            if (!nl.SourceJars.Contains(sourceJar)) nl.SourceJars.Add(sourceJar);
            // 後勝ちマージ(仕様書9章)
            foreach (var kv in parsed) nl.Entries[kv.Key] = kv.Value;
        }

        return found;
    }
}
