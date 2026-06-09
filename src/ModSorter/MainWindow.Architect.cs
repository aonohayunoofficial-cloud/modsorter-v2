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
    // 読み込んだジャンル一覧と、現在選択中のジャンル
    private List<Genre>? _genres;
    private Genre? _currentGenre;

    private async void NavArchitect_Click(object sender, RoutedEventArgs e)
    {
        // ここで初めて生成（遅延起動）
        bool firstLaunch = _architectHost == null;
        _architectHost ??= new ArchitectModeHost();
        MainTabs.SelectedIndex = 4;
        Log("建築モードを起動しました（最小実験）。");

        // 初回起動時にモデル一覧とジャンルをロード
        if (firstLaunch)
        {
            await LoadArchModelsAsync();
            LoadArchGenres();
        }
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
    private void LoadArchGenres()
    {
        _genres = GenreCatalog.Load();
        ArchGenreCombo.ItemsSource = _genres;

        if (_genres.Count == 0)
        {
            ArchStatus.Text = "ジャンルが読み込めませんでした。" +
                (string.IsNullOrEmpty(GenreCatalog.LastError) ? "" : GenreCatalog.LastError);
            return;
        }
        ArchGenreCombo.SelectedIndex = 0; // 先頭ジャンルを選択（→ブロック欄も自動入力）
    }

    // ブロック選択ウィンドウを開き、決定したら ArchBlocksBox に書き戻す。
    private void ArchPickBlocks_Click(object sender, RoutedEventArgs e)
    {
        var current = ArchBlocksBox.Text
            .Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        var win = new BlockPickerWindow(current) { Owner = this };
        bool? ok = win.ShowDialog();
        if (ok == true && win.ResultCsv != null)
        {
            ArchBlocksBox.Text = win.ResultCsv;
            UpdateBlocksSummary();
            ArchStatus.Text = "ブロック選択を反映しました。";
        }
    }
    // 使用可能ブロックの件数サマリを更新する（隠した欄の中身を要約表示）。
    private void UpdateBlocksSummary()
    {
        var ids = ArchBlocksBox.Text
            .Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        ArchBlocksSummary.Text = ids.Count == 0
            ? "(未選択)"
            : $"{ids.Count} 種類を選択中";
    }
    private void ArchGenre_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _currentGenre = ArchGenreCombo.SelectedItem as Genre;
        if (_currentGenre == null) return;

        // ブロック欄を、このジャンルのブロックで自動入力（日本語名つきの参考表示も）
        // 実際にモデルへ渡すのは ID。欄にはIDをカンマ区切りで入れる。
        ArchBlocksBox.Text = string.Join(", ", _currentGenre.Blocks.Select(b => b.Id));
        UpdateBlocksSummary();

        // 分かりやすいように、ID→日本語名の対応をステータスに出す
        var pairs = _currentGenre.Blocks
            .Select(b => $"{b.Name}({b.Id})");
        ArchStatus.Text = $"ジャンル「{_currentGenre.DisplayName}」: " + string.Join(" / ", pairs);
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

        // 寸法欄を読み取る（数値でなければエラー）
        if (!int.TryParse(ArchWidthBox.Text.Trim(), out int w) ||
            !int.TryParse(ArchDepthBox.Text.Trim(), out int d) ||
            !int.TryParse(ArchHeightBox.Text.Trim(), out int h))
        {
            ArchStatus.Text = "幅・奥行・高さは数値で入力してください。";
            return;
        }
        if (w < 2 || d < 2 || h < 2 || w > 64 || d > 64 || h > 64)
        {
            ArchStatus.Text = "幅・奥行・高さは 2〜64 の範囲で入力してください。";
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        // 種類で経路を分岐。0=建築(家)、1=プリミティブ(曲面)。
        bool isPrimitive = ArchKindCombo.SelectedIndex == 1;
        if (isPrimitive)
        {
            // サイズ欄(直径)を半径に変換。半径 = 直径/2（最低1）。
            int rx = System.Math.Max(1, w / 2);
            int ry = System.Math.Max(1, h / 2); // 高さ→y半径
            int rz = System.Math.Max(1, d / 2); // 奥行→z半径
            _archCases = await _architectHost.Generation.GeneratePrimitiveMultipleAsync(
                model, prompt, blocks, 3, rx, ry, rz);
        }
        else
        {
            string? style = _currentGenre?.StylePrompt;
            // 正面の向き（ファサード神殿用）。選択中の ComboBoxItem の Tag を取り出す。
            string facade = "south";
            if (ArchFacadeCombo.SelectedItem is System.Windows.Controls.ComboBoxItem fi
                && fi.Tag is string ftag && !string.IsNullOrWhiteSpace(ftag))
                facade = ftag;
            _archCases = await _architectHost.Generation.GenerateMultipleAsync(
                model, prompt, blocks, 3, style, w, d, h, facade);
        }

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
