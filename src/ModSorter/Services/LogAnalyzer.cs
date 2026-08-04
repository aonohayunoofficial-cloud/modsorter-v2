using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ModSorter.Services;

// 実行ログ(logs\latest.log 等)を解析する。
// クラッシュレポート(crash-reports\*.txt)が「落ちた瞬間の1件」だけを持つのに対し、
// latest.log は起動から終了までの全ログで、落ちなかった Mixin 失敗・依存不足・警告も残る。
// 形式が別物なので CrashAnalyzer とは別実装にし、結果の型(CrashAnalyzer.Result / Issue)だけ
// 共通にして、既存のクラッシュ解析UI(中央リスト・右詳細・削除ボタン)にそのまま載せる。
public static class LogAnalyzer
{
    // latest.log は数十MBになることがあるので末尾だけ読む。
    private const long MaxBytes = 8L * 1024 * 1024;
    private const int MaxChars = 4_000_000;

    private const string Unknown = "(特定不可)";

    // 経路に出るだけで原因ではないことが多いID。CrashAnalyzer 側の一覧とは別に持つ。
    // ログにはローダー本体・ライブラリのログ行が大量に出るため、ここは独自に増やす。
    private static readonly HashSet<string> CoreIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "minecraft", "neoforge", "forge", "fml", "fabricloader", "fabric", "quilt_loader",
        "java", "mixin", "mixinextras", "mojang", "modlauncher", "securejarhandler",
        "connectormod", "sinytra_connector", "connector",
        "fabric_api", "fabric-api", "architectury", "cloth_config", "owo", "veil",
        "ponder", "catnip", "flywheel", "kubejs", "rhino",
        "yet_another_config_lib_v3",
    };

    public static CrashAnalyzer.Result Analyze(string filePath)
    {
        var result = new CrashAnalyzer.Result();

        string text;
        try { text = ReadTail(filePath); }
        catch (Exception ex)
        {
            result.Description = $"(ログの読み込みに失敗: {ex.Message})";
            return result;
        }

        DetectEnvironment(text, result);

        var raw = new List<CrashAnalyzer.Issue>();
        var missing = new List<string>();

        AddForgeDependencies(text, raw, missing);
        AddFabricDependencies(text, raw, missing);
        AddDuplicates(text, raw);
        AddMixinFailures(text, raw);
        AddExceptions(text, raw);
        AddFatalLines(text, raw);

        missing.Sort(StringComparer.OrdinalIgnoreCase);
        result.MissingDependencies = missing;
        result.Issues = Merge(raw);
        result.ParsedAsModLoading = result.Issues.Count > 0;

        int errorLines = Regex.Matches(text, @"/(?:ERROR|FATAL)\]").Count;
        int warnLines = Regex.Matches(text, @"/WARN\]").Count;
        result.Description = $"ERROR/FATAL {errorLines} 行 / WARN {warnLines} 行";

        return result;
    }

    // ゲーム起動中でも読めるよう共有指定で開く。巨大なログは末尾だけ読む。
    private static string ReadTail(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var gr = new StreamReader(gz, Encoding.UTF8);
            string all = gr.ReadToEnd();
            return all.Length > MaxChars ? all.Substring(all.Length - MaxChars) : all;
        }

        // 途中から読むと先頭の1文字が壊れることがあるが、行単位の解析なので影響しない。
        if (fs.Length > MaxBytes) fs.Seek(-MaxBytes, SeekOrigin.End);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    // 環境(Minecraftのバージョンとローダー)を拾う。ログ冒頭の起動行から取る。
    private static void DetectEnvironment(string text, CrashAnalyzer.Result result)
    {
        var f = Regex.Match(text,
            @"(?<loader>NeoForge|Forge) mod loading, version (?<lv>[\w\.\-\+]+), for MC (?<mc>[\w\.\-]+)");
        if (f.Success)
        {
            result.Loader = $"{f.Groups["loader"].Value} {f.Groups["lv"].Value}";
            result.MinecraftVersion = f.Groups["mc"].Value;
            return;
        }

        var fab = Regex.Match(text,
            @"Loading Minecraft (?<mc>[\w\.\-]+) with (?<loader>Fabric Loader|Quilt Loader) (?<lv>[\w\.\-\+]+)");
        if (fab.Success)
        {
            result.MinecraftVersion = fab.Groups["mc"].Value;
            result.Loader = $"{fab.Groups["loader"].Value} {fab.Groups["lv"].Value}";
            return;
        }

        var mc = Regex.Match(text, @"--fml\.mcVersion,?\s*(?<mc>[\w\.\-]+)");
        if (mc.Success) result.MinecraftVersion = mc.Groups["mc"].Value;
    }

    // Forge/NeoForge の依存エラー一覧。
    //   Mod ID: 'jei', Requested by: 'somemod', Expected range: '[15,)', Actual version: '[MISSING]'
    private static void AddForgeDependencies(
        string text, List<CrashAnalyzer.Issue> list, List<string> missing)
    {
        foreach (Match m in Regex.Matches(text,
            @"Mod ID:\s*'(?<dep>[^']*)',\s*Requested by:\s*'(?<by>[^']*)',\s*" +
            @"Expected range:\s*'(?<req>[^']*)',\s*Actual version:\s*'(?<cur>[^']*)'"))
        {
            string dep = m.Groups["dep"].Value.Trim();
            string by = m.Groups["by"].Value.Trim();
            string req = m.Groups["req"].Value.Trim();
            string cur = m.Groups["cur"].Value.Trim();
            bool notInstalled = cur.Contains("MISSING", StringComparison.OrdinalIgnoreCase);

            list.Add(new CrashAnalyzer.Issue
            {
                Kind = notInstalled
                    ? CrashAnalyzer.IssueKind.MissingDependency
                    : CrashAnalyzer.IssueKind.VersionMismatch,
                ModId = by,
                DependencyId = dep,
                Requirement = req,
                CurrentState = cur,
                RawFailure = Trim(m.Value, 400),
                JapaneseSummary = notInstalled
                    ? $"「{by}」は前提MOD「{dep}」が必要ですが、インストールされていません。\n" +
                      $"     必要バージョン: {req}"
                    : $"「{by}」が要求する「{dep}」のバージョンが合っていません。\n" +
                      $"     必要バージョン: {req}\n" +
                      $"     現在のバージョン: {cur}"
            });

            if (notInstalled && !missing.Contains(dep, StringComparer.OrdinalIgnoreCase))
                missing.Add(dep);
        }
    }

    // Fabric/Quilt の依存エラー。
    //   Mod 'Some Mod' (somemod) 1.0.0 requires any version of jei, which is missing!
    private static void AddFabricDependencies(
        string text, List<CrashAnalyzer.Issue> list, List<string> missing)
    {
        foreach (Match m in Regex.Matches(text,
            @"Mod '(?<name>[^']*)' \((?<id>[\w\-\.]+)\)[^\r\n]*?requires (?<req>[^\r\n]*?) of " +
            @"(?<dep>[\w\-\.]+), which is missing",
            RegexOptions.IgnoreCase))
        {
            string id = m.Groups["id"].Value.Trim();
            string dep = m.Groups["dep"].Value.Trim();
            string req = m.Groups["req"].Value.Trim();

            list.Add(new CrashAnalyzer.Issue
            {
                Kind = CrashAnalyzer.IssueKind.MissingDependency,
                ModId = id,
                DependencyId = dep,
                Requirement = req,
                CurrentState = "not installed",
                RawFailure = Trim(m.Value, 400),
                JapaneseSummary =
                    $"「{id}」は前提MOD「{dep}」が必要ですが、インストールされていません。\n" +
                    $"     必要バージョン: {req}"
            });

            if (!missing.Contains(dep, StringComparer.OrdinalIgnoreCase))
                missing.Add(dep);
        }
    }

    // 同じMODが二重に入っているケース。
    private static void AddDuplicates(string text, List<CrashAnalyzer.Issue> list)
    {
        foreach (Match m in Regex.Matches(text,
            @"^[^\r\n]*(?:Found duplicate mods|Duplicate mod ID|duplicate mod file)[^\r\n]*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            string line = m.Value.Trim();
            var idm = Regex.Match(line, @"'(?<id>[\w\-\.]+)'");
            list.Add(new CrashAnalyzer.Issue
            {
                Kind = CrashAnalyzer.IssueKind.Incompatible,
                ModId = idm.Success ? idm.Groups["id"].Value : Unknown,
                RawFailure = Trim(line, 400),
                JapaneseSummary =
                    "同じMODが二重に入っています。新しい方を1つだけ残し、もう一方のjarを削除してください。\n" +
                    $"     ログ: {Trim(line, 200)}"
            });
        }
    }

    // Mixin の適用失敗。MOD同士の競合か、対応していないバージョンの組み合わせで出る。
    private static void AddMixinFailures(string text, List<CrashAnalyzer.Issue> list)
    {
        foreach (Match m in Regex.Matches(text,
            @"Mixin apply for mod (?<id>[\w\-\.]+) failed (?<cfg>\S+?):(?<mixin>\S+)"))
            list.Add(MixinIssue(m.Groups["id"].Value, m.Groups["mixin"].Value, m.Value));

        foreach (Match m in Regex.Matches(text,
            @"Mixin apply failed (?<cfg>[\w\-\.]+)\.mixins\.json:(?<mixin>\S+)"))
            list.Add(MixinIssue(ModIdFromConfig(m.Groups["cfg"].Value),
                m.Groups["mixin"].Value, m.Value));

        foreach (Match m in Regex.Matches(text,
            @"Critical injection failure:[^\r\n]*?in (?<cfg>[\w\-\.]+)\.mixins\.json:(?<mixin>\S+)"))
            list.Add(MixinIssue(ModIdFromConfig(m.Groups["cfg"].Value),
                m.Groups["mixin"].Value, m.Value));
    }

    private static CrashAnalyzer.Issue MixinIssue(string modId, string mixin, string rawLine)
        => new()
        {
            Kind = CrashAnalyzer.IssueKind.RuntimeError,
            ModId = modId,
            RawFailure = Trim(rawLine, 400),
            JapaneseSummary =
                $"「{modId}」の Mixin({mixin}) の適用に失敗しています。\n" +
                "     他MODが同じ場所を書き換えている(競合)か、MOD側がこのMinecraft/ローダーの\n" +
                "     バージョンに対応していないときに出ます。\n" +
                $"     ・「{modId}」を最新版に更新する\n" +
                $"     ・直らなければ「{modId}」を一旦外して起動できるか確認する"
        };

    // "somemod.mixins.json" の somemod、"mixins.somemod.json" の somemod を MOD ID とみなす。
    private static string ModIdFromConfig(string cfg)
    {
        string s = (cfg ?? "").Trim();
        if (s.StartsWith("mixins.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
        int dot = s.IndexOf('.');
        if (dot > 0) s = s.Substring(0, dot);
        return s.Length == 0 ? Unknown : s;
    }

    // 例外とスタックトレース。例外行と、直後に続く「at ...」「Caused by:」を1ブロックとして扱う。
    private static void AddExceptions(string text, List<CrashAnalyzer.Issue> list)
    {
        foreach (Match m in Regex.Matches(text,
            @"^(?:\[[^\]\r\n]*\]\s*)*(?<type>[\w\.\$]+(?:Exception|Error))(?::\s*(?<msg>[^\r\n]*))?\r?\n" +
            @"(?<body>(?:[ \t]+(?:at |\.\.\.)[^\r\n]*\r?\n|Caused by:[^\r\n]*\r?\n|[ \t]+Suppressed:[^\r\n]*\r?\n)+)",
            RegexOptions.Multiline))
        {
            string type = m.Groups["type"].Value;
            string msg = m.Groups["msg"].Value.Trim();
            string body = m.Groups["body"].Value;

            // 最後の Caused by を根本原因とする。無ければ最上段をそのまま使う。
            string rootType = type, rootMsg = msg;
            foreach (Match cm in Regex.Matches(body,
                @"Caused by:\s*(?<t>[\w\.\$]+(?:Exception|Error))(?::\s*(?<m>[^\r\n]*))?"))
            {
                rootType = cm.Groups["t"].Value.Trim();
                rootMsg = cm.Groups["m"].Value.Trim();
            }

            // MOD帰属タグを拾う。パッケージ名からの推定は誤爆しやすいので使わない。
            var involved = new List<string>();
            string suspect = "";
            foreach (Match fm in Regex.Matches(m.Value, @"TRANSFORMER/(?<id>[\w\-]+)@"))
                AddId(involved, ref suspect, fm.Groups["id"].Value);
            foreach (Match fm in Regex.Matches(m.Value, @"from mod\s+(?<id>[\w\-]+)",
                RegexOptions.IgnoreCase))
                AddId(involved, ref suspect, fm.Groups["id"].Value);

            var issue = new CrashAnalyzer.Issue
            {
                Kind = CrashAnalyzer.IssueKind.RuntimeError,
                ModId = suspect.Length > 0 ? suspect : Unknown,
                TopException = type,
                RootException = rootType,
                RootMessage = rootMsg,
                InvolvedMods = involved,
                RawFailure = Trim(type + (msg.Length > 0 ? ": " + msg : ""), 400)
            };

            string detail = $"     種別: {rootType}" +
                (rootMsg.Length > 0 ? $"\n     内容: {Trim(rootMsg, 200)}" : "");

            issue.JapaneseSummary = suspect.Length > 0
                ? $"「{suspect}」の処理中に実行時エラーが発生しています。\n{detail}\n" +
                  $"     ・「{suspect}」を最新版に更新する\n" +
                  $"     ・直らなければ「{suspect}」を一旦外して起動できるか確認する"
                : $"実行時エラーが記録されていますが、発生源のMODを特定できませんでした。\n{detail}";

            list.Add(issue);
        }
    }

    // FATAL 行。ローダーが致命的と判断したもの。
    private static void AddFatalLines(string text, List<CrashAnalyzer.Issue> list)
    {
        foreach (Match m in Regex.Matches(text,
            @"^\[[^\]\r\n]*\]\s*\[[^\]\r\n]*/FATAL\][^\r\n]*$", RegexOptions.Multiline))
        {
            string line = m.Value.Trim();
            list.Add(new CrashAnalyzer.Issue
            {
                Kind = CrashAnalyzer.IssueKind.RuntimeError,
                ModId = TagOf(line),
                RawFailure = Trim(line, 400),
                JapaneseSummary =
                    "ログに致命的エラー(FATAL)が記録されています。\n     " + Trim(line, 300)
            });
        }
    }

    // ログ行の「[logger/TAG]:」の TAG を MOD ID とみなす。
    private static string TagOf(string line)
    {
        var m = Regex.Match(line, @"\[[^\]\r\n]*?/(?<tag>[A-Za-z0-9_\-]+)\]:");
        if (!m.Success) return Unknown;
        string tag = m.Groups["tag"].Value.ToLowerInvariant();
        return CoreIds.Contains(tag) ? Unknown : tag;
    }

    private static void AddId(List<string> involved, ref string suspect, string id)
    {
        if (!involved.Contains(id, StringComparer.OrdinalIgnoreCase)) involved.Add(id);
        if (suspect.Length == 0 && !CoreIds.Contains(id)) suspect = id;
    }

    // 同じ内容の繰り返しを1件にまとめ、原因になりやすい順に並べる。
    // 発生源を特定できなかった実行時エラーはログに大量に出るので、後ろに5件までとする。
    private static List<CrashAnalyzer.Issue> Merge(List<CrashAnalyzer.Issue> raw)
    {
        var order = new List<string>();
        var map = new Dictionary<string, CrashAnalyzer.Issue>(StringComparer.Ordinal);
        var count = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var it in raw)
        {
            string key = $"{it.Kind}|{it.ModId}|{Trim(it.RawFailure, 120)}";
            if (!map.ContainsKey(key))
            {
                map[key] = it;
                order.Add(key);
                count[key] = 0;
            }
            count[key]++;
        }

        var merged = new List<CrashAnalyzer.Issue>();
        foreach (var key in order)
        {
            var it = map[key];
            if (count[key] > 1)
                it.JapaneseSummary += $"\n     (同じ内容がログ中に {count[key]} 回)";
            merged.Add(it);
        }

        static int Rank(CrashAnalyzer.Issue i) => i.Kind switch
        {
            CrashAnalyzer.IssueKind.MissingDependency => 0,
            CrashAnalyzer.IssueKind.VersionMismatch => 1,
            CrashAnalyzer.IssueKind.Incompatible => 2,
            CrashAnalyzer.IssueKind.RuntimeError => 3,
            _ => 4
        };

        var known = merged.Where(i => i.ModId != Unknown).OrderBy(Rank).ToList();
        var unknown = merged.Where(i => i.ModId == Unknown).OrderBy(Rank).Take(5).ToList();
        known.AddRange(unknown);
        return known;
    }

    private static string Trim(string s, int max)
    {
        s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
