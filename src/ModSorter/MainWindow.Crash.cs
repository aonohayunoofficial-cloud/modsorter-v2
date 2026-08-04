using ModSorter.Models;
using ModSorter.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ModSorter;

public partial class MainWindow : Window
{
    // ===== クラッシュレポート =====
    private void LoadCrashFiles()
    {
        CrashFileList.Items.Clear();
        if (string.IsNullOrEmpty(_instancePath)) return;

        int crashCount = 0, logCount = 0;

        // クラッシュレポート(crash-reports\*.txt)
        var crashDir = Path.Combine(_instancePath, "crash-reports");
        if (Directory.Exists(crashDir))
        {
            foreach (var f in Directory.GetFiles(crashDir, "*.txt")
                                       .OrderByDescending(File.GetLastWriteTime))
            {
                CrashFileList.Items.Add(new CrashFileItem
                {
                    FullPath = f,
                    Display = $"[レポート] {File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}  " +
                              $"{Path.GetFileName(f)}"
                });
                crashCount++;
            }
        }
        else
        {
            Log("crash-reports\\ が見つかりません。");
        }

        // 実行ログ(logs\*.log / *.log.gz)。latest.log を必ず先頭に置き、古い分は30件まで。
        var logDir = Path.Combine(_instancePath, "logs");
        if (Directory.Exists(logDir))
        {
            var logs = Directory.GetFiles(logDir, "*.log")
                .Concat(Directory.GetFiles(logDir, "*.log.gz"))
                .OrderByDescending(f => Path.GetFileName(f)
                    .Equals("latest.log", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTime)
                .Take(30);

            foreach (var f in logs)
            {
                CrashFileList.Items.Add(new CrashFileItem
                {
                    FullPath = f,
                    Display = $"[ログ] {File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}  " +
                              $"{Path.GetFileName(f)}"
                });
                logCount++;
            }
        }
        else
        {
            Log("logs\\ が見つかりません。");
        }

        Log($"クラッシュレポート {crashCount} 件 / 実行ログ {logCount} 件を検出しました。");
    }

    private void CrashFile_Selected(object sender, SelectionChangedEventArgs e)
    {
    }

    private void AnalyzeCrash_Click(object sender, RoutedEventArgs e)
    {
        if (CrashFileList.SelectedItem is not CrashFileItem item)
        {
            MessageBox.Show("レポートまたはログを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 拡張子で解析器を振り分ける。*.log / *.log.gz は実行ログ、それ以外はレポート。
        bool isLog =
            item.FullPath.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
            item.FullPath.EndsWith(".log.gz", StringComparison.OrdinalIgnoreCase);

        var result = isLog
            ? LogAnalyzer.Analyze(item.FullPath)
            : CrashAnalyzer.Analyze(item.FullPath);

        // ログ解析は jar 名を持たないので、スキャン済みの _mods から ModId で補う。
        // これで右詳細のリンクと削除ボタンがレポート解析と同じように使える。
        if (isLog)
        {
            foreach (var issue in result.Issues)
            {
                if (!string.IsNullOrEmpty(issue.ModFile)) continue;
                if (string.IsNullOrEmpty(issue.ModId)) continue;
                var hit = _mods.FirstOrDefault(m =>
                    string.Equals(m.ModId, issue.ModId, StringComparison.OrdinalIgnoreCase));
                if (hit != null) issue.ModFile = hit.FileName;
            }
        }

        string env = "";
        if (!string.IsNullOrEmpty(result.MinecraftVersion))
            env = $"Minecraft {result.MinecraftVersion}";
        if (!string.IsNullOrEmpty(result.Loader))
            env += (env.Length > 0 ? " / " : "") + result.Loader;

        string missingText = result.MissingDependencies.Count > 0
            ? $"\n不足している前提MOD: {string.Join(", ", result.MissingDependencies)}"
            : "";

        // 中央サマリ
        if (isLog)
        {
            string head = env.Length > 0 ? env + "\n" : "";
            string tail = string.IsNullOrEmpty(result.Description)
                ? "" : $"\n{result.Description}";
            CrashSummary.Text = result.Issues.Count == 0
                ? head + "実行ログから問題は検出されませんでした。" + tail
                : head + $"実行ログの解析: {result.Issues.Count} 件{missingText}" + tail;
        }
        else if (!result.ParsedAsModLoading)
        {
            CrashSummary.Text = "MODロード失敗形式として解析できませんでした。" +
                "別の種類のクラッシュの可能性があります。";
        }
        else
        {
            CrashSummary.Text =
                $"{env}\nMODロード失敗: {result.Issues.Count} 件{missingText}";
        }

        // 中央リストに issue をバインド
        CrashIssueList.ItemsSource = result.Issues;

        // 右詳細は一旦クリア
        ClearCrashDetail();

        Log($"{(isLog ? "ログ解析" : "クラッシュ解析")}: {Path.GetFileName(item.FullPath)} → " +
            $"{result.Issues.Count} 件");
    }

    // クラッシュ詳細パネルで現在表示中のMODのURL
    private string _crashCfUrl = "";
    private string _crashMrUrl = "";

    // 中央 issue リストの選択 → 右詳細パネルに表示
    private void CrashIssueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CrashIssueList.SelectedItem is not CrashAnalyzer.Issue issue)
        {
            ClearCrashDetail();
            return;
        }

        // クラッシュ原因(日本語要約)
        CrashDetailReason.Text = issue.JapaneseSummary;
        CrashDetailFile.Text = string.IsNullOrEmpty(issue.ModFile)
            ? "" : $"jar: {issue.ModFile}";

        // スキャン済みの _mods から、jarファイル名で該当MODを探す
        ModEntry? mod = null;
        if (!string.IsNullOrEmpty(issue.ModFile))
        {
            mod = _mods.FirstOrDefault(m =>
                string.Equals(m.FileName, issue.ModFile,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (mod != null)
        {
            CrashDetailName.Text = mod.DisplayName;
            CrashDetailId.Text = $"ID: {mod.ModId}";
            CrashDetailVersion.Text = $"バージョン: {mod.Version}";
            CrashDetailLoader.Text = $"ローダー: {mod.Loader}";
            CrashDetailCategory.Text = mod.Categories.Count == 0
                ? "カテゴリ: ―"
                : $"カテゴリ ({mod.CategorySource}): {mod.CategoryText}";

            // 直リンクがあればそれを、無ければ名前で検索ページにフォールバック
            string cfQuery = string.IsNullOrEmpty(mod.DisplayName) ? mod.ModId : mod.DisplayName;
            if (!string.IsNullOrEmpty(mod.CurseForgeUrl))
            {
                _crashCfUrl = mod.CurseForgeUrl;
                CrashUrlCurseForge.Text = $"CurseForge: {mod.CurseForgeUrl}";
            }
            else
            {
                _crashCfUrl = BuildCfSearchUrl(cfQuery);
                CrashUrlCurseForge.Text = $"CurseForge で検索: {cfQuery}";
            }
            if (!string.IsNullOrEmpty(mod.ModrinthUrl))
            {
                _crashMrUrl = mod.ModrinthUrl;
                CrashUrlModrinth.Text = $"Modrinth: {mod.ModrinthUrl}";
            }
            else
            {
                _crashMrUrl = BuildMrSearchUrl(cfQuery);
                CrashUrlModrinth.Text = $"Modrinth で検索: {cfQuery}";
            }

            // 削除ボタンは「mods内に実ファイルがある」場合のみ有効化(第3段階で実処理)
            CrashDeleteBtn.IsEnabled = true;
            CrashDeleteBtn.Tag = mod;   // 削除対象を保持
        }
        else
        {
            // mods に無い or 未スキャン: ModId を検索キーにしてフォールバック
            CrashDetailName.Text = issue.ModId;
            CrashDetailId.Text = "(未スキャン: 名前で検索ページを開けます)";
            CrashDetailVersion.Text = "";
            CrashDetailLoader.Text = "";
            CrashDetailCategory.Text = "";

            string q = issue.ModId;
            if (string.IsNullOrEmpty(q) || q == "(特定不可)")
            {
                _crashCfUrl = "";
                _crashMrUrl = "";
                CrashUrlCurseForge.Text = "CurseForge: ―";
                CrashUrlModrinth.Text = "Modrinth: ―";
            }
            else
            {
                _crashCfUrl = BuildCfSearchUrl(q);
                _crashMrUrl = BuildMrSearchUrl(q);
                CrashUrlCurseForge.Text = $"CurseForge で検索: {q}";
                CrashUrlModrinth.Text = $"Modrinth で検索: {q}";
            }

            CrashDeleteBtn.IsEnabled = false;
            CrashDeleteBtn.Tag = null;
        }

    }

    private void ClearCrashDetail()
    {
        CrashDetailReason.Text = "(中央のリストから問題を選択)";
        CrashDetailName.Text = "―";
        CrashDetailId.Text = "";
        CrashDetailVersion.Text = "";
        CrashDetailLoader.Text = "";
        CrashDetailCategory.Text = "";
        CrashDetailFile.Text = "";
        _crashCfUrl = "";
        _crashMrUrl = "";
        CrashUrlCurseForge.Text = "CurseForge: ―";
        CrashUrlModrinth.Text = "Modrinth: ―";
        CrashDeleteBtn.IsEnabled = false;
        CrashDeleteBtn.Tag = null;
    }

    // MOD名/IDから検索ページURLを作る(直リンクが無いときのフォールバック)
    private static string BuildCfSearchUrl(string query)
        => "https://www.curseforge.com/minecraft/search?search=" +
           Uri.EscapeDataString(query);

    private static string BuildMrSearchUrl(string query)
        => "https://modrinth.com/mods?q=" + Uri.EscapeDataString(query);

    private void CrashUrlCf_Click(object sender, MouseButtonEventArgs e)
    {
        if (_crashCfUrl.StartsWith("http"))
            Process.Start(new ProcessStartInfo(_crashCfUrl) { UseShellExecute = true });
    }

    private void CrashUrlMr_Click(object sender, MouseButtonEventArgs e)
    {
        if (_crashMrUrl.StartsWith("http"))
            Process.Start(new ProcessStartInfo(_crashMrUrl) { UseShellExecute = true });
    }

    private void CrashDelete_Click(object sender, RoutedEventArgs e)
    {
        if (CrashDeleteBtn.Tag is not ModEntry mod)
        {
            MessageBox.Show("削除対象のMODが特定できません。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(mod.FilePath) || !File.Exists(mod.FilePath))
        {
            MessageBox.Show(
                $"ファイルが見つかりません。\n{mod.FilePath}",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"次のMODのjarファイルをごみ箱に移動します。\n\n" +
            $"MOD: {mod.DisplayName}\nID: {mod.ModId}\n" +
            $"ファイル: {mod.FileName}\n\nよろしいですか?",
            "ModSorter - MOD削除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            // ごみ箱へ送る(復元可能)
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                mod.FilePath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"削除に失敗しました。\n{ex.Message}", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Log($"MOD削除失敗: {mod.FileName} ({ex.Message})");
            return;
        }

        Log($"MOD削除(ごみ箱): {mod.DisplayName} [{mod.ModId}] {mod.FileName}");

        // 履歴に記録(第4で実装する Activity に積む)
        AddActivity($"MOD削除: {mod.DisplayName} (ID: {mod.ModId})");

        // _mods から除き、Tab1 の表示も更新
        _mods.Remove(mod);
        RefreshModViews();

        // 中央 issue リストから、この jar に対応する issue を消す
        if (CrashIssueList.ItemsSource is IEnumerable<CrashAnalyzer.Issue> issues)
        {
            var remaining = issues
                .Where(i => !string.Equals(i.ModFile, mod.FileName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            CrashIssueList.ItemsSource = remaining;
        }

        // 右詳細をクリア
        ClearCrashDetail();
    }


}