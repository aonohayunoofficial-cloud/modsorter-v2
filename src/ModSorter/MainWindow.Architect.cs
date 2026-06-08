using System.Linq;
using System.Text;
using System.Windows;
using ModSorter.Architect;
using ModSorter.Architect.Generation;
using System.Threading.Tasks;

namespace ModSorter;

public partial class MainWindow
{
    // 建築モードのリソースは初回起動までロードしない（仕様書 第1部 ★徹底）
    private ArchitectModeHost? _architectHost;

    private async void NavArchitect_Click(object sender, RoutedEventArgs e)
    {
        // ここで初めて生成（遅延起動）
        bool firstLaunch = _architectHost == null;
        _architectHost ??= new ArchitectModeHost();
        MainTabs.SelectedIndex = 4;
        Log("建築モードを起動しました（最小実験）。");

        // 初回起動時にモデル一覧をロード（タブを開いた後なので遅延方針に反しない）
        if (firstLaunch)
            await LoadArchModelsAsync();
    }

    private async void ArchModelRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadArchModelsAsync();
    }

    private async Task LoadArchModelsAsync()
    {
        if (_architectHost == null) return;

        ArchModelCombo.IsEnabled = false;
        string? previous = ArchModelCombo.SelectedItem as string;

        var models = await _architectHost.Generation.ListModelsAsync();
        ArchModelCombo.ItemsSource = models;
        ArchModelCombo.IsEnabled = true;

        if (models.Count == 0)
        {
            ArchStatus.Text = "モデル一覧を取得できません（Ollama未起動の可能性）。";
            return;
        }

        // 直前の選択を維持。なければ先頭を選ぶ。
        if (previous != null && models.Contains(previous))
            ArchModelCombo.SelectedItem = previous;
        else
            ArchModelCombo.SelectedIndex = 0;

        ArchStatus.Text = $"モデル {models.Count} 件を取得しました。";
    }


    private async void ArchGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_architectHost == null)
        {
            ArchStatus.Text = "建築モードが未起動です。";
            return;
        }

        string model = (ArchModelCombo.SelectedItem as string ?? "").Trim();
        string prompt = ArchPromptBox.Text.Trim();

        var blocks = ArchBlocksBox.Text
            .Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (string.IsNullOrEmpty(model)) { ArchStatus.Text = "モデル名が空です。"; return; }
        if (string.IsNullOrEmpty(prompt)) { ArchStatus.Text = "指示が空です。"; return; }
        if (blocks.Count == 0) { ArchStatus.Text = "使用可能ブロックが空です。"; return; }

        ArchGenBtn.IsEnabled = false;
        ArchStatus.Text = "生成中...（モデルが重い場合は時間がかかります）";
        ArchResultBox.Text = "";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _architectHost.Generation.GenerateAsync(model, prompt, blocks);
        sw.Stop();

        ArchGenBtn.IsEnabled = true;

        var sb = new StringBuilder();
        sb.AppendLine($"[所要 {sw.Elapsed.TotalSeconds:F1} 秒]");
        sb.AppendLine();

        if (result.Blocks == null)
        {
            ArchStatus.Text = $"失敗: {result.Error}";
            sb.AppendLine("=== 生出力 ===");
            sb.AppendLine(result.RawResponse ?? "(なし)");
        }
        else
        {
            // 候補外IDの混入チェック（捏造防止の確認用）
            var allowed = new System.Collections.Generic.HashSet<string>(
                blocks, System.StringComparer.OrdinalIgnoreCase);
            var invalid = result.Blocks
                .Where(b => !allowed.Contains(b.Id))
                .Select(b => b.Id).Distinct().ToList();

            ArchStatus.Text =
                $"成功: {result.Blocks.Count} ブロック" +
                (invalid.Count > 0 ? $" / 候補外ID {invalid.Count}種 混入" : " / 候補外なし");

            sb.AppendLine($"=== パース結果: {result.Blocks.Count} ブロック ===");
            if (invalid.Count > 0)
                sb.AppendLine($"[警告] 候補外ID: {string.Join(", ", invalid)}");
            sb.AppendLine();
            foreach (var b in result.Blocks)
                sb.AppendLine($"({b.X},{b.Y},{b.Z}) {b.Id}");
            sb.AppendLine();
            sb.AppendLine("=== 生出力 ===");
            sb.AppendLine(result.RawResponse ?? "(なし)");
        }

        ArchResultBox.Text = sb.ToString();
        Log($"建築生成テスト: {ArchStatus.Text}");
    }
}
