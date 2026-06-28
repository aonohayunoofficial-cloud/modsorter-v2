using ModSorter.Architect;
using ModSorter.Architect.Generation;
using ModSorter.Architect.Preview;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


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
    // 現在プレビュー/結果に表示している案のインデックス（-1 = 未表示）。出力対象に使う。
    private int _archShownCaseIndex = -1;

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

        // 定型ワード欄を、このジャンルの style_prompt で自動入力する。
        // style_prompt が空のジャンルなら欄は変更しない（ユーザーの入力を消さない）。
        // ArchFixedWordsBox が未生成のタイミング(起動直後など)に備えて null チェック。
        if (ArchFixedWordsBox != null &&
            !string.IsNullOrWhiteSpace(_currentGenre.StylePrompt))
        {
            ArchFixedWordsBox.Text = _currentGenre.StylePrompt.Trim();
        }

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

        // 種類が 彫刻(テキスト→画像→GLB) (index=2) なら、専用フローへ。
        // LLM(Ollama)は使わないため、以降の model/prompt/blocks チェックは通さない。
        if (ArchKindCombo.SelectedIndex == 2)
        {
            ArchGenBtn.IsEnabled = false;
            SetCaseButtonsEnabled(false);
            try
            {
                await GenerateSculptureAsync();
            }
            finally
            {
                ArchGenBtn.IsEnabled = true;
            }
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
            ArchGenBtn.IsEnabled = true;
            return;
        }
        if (w < 2 || d < 2 || h < 2 || w > 64 || d > 64 || h > 64)
        {
            ArchStatus.Text = "幅・奥行・高さは 2〜64 の範囲で入力してください。";
            ArchGenBtn.IsEnabled = true;
            return;
        }

        // LLM生成は所要時間が読めないので不定進捗(グルグル)で表示する。
        ProgressShow("3案を生成中...（少し時間がかかります）", indeterminate: true);

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

        // LLM生成が終わったので進捗バーを隠す。
        ProgressHide();

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

    // 文字列にひらがな・カタカナ・漢字が含まれるか判定する。
    private static bool ContainsJapanese(string s)
    {
        foreach (char c in s)
        {
            // ひらがな(3040-309F) / カタカナ(30A0-30FF) / 漢字(4E00-9FFF)
            if ((c >= '\u3040' && c <= '\u309F') ||
                (c >= '\u30A0' && c <= '\u30FF') ||
                (c >= '\u4E00' && c <= '\u9FFF'))
                return true;
        }
        return false;
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
            // 失敗案を表示中は出力できない。
            _archShownCaseIndex = -1;
            if (ArchExportBtn != null) ArchExportBtn.IsEnabled = false;
            return;
        }

        sb.AppendLine($"パース結果: {result.Blocks.Count} ブロック");
        sb.AppendLine();
        sb.AppendLine("=== 生出力(スペック) ===");
        sb.AppendLine(result.RawResponse ?? "(なし)");
        ArchResultBox.Text = sb.ToString();

        // 表示中の案を記録し、schematics 出力ボタンを有効化する。
        _archShownCaseIndex = index;
        if (ArchExportBtn != null) ArchExportBtn.IsEnabled = true;

        await RenderArchPreviewAsync(result.Blocks);
    }

    // 表示中の案を schematics に NBT 出力する。
    // 出力先決定は機械側と共通の ResolveSchematicOutPath に委譲する（二重実装回避）。
    // 建築の GeneratedBlock は座標+IDのみで向き状態を持たないため、Properties は空で書き出す。
    private void ArchExport_Click(object sender, RoutedEventArgs e)
    {
        if (_archCases == null || _archShownCaseIndex < 0 ||
            _archShownCaseIndex >= _archCases.Count)
        {
            ArchStatus.Text = "出力できる案がありません。先に生成して案を表示してください。";
            return;
        }

        var blocks = _archCases[_archShownCaseIndex].Blocks;
        if (blocks == null || blocks.Count == 0)
        {
            ArchStatus.Text = "表示中の案にブロックがありません。";
            return;
        }

        // 出力先を決定。.minecraft/schematics に <名前>.nbt で保存する。
        // instancePath 未設定や保存不可時は diagnostics へフォールバックする。
        string outPath = ResolveSchematicOutPath(ArchNameBox?.Text ?? "", "building");
        if (outPath.Length == 0)
        {
            // ユーザーが上書きを拒否した場合は保存を中止する。
            ArchStatus.Text = "出力をキャンセルしました（同名上書き拒否）。";
            Log("建築データの保存をキャンセルしました（同名上書き拒否）。");
            return;
        }

        // GeneratedBlock → StructureNbtWriter.Block へ詰め替え（向き状態なし）。
        var nbtBlocks = blocks.Select(b =>
            new ModSorter.Architect.Generation.StructureNbtWriter.Block
            {
                Name = b.Id,
                X = b.X,
                Y = b.Y,
                Z = b.Z
            }).ToList();

        try
        {
            ModSorter.Architect.Generation.StructureNbtWriter.Save(nbtBlocks, outPath);
            ArchStatus.Text = $"案{_archShownCaseIndex + 1} を出力しました（{nbtBlocks.Count} ブロック）: {outPath}";
            Log($"建築データ出力: 案{_archShownCaseIndex + 1} / {nbtBlocks.Count} ブロック / {outPath}");
        }
        catch (Exception ex)
        {
            ArchStatus.Text = $"出力に失敗しました: {ex.Message}";
            Log($"建築データ出力失敗: {ex.Message}");
        }
    }

    // 画像ギャラリーを開き、選ばれた画像をそのまま 3D化する。
    private async void ArchGallery_Click(object sender, RoutedEventArgs e)
    {
        var gallery = new ModSorter.Architect.Preview.GalleryWindow { Owner = this };
        bool? ok = gallery.ShowDialog();
        if (ok != true || gallery.SelectedForSculpt.Count == 0)
            return;

        // 解像度・ブロックIDは現在のUI欄から取得（彫刻フローと同じルール）。
        var blockIds = ArchBlocksBox.Text
            .Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        string blockId = blockIds.Count > 0 ? blockIds[0] : "minecraft:stone";

        int resolution = GetSculptResolution();

        await RunSculptureFromImagesAsync(gallery.SelectedForSculpt, resolution, blockId);
    }

    // モード切り替えで、サイズ3欄(建築/プリミティブ)と解像度コンボ(生成AI)を出し分ける。
    private void ArchKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 起動直後など、まだ要素が出来ていないことがあるので null チェック。
        if (ArchSizePanel == null || ArchResolutionPanel == null) return;

        bool isAi = ArchKindCombo.SelectedIndex == 2; // 2 = 生成AI（GLBから）
        ArchSizePanel.Visibility = isAi ? Visibility.Collapsed : Visibility.Visible;
        ArchResolutionPanel.Visibility = isAi ? Visibility.Visible : Visibility.Collapsed;
    }

    // 生成AIモードで選ばれている解像度を返す。コンボのTagから取得、失敗時は48。
    private int GetSculptResolution()
    {
        if (ArchResolutionCombo?.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag && int.TryParse(tag, out int res))
        {
            return System.Math.Clamp(res, 8, 128);
        }
        return 48;
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

        // プレビューの準備ができていなければ、できるまで少し待つ（最大5秒）。
        // TRELLIS.2 実行を挟むとタイミング次第で未初期化のことがあるため。
        int waited = 0;
        while (!_previewWindow.IsReady && waited < 5000)
        {
            await Task.Delay(100);
            waited += 100;
        }
        if (!_previewWindow.IsReady)
        {
            Log("プレビュー描画中止: ウィンドウが準備完了になりませんでした。");
            ArchResultBox.AppendText("\n[警告] プレビュー未初期化のため描画をスキップ。\n");
            return;
        }

        string json = JsonSerializer.Serialize(blocks.Select(b => new
        {
            x = b.X,
            y = b.Y,
            z = b.Z,
            id = b.Id
        }));

        // この案で使われているブロックの実テクスチャ(PNG)を集めて、
        // base64データURIの辞書にしてプレビューへ先に渡す。
        // baseId(状態[...]を除いたID)単位で1枚あればよいので重複排除する。
        try
        {
            var texMap = BuildTextureMap(blocks);
            string texJson = JsonSerializer.Serialize(texMap);
            Log($"プレビュー用テクスチャ: {texMap.Count} 種類を送信します。");
            await _previewWindow.SetTexturesAsync(texJson);
        }
        catch (Exception ex)
        {
            // テクスチャ取得に失敗してもプレビュー自体は単色で続行する。
            Log($"テクスチャ取得をスキップ: {ex.Message}");
        }

        Log($"プレビュー描画: {blocks.Count} ブロックを送信します。");
        await _previewWindow.RenderAsync(json);
        _previewWindow.Activate(); // 結果が見えるよう前面へ
    }

    // 描画するブロック群から、使用ブロックの実テクスチャを集める。
    // 戻り値: baseId(例 "minecraft:oak_planks") → "data:image/png;base64,...."
    private Dictionary<string, string> BuildTextureMap(List<GeneratedBlock> blocks)
    {
        var result = new Dictionary<string, string>();

        // ブロックIDから状態(例 "[facing=north]")を落とした baseId のユニーク集合。
        var baseIds = blocks
            .Select(b => b.Id.Split('[')[0])
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        var vanilla = FindVanillaJar();
        var modJars = (_mods ?? new List<ModSorter.Models.ModEntry>())
            .Select(m => m.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        using var tp = new ModSorter.Architect.Generation.BlockTextureProvider(vanilla, modJars);

        foreach (var id in baseIds)
        {
            var png = tp.GetTexture(id);
            if (png != null && png.Length > 0)
                result[id] = "data:image/png;base64," + System.Convert.ToBase64String(png);
        }

        return result;
    }

    // 彫刻モード(一気通貫): プロンプト → ComfyUIで画像を複数生成 → ユーザーが1枚選択
    // → 選んだ画像だけ TRELLIS.2 で GLB化 → MeshVoxelizerでボクセル化 → プレビュー。
    private async Task GenerateSculptureAsync()
    {
        // 0. 入力チェック。プロンプトとブロックは必要(modelは不要)。
        string prompt = ArchPromptBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            ArchStatus.Text = "指示(プロンプト)が空です。";
            return;
        }

        // 0-A. 日本語が含まれていれば DeepL で英訳する（FLUXは英語が得意）。
        if (ContainsJapanese(prompt))
        {
            string deeplKey = ModSorter.Models.Settings.Decrypt(_settings.DeepLKeyEnc);
            if (string.IsNullOrEmpty(deeplKey))
            {
                ArchStatus.Text =
                    "日本語プロンプトですが DeepL キーが未設定です。設定で保存してください。";
                return;
            }
            if (!ModSorter.Clients.DeepLClient.IsReady)
                ModSorter.Clients.DeepLClient.Init(deeplKey);

            ArchStatus.Text = "プロンプトを英訳中...";
            string? en = await ModSorter.Clients.DeepLClient.TranslateToEnglishAsync(prompt);
            if (string.IsNullOrEmpty(en))
            {
                ArchStatus.Text =
                    $"プロンプトの英訳に失敗: {ModSorter.Clients.DeepLClient.LastError}";
                return;
            }
            Log($"プロンプト英訳: 「{prompt}」→「{en}」");
            prompt = en; // 以降は英訳済みプロンプトを使う
        }

        // 0-B. 定型ワード（UI欄）を末尾に連結する。空なら何もしない。
        //      英訳後に付けることで、定型部分は英語のまま安定して渡る。
        string fixedWords = ArchFixedWordsBox.Text.Trim();
        if (!string.IsNullOrEmpty(fixedWords))
        {
            // プロンプト末尾がカンマやピリオドでなければカンマで区切る。
            string sep = (prompt.EndsWith(",") || prompt.EndsWith(".") ||
                          prompt.Length == 0) ? " " : ", ";
            prompt = prompt + sep + fixedWords;
            Log($"定型ワード連結後: 「{prompt}」");
        }

        // 0-C. 窓トグルに応じて、窓の有無だけを足す。
        //      立体感を殺す flat lighting / minimal shadows はここでは付けない。
        //      (陰影の濁りは MeshVoxelizer 側のガンマ補正で対処済み)
        bool withWindows = ArchWindowToggle.IsChecked == true;
        string windowWords = withWindows
            ? "with large windows"
            : "without windows";
        {
            string sep = (prompt.EndsWith(",") || prompt.EndsWith(".") ||
                          prompt.Length == 0) ? " " : ", ";
            prompt = prompt + sep + windowWords;
            Log($"窓指示({(withWindows ? "あり" : "なし")})連結後: 「{prompt}」");
        }

        var blockIds = ArchBlocksBox.Text
            .Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        string blockId = blockIds.Count > 0 ? blockIds[0] : "minecraft:stone";

        int resolution = GetSculptResolution();

        // 1. 画像選択ウィンドウを開いて画像生成→選択。
        ArchStatus.Text = "画像生成ウィンドウを開きました。画像を選んでください。";
        var picker = new ModSorter.Architect.Preview.ImagePickerWindow(prompt)
        {
            Owner = this
        };
        // 表示後に最初の生成を走らせるため、Loaded で StartAsync を呼ぶ。
        picker.Loaded += async (_, __) => await picker.StartAsync();

        bool? picked = picker.ShowDialog();
        if (picked != true || picker.SelectedImageWslPaths.Count == 0)
        {
            ArchStatus.Text = "画像選択をキャンセルしました。";
            return;
        }
        // 2. 選ばれた画像を共通メソッドへ渡して 3D化→ボクセル化→案表示。
        //    解像度・ブロックID もそのまま渡す。ギャラリーからも同じ経路を使う。
        await RunSculptureFromImagesAsync(picker.SelectedImageWslPaths, resolution, blockId);
    }

    // 選択済み画像(WSL相対パスのリスト)を順に TRELLIS.2 で GLB化 → ボクセル化し、
    // 案として並べて表示する。GenerateSculptureAsync とギャラリーの両方から呼ぶ。
    // wslImagePaths: 例 ["assets/arch_xxx.png", ...]（1枚以上）
    private async Task RunSculptureFromImagesAsync(
        List<string> wslImagePaths, int resolution, string blockId)
    {
        if (wslImagePaths == null || wslImagePaths.Count == 0)
        {
            ArchStatus.Text = "3D化する画像が選ばれていません。";
            return;
        }

        ArchResultBox.Text = "";
        void AppendLog(string line) => Dispatcher.Invoke(() =>
        {
            ArchResultBox.AppendText(line + "\n");
            ArchResultBox.ScrollToEnd();
        });

        // 3D化は長時間処理。所要時間が読めないので不定進捗(グルグル)で表示する。
        ProgressShow("3D化の準備中...", indeterminate: true);
        const string wslGlbUncDir =
            @"\\wsl$\Ubuntu-22.04\home\yuno\projects\TRELLIS.2";

        // 色マッチ用の候補を作る。shape(階段/柵など形状物)は見た目が崩れるので除外。
        // 指定が無い場合は全ブロックから選ぶ（NULL = ALL）。
        // 指定があっても、その全てが代表色を持たない(shape系のみ等)と候補が空になり
        // マッチ不能になるため、その場合だけ安全策で全体にフォールバックする。
        var catColor = ModSorter.Architect.BlockCatalog.Load();
        // 立方体でない形状ブロック(shape: 階段/ハーフ/柵/壁/鉄格子)だけ色マッチから除外。
        // ボクセルに置くと向き/状態で崩れるため。金属やガラス等のフルブロックは候補に残す。
        // 「使いたくない色」はユーザーが指定パレットから外せば自然に除外される。
        var excludeKeys = new HashSet<string> { "shape" };
        var allColorItems = catColor
            .Where(c => !excludeKeys.Contains(c.Key))
            .SelectMany(c => c.Blocks)
            .ToList();

        // ユーザーがUIで指定したブロックだけに候補を絞る。
        // 「この色に一番近いのは、指定されたパレットの中ではこれ」を選べるようにする。
        var pickedIds = ArchBlocksBox.Text
            .Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToHashSet();

        // 指定ブロックのうち、カタログに色情報があるものだけが色マッチに使える。
        var colorItems = allColorItems
            .Where(b => pickedIds.Contains(b.Id))
            .ToList();

        // 指定が無い／指定ブロックが全部色を持たない場合は、候補が空になって
        // マッチ不能になるので、安全策としてカタログ全体にフォールバックする。
        if (colorItems.Count == 0)
        {
            AppendLog("[注意] 指定ブロックに色マッチ可能なものが無いため、カタログ全体から選びます。");
            colorItems = allColorItems;
        }
        else
        {
            AppendLog($"色マッチ候補: 指定ブロックのうち {colorItems.Count} 種類を使用。");
        }

        // 各候補ブロックの代表色を、テクスチャPNGの平均色で上書きする(遅延計算+キャッシュ)。
        // PNGが取れないブロックは、カタログ手入力の color をそのまま使う(フォールバック)。
        try
        {
            var vanilla = FindVanillaJar();
            var modJars = (_mods ?? new List<ModSorter.Models.ModEntry>())
                .Select(m => m.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            using var tp = new ModSorter.Architect.Generation.BlockTextureProvider(vanilla, modJars);

            ProgressShow("ブロックの代表色を計算中...", indeterminate: true);

            int sampled = 0;
            foreach (var item in colorItems)
            {
                var avg = await Task.Run(() =>
                    ModSorter.Architect.Generation.BlockColorSampler.GetAverageColor(tp, item.Id));
                if (avg != null)
                {
                    item.Color = avg;   // テクスチャ平均色で代表色を上書き
                    sampled++;
                }
                // null のときは手入力 color のまま(変更しない)
            }
            AppendLog($"テクスチャ平均色を適用: {sampled}/{colorItems.Count} 種類。");
        }
        catch (Exception ex)
        {
            // 平均色の取得に失敗してもカタログ手入力色で続行する。
            AppendLog($"[注意] テクスチャ平均色の取得をスキップ: {ex.Message}");
        }

        var matcher = new ColorMatcher(colorItems);

        var cases = new List<GenerationResult>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < wslImagePaths.Count; i++)
        {
            string imageWsl = wslImagePaths[i];
            ArchStatus.Text = $"案{i + 1}/{wslImagePaths.Count} を 3D化中...（数分かかります）";
            ProgressShow($"案 {i + 1}/{wslImagePaths.Count} を 3D化中...（数分）",
                         indeterminate: true);
            AppendLog($"=== 案{i + 1}: TRELLIS.2 で 3D化: {imageWsl} ===");

            // 案ごとに別 GLB 名。
            string glbName = $"arch_case{i}.glb";
            var tr = await Trellis2Runner.RunAsync(imageWsl, glbName, AppendLog);
            if (!tr.Success)
            {
                AppendLog($"案{i + 1} 3D化失敗 (終了コード {tr.ExitCode})");
                cases.Add(new GenerationResult { Error = $"3D化失敗(終了 {tr.ExitCode})" });
                continue;
            }

            string glbUnc = System.IO.Path.Combine(wslGlbUncDir, glbName);
            if (!System.IO.File.Exists(glbUnc))
            {
                AppendLog($"案{i + 1} GLB が見つかりません: {glbUnc}");
                cases.Add(new GenerationResult { Error = "GLBが見つかりません" });
                continue;
            }

            AppendLog($"=== 案{i + 1}: ボクセル化 (解像度 {resolution}, ブロック {blockId}) ===");
            ProgressShow($"案 {i + 1}/{wslImagePaths.Count} をボクセル化中...",
                         indeterminate: true);
            var vox = await Task.Run(() =>
                MeshVoxelizer.Voxelize(
                    glbUnc, resolution, MeshVoxelizer.FillMode.Hollow, blockId, matcher));
            if (vox.Blocks == null)
            {
                AppendLog($"案{i + 1} ボクセル化失敗: {vox.Error}");
                cases.Add(new GenerationResult { Error = $"ボクセル化失敗: {vox.Error}" });
                continue;
            }
            AppendLog($"案{i + 1} 完了: {vox.Blocks.Count} ブロック");
            if (!string.IsNullOrEmpty(vox.MatchLog))
            {
                AppendLog(vox.MatchLog);
                try
                {
                    string dump = DiagPath($"colormatch_case{i}.txt");
                    System.IO.File.WriteAllText(dump, vox.MatchLog);
                    AppendLog($"(集計をファイル出力: {dump})");
                }
                catch (Exception ex)
                {
                    AppendLog($"(集計ファイル出力失敗: {ex.Message})");
                }
            }
            cases.Add(vox);
        }

        sw.Stop();
        _archCases = cases;

        // 全案の処理が終わったので進捗バーを隠す。
        ProgressHide();

        // 案ボタンを成否で有効化。
        var caseButtons = new[] { ArchCase1Btn, ArchCase2Btn, ArchCase3Btn };
        int okCount = 0;
        for (int i = 0; i < caseButtons.Length; i++)
        {
            if (i < cases.Count && cases[i].Blocks != null)
            {
                caseButtons[i].IsEnabled = true;
                caseButtons[i].Content = $"案{i + 1} ({cases[i].Blocks!.Count})";
                okCount++;
            }
            else
            {
                caseButtons[i].IsEnabled = false;
                caseButtons[i].Content = $"案{i + 1}";
            }
        }

        ArchStatus.Text = $"[所要 {sw.Elapsed.TotalSeconds:F0} 秒] " +
                          $"成功 {okCount}/{wslImagePaths.Count} 案。";

        // 最初の成功案を表示。
        int firstOk = cases.FindIndex(r => r.Blocks != null);
        if (firstOk >= 0)
            await ShowCase(firstOk);
        else
            ArchResultBox.AppendText("\n全案が失敗しました。\n");

        Log($"彫刻(一気通貫): {ArchStatus.Text}");
    }

    // 診断ファイルの保存先。実行ファイル直下の diagnostics フォルダに置く。
    // 以前は Desktop に出していたが、ModSorter 配下にまとめる。
    // (MeshVoxelizer 側にも同名ヘルパーがあるが、依存を増やさずローカルに持つ)
    private static string DiagPath(string fileName)
    {
        string dir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "diagnostics");
        System.IO.Directory.CreateDirectory(dir);
        return System.IO.Path.Combine(dir, fileName);
    }

    // バニラのクライアント jar(テクスチャ入り)を探す。
    // CurseForge は ...\minecraft\Install\versions に、公式型は <instance>\versions に入る。
    // ローダー(neoforge/forge/fabric/quilt)やスナップショットは除外し、
    // 純粋なバージョン番号(数字とドット)のフォルダを優先する。
    private string? FindVanillaJar()
    {
        try
        {
            var dirsToScan = new List<string>();

            // CurseForge: ...\Instances\<name> から2つ上がって Install\versions
            var instancesParent = System.IO.Directory.GetParent(_instancePath ?? "")?.FullName;
            var cfRoot = System.IO.Directory.GetParent(instancesParent ?? "")?.FullName;
            if (cfRoot != null)
                dirsToScan.Add(System.IO.Path.Combine(cfRoot, "Install", "versions"));

            // 公式ランチャー型: <instance>\versions
            if (!string.IsNullOrEmpty(_instancePath))
                dirsToScan.Add(System.IO.Path.Combine(_instancePath, "versions"));

            var candidates = new List<string>();
            foreach (var versionsDir in dirsToScan)
            {
                if (!System.IO.Directory.Exists(versionsDir)) continue;
                foreach (var dir in System.IO.Directory.GetDirectories(versionsDir))
                {
                    string ver = System.IO.Path.GetFileName(dir);

                    // ローダー名を含むフォルダは除外(テクスチャが無い)。
                    string lower = ver.ToLowerInvariant();
                    if (lower.Contains("neoforge") || lower.Contains("forge") ||
                        lower.Contains("fabric") || lower.Contains("quilt"))
                        continue;

                    // 純粋なバージョン番号(数字とドットのみ)を優先候補に。
                    // 例: 1.21.1 はOK、26.2-pre-1 や snapshot は除外。
                    bool isPureVersion = ver.All(c => char.IsDigit(c) || c == '.');
                    if (!isPureVersion) continue;

                    string jar = System.IO.Path.Combine(dir, ver + ".jar");
                    if (System.IO.File.Exists(jar)) candidates.Add(jar);
                }
            }

            // バージョン番号の文字列順で一番大きいもの(=新しめ)を選ぶ。
            return candidates.OrderBy(p => p).LastOrDefault();
        }
        catch { return null; }
    }
}