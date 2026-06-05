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
        var dir = Path.Combine(_instancePath, "crash-reports");
        if (!Directory.Exists(dir))
        {
            Log("crash-reports\\ が見つかりません。");
            return;
        }
        foreach (var f in Directory.GetFiles(dir, "*.txt")
                                   .OrderByDescending(File.GetLastWriteTime))
        {
            CrashFileList.Items.Add(new CrashFileItem
            {
                FullPath = f,
                Display = $"{File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}  {Path.GetFileName(f)}"
            });
        }
        Log($"{CrashFileList.Items.Count} 件のクラッシュレポートを検出しました。");
    }

    private void CrashFile_Selected(object sender, SelectionChangedEventArgs e)
    {
    }

    private void AnalyzeCrash_Click(object sender, RoutedEventArgs e)
    {
        if (CrashFileList.SelectedItem is not CrashFileItem item)
        {
            MessageBox.Show("レポートを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = CrashAnalyzer.Analyze(item.FullPath);

        // 中央サマリ
        if (!result.ParsedAsModLoading)
        {
            CrashSummary.Text = "MODロード失敗形式として解析できませんでした。" +
                "別の種類のクラッシュの可能性があります。";
        }
        else
        {
            string env = "";
            if (!string.IsNullOrEmpty(result.MinecraftVersion))
                env = $"Minecraft {result.MinecraftVersion}";
            if (!string.IsNullOrEmpty(result.Loader))
                env += (env.Length > 0 ? " / " : "") + result.Loader;

            string missing = result.MissingDependencies.Count > 0
                ? $"\n不足している前提MOD: {string.Join(", ", result.MissingDependencies)}"
                : "";

            CrashSummary.Text =
                $"{env}\nMODロード失敗: {result.Issues.Count} 件{missing}";
        }

        // 中央リストに issue をバインド
        CrashIssueList.ItemsSource = result.Issues;

        // 右詳細は一旦クリア
        ClearCrashDetail();

        Log($"クラッシュ解析: {Path.GetFileName(item.FullPath)} → " +
            $"ロード失敗 {result.Issues.Count} 件");
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

            _crashCfUrl = mod.CurseForgeUrl;
            _crashMrUrl = mod.ModrinthUrl;
            CrashUrlCurseForge.Text = string.IsNullOrEmpty(mod.CurseForgeUrl)
                ? "CurseForge: ―" : $"CurseForge: {mod.CurseForgeUrl}";
            CrashUrlModrinth.Text = string.IsNullOrEmpty(mod.ModrinthUrl)
                ? "Modrinth: ―" : $"Modrinth: {mod.ModrinthUrl}";

            // 削除ボタンは「mods内に実ファイルがある」場合のみ有効化(第3段階で実処理)
            CrashDeleteBtn.IsEnabled = true;
            CrashDeleteBtn.Tag = mod;   // 削除対象を保持
        }
        else
        {
            // mods に無い or 未スキャン
            CrashDetailName.Text = issue.ModId;
            CrashDetailId.Text = "(スキャン済みのMODではないため、MOD詳細は表示できません)";
            CrashDetailVersion.Text = "";
            CrashDetailLoader.Text = "";
            CrashDetailCategory.Text = "";

            _crashCfUrl = "";
            _crashMrUrl = "";
            CrashUrlCurseForge.Text = "CurseForge: ―";
            CrashUrlModrinth.Text = "Modrinth: ―";

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

    // 第3段階で実装。今は何もしない
    private void CrashDelete_Click(object sender, RoutedEventArgs e)
    {
    }

}