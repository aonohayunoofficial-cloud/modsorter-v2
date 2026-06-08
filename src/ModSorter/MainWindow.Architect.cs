using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ModSorter.Architect;
using ModSorter.Architect.Generation;
using ModSorter.Architect.Preview;
using System.Collections.Generic;


namespace ModSorter;

public partial class MainWindow
{
    // 建築モードのリソースは初回起動までロードしない（仕様書 第1部 ★徹底）
    private ArchitectModeHost? _architectHost;

    private PreviewWindow? _previewWindow;
    // 直近に生成した3案を保持（案切り替え用）
    private List<GenerationResult>? _archCases;

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

    // 3Dプレビューを別ウィンドウで開く（既に開いていれば前面に出す）
    private async void ArchOpenPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_previewWindow == null)
        {
            _previewWindow = new PreviewWindow { Owner = this };
            // 閉じられたら参照をクリアして再生成できるようにする
            _previewWindow.Closed += (_, __) => _previewWindow = null;
            _previewWindow.Show();
            await _previewWindow.InitAsync();
            Log("3Dプレビューウィンドウを開きました。");
        }
        else
        {
            _previewWindow.Activate(); // 既に開いていれば前面へ
        }
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
        SetCaseButtonsEnabled(false);
        ArchStatus.Text = "3案を生成中...（少し時間がかかります）";
        ArchResultBox.Text = "";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _archCases = await _architectHost.Generation.GenerateMultipleAsync(model, prompt, blocks, 3);
        sw.Stop();

        ArchGenBtn.IsEnabled = true;

        // 各案ボタンを成否に応じて有効化＋ブロック数をラベルに反映
        var caseButtons = new[] { ArchCase1Btn, ArchCase2Btn, ArchCase3Btn };
        int okCount = 0;
        for (int i = 0; i < caseButtons.Length; i++)
        {
            if (i < _archCases.Count && _archCases[i].Blocks != null)
            {
                caseButtons[i].IsEnabled = true;
                caseButtons[i].Content = $"案{i + 1} ({_archCases[i].Blocks!.Count})";
                okCount++;
            }
            else
            {
                caseButtons[i].IsEnabled = false;
                caseButtons[i].Content = $"案{i + 1} (失敗)";
            }
        }

        ArchStatus.Text = $"[所要 {sw.Elapsed.TotalSeconds:F1} 秒] 成功 {okCount}/3 案。" +
                          (okCount > 0 ? "案ボタンで切り替えて表示します。" : "全案が失敗しました。");

        // 最初の成功案を自動表示
        int firstOk = _archCases.FindIndex(r => r.Blocks != null);
        if (firstOk >= 0)
            await ShowCase(firstOk);
        else
            ArchResultBox.Text = "全案が失敗しました。\n" +
                string.Join("\n", _archCases.Select((r, i) => $"案{i + 1}: {r.Error}"));

        Log($"建築3案生成: {ArchStatus.Text}");
    }

    private void SetCaseButtonsEnabled(bool enabled)
    {
        ArchCase1Btn.IsEnabled = enabled;
        ArchCase2Btn.IsEnabled = enabled;
        ArchCase3Btn.IsEnabled = enabled;
    }

    // 案ボタンが押されたとき
    private async void ArchCase_Click(object sender, RoutedEventArgs e)
    {
        if (_archCases == null) return;
        if (sender is FrameworkElement fe && fe.Tag is string tagStr
            && int.TryParse(tagStr, out int index))
        {
            await ShowCase(index);
        }
    }

    // 指定インデックスの案を結果テキストとプレビューに表示する
    private async Task ShowCase(int index)
    {
        if (_archCases == null || index < 0 || index >= _archCases.Count) return;
        var result = _archCases[index];

        var sb = new StringBuilder();
        sb.AppendLine($"=== 案{index + 1} ===");
        if (result.Blocks == null)
        {
            sb.AppendLine($"この案は失敗: {result.Error}");
            ArchResultBox.Text = sb.ToString();
            return;
        }

        sb.AppendLine($"パース結果: {result.Blocks.Count} ブロック");
        sb.AppendLine();
        sb.AppendLine("=== 生出力(スペック) ===");
        sb.AppendLine(result.RawResponse ?? "(なし)");
        ArchResultBox.Text = sb.ToString();

        await RenderArchPreviewAsync(result.Blocks);
    }

    // 生成結果を別ウィンドウの 3Dプレビューへ描画する。
    // ウィンドウが未オープンなら自動で開いて描画する。
    private async Task RenderArchPreviewAsync(System.Collections.Generic.List<GeneratedBlock> blocks)
    {
        // プレビューウィンドウが無ければ開く
        if (_previewWindow == null)
        {
            _previewWindow = new PreviewWindow { Owner = this };
            _previewWindow.Closed += (_, __) => _previewWindow = null;
            _previewWindow.Show();
            await _previewWindow.InitAsync();
        }

        if (!_previewWindow.IsReady) return;

        string json = JsonSerializer.Serialize(blocks.Select(b => new
        {
            x = b.X,
            y = b.Y,
            z = b.Z,
            id = b.Id
        }));
        await _previewWindow.RenderAsync(json);
        _previewWindow.Activate(); // 結果が見えるよう前面へ
    }

}
