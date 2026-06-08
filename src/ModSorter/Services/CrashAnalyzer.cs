using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ModSorter.Services;

// クラッシュレポート(*.txt)を正規表現で解析して要約を作る
public static class CrashAnalyzer
{
    // 失敗の種類
    public enum IssueKind
    {
        MissingDependency,   // 前提MODが入っていない
        VersionMismatch,     // 入ってはいるがバージョンが合わない
        Incompatible,        // 競合(共存不可)
        RuntimeError,        // 実行時例外(初期化中クラッシュ等)
        Unknown
    }

    // 1件のロード失敗issueを表す
    public class Issue
    {
        public string ModId = "";          // 問題のMOD
        public string ModFile = "";        // jarファイル名
        public string RawFailure = "";     // 原文(Failure message)
        public string RawCurrent = "";     // 原文("Currently, ...")
        public string Reason = "";         // The reason is: の補足(あれば)

        public IssueKind Kind = IssueKind.Unknown;
        public string DependencyId = "";   // 依存先・競合先のID
        public string Requirement = "";    // 要求条件(例: 6.0.9 or above, and below 6.0.10)
        public string CurrentState = "";   // 現状(例: 6.0.10 / not installed)
        public string JapaneseSummary = ""; // 日本語の要約文

        // ランタイム例外解析用
        public string TopException = "";     // 最上段の例外型
        public string RootException = "";    // 根本原因の例外型
        public string RootMessage = "";      // 根本原因のメッセージ
        public List<string> InvolvedMods = new();  // トレースに登場したMOD

        // 中央リスト表示用のラベル
        public string KindLabel => Kind switch
        {
            IssueKind.MissingDependency => "[前提MOD不足]",
            IssueKind.VersionMismatch => "[バージョン不一致]",
            IssueKind.Incompatible => "[競合]",
            IssueKind.RuntimeError => "[実行時エラー]",
            _ => "[その他]"
        };
    }

    public class Result
    {
        public string Description = "";
        public List<Issue> Issues = new();
        public string MinecraftVersion = "";
        public string Loader = "";
        public bool ParsedAsModLoading = false;
        // 不足している前提MODのID一覧(not installed のもの・重複なし)
        public List<string> MissingDependencies = new();
    }

    public static Result Analyze(string filePath)
    {
        var result = new Result();
        string text;
        try { text = File.ReadAllText(filePath); }
        catch (Exception ex)
        {
            result.Description = $"(レポートの読み込みに失敗: {ex.Message})";
            return result;
        }

        var descM = Regex.Match(text, @"^Description:\s*(.+)$", RegexOptions.Multiline);
        if (descM.Success) result.Description = descM.Groups[1].Value.Trim();

        var mcM = Regex.Match(text, @"Minecraft Version:\s*(.+)");
        if (mcM.Success) result.MinecraftVersion = mcM.Groups[1].Value.Trim();

        var loaderM = Regex.Match(text, @"(NeoForge|Forge|Fabric Loader)\s*[:#]?\s*([0-9][\w\.\-\+]*)");
        if (loaderM.Success)
            result.Loader = $"{loaderM.Groups[1].Value} {loaderM.Groups[2].Value}";

        var blockRegex = new Regex(
            @"-- Mod loading issue for:\s*(?<id>[^\-]+?)\s*--(?<body>.*?)(?=-- Mod loading issue for:|-- System Details --|\z)",
            RegexOptions.Singleline);

        var missingSet = new List<string>();

        foreach (Match m in blockRegex.Matches(text))
        {
            result.ParsedAsModLoading = true;
            var issue = new Issue { ModId = m.Groups["id"].Value.Trim() };
            var body = m.Groups["body"].Value;

            var fileM = Regex.Match(body, @"Mod file:\s*(.+)");
            if (fileM.Success)
                issue.ModFile = Path.GetFileName(fileM.Groups[1].Value.Trim());

            var failM = Regex.Match(body, @"Failure message:\s*(.+)");
            if (failM.Success) issue.RawFailure = failM.Groups[1].Value.Trim();

            var curM = Regex.Match(body, @"Currently,\s*(.+)");
            if (curM.Success) issue.RawCurrent = curM.Groups[1].Value.Trim();

            var reasonM = Regex.Match(body, @"The reason is:\s*(.+)");
            if (reasonM.Success) issue.Reason = reasonM.Groups[1].Value.Trim();

            Classify(issue);

            // 不足依存を集計(重複なし)
            if (issue.Kind == IssueKind.MissingDependency &&
                !string.IsNullOrEmpty(issue.DependencyId) &&
                !missingSet.Contains(issue.DependencyId))
            {
                missingSet.Add(issue.DependencyId);
            }

            result.Issues.Add(issue);
        }

        missingSet.Sort(StringComparer.OrdinalIgnoreCase);
        result.MissingDependencies = missingSet;

        // ロード失敗ブロックが無ければランタイム例外として解析を試みる
        if (!result.ParsedAsModLoading)
            AnalyzeRuntime(text, result);

        return result;
    }

    private static readonly HashSet<string> _coreMods = new(StringComparer.OrdinalIgnoreCase)
    {
        "minecraft", "neoforge", "forge", "fabricloader", "fabric",
        "java", "mixinextras", "connectormod", "sinytra_connector", "connector",
        // 経路に出やすいライブラリ/API系(原因ではないことが多い)
        "ponder", "catnip", "flywheel", "owo", "veil",
        "architectury", "cloth_config", "yet_another_config_lib_v3",
        "fabric_api", "kubejs", "rhino",
    };


    // ランタイム例外(初期化中クラッシュ等)を解析して容疑者MODを特定する
    private static void AnalyzeRuntime(string text, Result result)
    {
        // スタックトレース本体を取り出す("A detailed walkthrough" や末尾まで)
        // Description 直後の例外行から System Details の手前までを対象にする
        var stackM = Regex.Match(text,
            @"Description:.*?(?=-- System Details --|A detailed walkthrough|\z)",
            RegexOptions.Singleline);
        string stack = stackM.Success ? stackM.Value : text;

        // 最上段の例外型: 1行目の "xxx.Exception: ..." または "xxx.Error: ..."
        var topM = Regex.Match(stack, @"^([\w\.\$]+(?:Exception|Error))(?::\s*(.*))?$",
            RegexOptions.Multiline);

        // 最後の "Caused by:" を根本原因とする
        string rootType = "", rootMsg = "";
        foreach (Match cm in Regex.Matches(stack,
            @"Caused by:\s*([\w\.\$]+(?:Exception|Error))(?::\s*(.*))?"))
        {
            rootType = cm.Groups[1].Value.Trim();
            rootMsg = cm.Groups[2].Value.Trim();
        }
        // Caused by が無ければ最上段をそのまま根本原因に
        if (string.IsNullOrEmpty(rootType) && topM.Success)
        {
            rootType = topM.Groups[1].Value.Trim();
            rootMsg = topM.Groups[2].Value.Trim();
        }

        // スタック全体から MOD 帰属タグを収集
        //   TRANSFORMER/<modid>@<ver>/...
        //   {... from mod <modid>}
        var involved = new List<string>();
        string? suspect = null;

        // TRANSFORMER タグ(出現順 = スタックの深さ順)
        foreach (Match fm in Regex.Matches(stack,
            @"TRANSFORMER/(?<id>[\w\-]+)@(?<ver>[\w\.\-\+]+)/"))
        {
            string id = fm.Groups["id"].Value;
            if (!involved.Contains(id, StringComparer.OrdinalIgnoreCase))
                involved.Add(id);
            // コア以外で最初に見つかったものを容疑者にする
            if (suspect == null && !_coreMods.Contains(id))
                suspect = $"{id} {fm.Groups["ver"].Value}";
        }

        // "from mod <modid>" 形式(mixin 由来)
        foreach (Match fm in Regex.Matches(stack,
            @"from mod\s+(?<id>[\w\-]+)", RegexOptions.IgnoreCase))
        {
            string id = fm.Groups["id"].Value;
            if (!involved.Contains(id, StringComparer.OrdinalIgnoreCase))
                involved.Add(id);
            if (suspect == null && !_coreMods.Contains(id))
                suspect = id;
        }

        var issue = new Issue
        {
            Kind = IssueKind.RuntimeError,
            ModId = suspect ?? "(特定不可)",
            TopException = topM.Success ? topM.Groups[1].Value.Trim() : "",
            RootException = rootType,
            RootMessage = rootMsg,
            InvolvedMods = involved,
        };

        // 日本語要約(現象 → 対処の形)
        var sj = new StringBuilder();
        if (suspect != null)
        {
            var (explain, advice) = ExplainRuntime(rootType, rootMsg, suspect);
            sj.AppendLine($"【何が起きたか】");
            sj.AppendLine($"     {explain}");
            sj.AppendLine();
            sj.AppendLine($"【試すとよい対処】");
            sj.AppendLine($"     {advice}");
        }
        else
        {
            sj.AppendLine("実行時エラーの発生源MODを特定できませんでした。");
            if (!string.IsNullOrEmpty(rootType))
                sj.AppendLine($"     エラー種別: {rootType}");
        }

        // 技術詳細は参考として最小限だけ残す
        sj.AppendLine();
        sj.Append("     (参考: ");
        sj.Append(string.IsNullOrEmpty(rootType) ? "原因不明" : rootType);
        if (!string.IsNullOrEmpty(rootMsg)) sj.Append($" / {rootMsg}");
        sj.Append(")");

        issue.JapaneseSummary = sj.ToString().TrimEnd();

        result.Issues.Add(issue);
    }

    // Failure message と Currently を分類して日本語要約を作る
    private static void Classify(Issue issue)
    {
        string fail = issue.RawFailure;
        string cur = issue.RawCurrent;

        // 現状が "not installed" かどうか
        bool notInstalled = Regex.IsMatch(cur, @"is not installed", RegexOptions.IgnoreCase);

        // 現状のバージョン: "xxx is 1.2.3" の 1.2.3 部分
        var curVerM = Regex.Match(cur, @"\bis\s+(.+)$");
        issue.CurrentState = curVerM.Success ? curVerM.Groups[1].Value.Trim() : cur;

        // requires
        var reqM = Regex.Match(fail,
            @"requires\s+(?<dep>\S+)\s+(?<req>.+)$", RegexOptions.IgnoreCase);
        // only supports
        var supM = Regex.Match(fail,
            @"only supports\s+(?<dep>\S+)\s+(?<req>.+)$", RegexOptions.IgnoreCase);
        // is incompatible with
        var incM = Regex.Match(fail,
            @"is incompatible with\s+(?<dep>\S+)(?:\s+(?<req>.+))?$", RegexOptions.IgnoreCase);

        if (incM.Success)
        {
            issue.Kind = IssueKind.Incompatible;
            issue.DependencyId = incM.Groups["dep"].Value.Trim();
            issue.JapaneseSummary =
                $"「{issue.ModId}」は「{issue.DependencyId}」と競合します(共存できません)。";
            if (!string.IsNullOrEmpty(issue.Reason))
                issue.JapaneseSummary += $"\n     補足: {issue.Reason}";
        }
        else if (reqM.Success || supM.Success)
        {
            var mm = reqM.Success ? reqM : supM;
            issue.DependencyId = mm.Groups["dep"].Value.Trim();
            issue.Requirement = mm.Groups["req"].Value.Trim();

            if (notInstalled)
            {
                issue.Kind = IssueKind.MissingDependency;
                issue.JapaneseSummary =
                    $"「{issue.ModId}」は前提MOD「{issue.DependencyId}」が必要ですが、" +
                    $"インストールされていません。\n     必要バージョン: {issue.Requirement}";
            }
            else
            {
                issue.Kind = IssueKind.VersionMismatch;
                string verb = supM.Success ? "の対応バージョン範囲外です" : "のバージョンが要件を満たしません";
                issue.JapaneseSummary =
                    $"「{issue.ModId}」は「{issue.DependencyId}」{verb}。\n" +
                    $"     必要バージョン: {issue.Requirement}\n" +
                    $"     現在のバージョン: {issue.CurrentState}";
            }
        }
        else
        {
            issue.Kind = IssueKind.Unknown;
            issue.JapaneseSummary = issue.RawFailure; // 分類できなければ原文
        }
    }

    public static string Format(Result r)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(r.MinecraftVersion) || !string.IsNullOrEmpty(r.Loader))
        {
            sb.Append("環境: ");
            if (!string.IsNullOrEmpty(r.MinecraftVersion))
                sb.Append($"Minecraft {r.MinecraftVersion}");
            if (!string.IsNullOrEmpty(r.Loader))
                sb.Append($" / {r.Loader}");
            sb.AppendLine();
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(r.Description))
        {
            sb.AppendLine($"概要: {r.Description}");
            sb.AppendLine();
        }

        if (!r.ParsedAsModLoading)
        {
            // ランタイム例外として解析できていれば、その結果を出す
            var rtIssues = r.Issues.Where(x => x.Kind == IssueKind.RuntimeError).ToList();
            if (rtIssues.Count > 0)
            {
                sb.AppendLine("■ 実行時エラー(初期化中クラッシュ等)として解析しました");
                sb.AppendLine(new string('=', 40));
                sb.AppendLine();
                int n = 1;
                foreach (var issue in rtIssues)
                {
                    sb.AppendLine($"[{n}] {issue.KindLabel} {issue.ModId}");
                    sb.AppendLine($"     {issue.JapaneseSummary}");
                    sb.AppendLine();
                    n++;
                }
                return sb.ToString();
            }

            sb.AppendLine("(MODロード失敗形式として解析できませんでした。)");
            sb.AppendLine("別の種類のクラッシュの可能性があります。");
            return sb.ToString();
        }

        // 不足している前提MODのまとめ(先に出すと対処しやすい)
        if (r.MissingDependencies.Count > 0)
        {
            sb.AppendLine($"■ 不足している前提MOD: {r.MissingDependencies.Count} 件");
            foreach (var dep in r.MissingDependencies)
                sb.AppendLine($"    ・{dep}");
            sb.AppendLine();
        }

        sb.AppendLine($"■ MODロード失敗: {r.Issues.Count} 件");
        sb.AppendLine(new string('=', 40));
        sb.AppendLine();

        int i = 1;
        foreach (var issue in r.Issues)
        {
            string tag = issue.Kind switch
            {
                IssueKind.MissingDependency => "[前提MOD不足]",
                IssueKind.VersionMismatch => "[バージョン不一致]",
                IssueKind.Incompatible => "[競合]",
                _ => "[その他]"
            };
            sb.AppendLine($"[{i}] {tag} {issue.ModId}");
            if (!string.IsNullOrEmpty(issue.ModFile))
                sb.AppendLine($"     ファイル: {issue.ModFile}");
            sb.AppendLine($"     {issue.JapaneseSummary}");
            sb.AppendLine();
            i++;
        }

        return sb.ToString();
    }
    // 根本原因メッセージから「平易な説明」と「対処案」を作る
    private static (string explain, string advice) ExplainRuntime(
        string rootType, string rootMsg, string suspect)
    {
        string id = suspect.Split(' ')[0];  // バージョンを除いたMOD名

        // よくあるパターンを判定
        if (Regex.IsMatch(rootMsg, "config", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(rootMsg, "before|not loaded|loaded", RegexOptions.IgnoreCase))
        {
            return (
                $"「{id}」が、設定(config)が読み込まれる前に設定値を読もうとして落ちています。" +
                "MOD側の不具合か、対応していないバージョンの組み合わせで起きやすい現象です。",
                $"・「{id}」を最新版に更新してみてください。\n" +
                $"     ・それでも直らなければ「{id}」を一旦外して起動できるか確認してください。\n" +
                $"     ・「{id}」の config ファイルを削除して初期化すると直る場合があります。"
            );
        }
        if (rootType.Contains("NoClassDefFound") || rootType.Contains("ClassNotFound"))
        {
            return (
                $"「{id}」が必要とするクラスが見つからず落ちています。前提MODが不足しているか、" +
                "バージョンが噛み合っていない可能性が高いです。",
                $"・「{id}」の必要前提MOD(ライブラリ)が入っているか確認してください。\n" +
                $"     ・「{id}」と前提MODのバージョンを揃えてください。"
            );
        }
        if (rootType.Contains("NoSuchMethod") || rootType.Contains("NoSuchField"))
        {
            return (
                $"「{id}」が古い/新しいAPIを呼んでおり、他MODとバージョンが噛み合っていません。",
                $"・「{id}」と、関連する前提MODのバージョンを合わせて更新してください。"
            );
        }
        if (rootType.Contains("OutOfMemory"))
        {
            return (
                "メモリ不足で落ちています。特定MODの問題ではない可能性があります。",
                "・割り当てメモリを増やしてください(JVM引数 -Xmx)。"
            );
        }

        // 該当なし: 一般的な案内
        return (
            $"「{id}」の処理中に実行時エラーが発生しました。",
            $"・「{id}」を最新版に更新してみてください。\n" +
            $"     ・直らなければ「{id}」を一旦外して起動できるか確認してください。"
        );
    }

}
