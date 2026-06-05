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

        // 中央リスト表示用のラベル
        public string KindLabel => Kind switch
        {
            IssueKind.MissingDependency => "[前提MOD不足]",
            IssueKind.VersionMismatch => "[バージョン不一致]",
            IssueKind.Incompatible => "[競合]",
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

        return result;
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
}
