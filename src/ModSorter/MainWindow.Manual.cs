using ModSorter.Architect.Generation;
using ModSorter.Architect.Preview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ModSorter;

// 手動生成（パラメトリック建築 / Tab 6 / LLM不使用）の処理。
// 入力はすべてスライダー・トグル・チェックボックス（数値入力は使わない）。
// 使用ブロックは壁/床/屋根を個別に BlockPicker で選ぶ（結果の先頭1件を採用）。
// 開口は窓/ドア/アーチそれぞれ「面チェック＋本数スライダー」で指定し、
// チェックONの面へ本数ぶん等間隔配置する。ドア未指定でも Expander が正面に自動保証。
public partial class MainWindow
{
    private bool _manualPreviewReady = false;
    private DispatcherTimer? _manualDebounce;
    private List<GeneratedBlock>? _manualBlocks;

    // 壁/床/屋根に使うブロック（各1種）。既定は木材3種。
    private string _manualWallBlock = "minecraft:oak_planks";
    private string _manualFloorBlock = "minecraft:spruce_planks";
    private string _manualRoofBlock = "minecraft:dark_oak_planks";

    // トップメニューの「手動生成」ボタン → Tab 6。初回にプレビュー初期化＋初描画。
    private async void NavManual_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 6;
        Log("手動生成モードを開きました。");

        if (!_manualPreviewReady)
        {
            await ManualInitPreviewAsync();
            if (_manualPreviewReady)
                ManualScheduleRender();
        }
    }

    // タブ内 WebView2 を初期化。
    private async System.Threading.Tasks.Task ManualInitPreviewAsync()
    {
        try
        {
            await ManualPreviewWeb.EnsureCoreWebView2Async();

            var navDone = new System.Threading.Tasks.TaskCompletionSource<bool>();
            void Handler(object? s,
                Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs ev)
            {
                ManualPreviewWeb.NavigationCompleted -= Handler;
                navDone.TrySetResult(ev.IsSuccess);
            }
            ManualPreviewWeb.NavigationCompleted += Handler;

            ManualPreviewWeb.NavigateToString(PreviewHtml.Build());

            var completed = await System.Threading.Tasks.Task.WhenAny(
                navDone.Task, System.Threading.Tasks.Task.Delay(10000));
            _manualPreviewReady = (completed == navDone.Task && navDone.Task.Result);

            if (!_manualPreviewReady)
            {
                ManualPreviewWeb.NavigationCompleted -= Handler;
                ManualStatus.Text = "プレビューの初期化に失敗しました。";
            }
        }
        catch (Exception ex)
        {
            _manualPreviewReady = false;
            ManualStatus.Text = $"プレビュー初期化エラー: {ex.Message}";
        }
    }

    // 寸法・勾配スライダー変更 → ラベル更新＋再描画予約。
    private void ManualSlider_Changed(object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider sl || sl.Tag is not string which) return;
        int v = (int)Math.Round(e.NewValue);
        switch (which)
        {
            case "w": if (ManualWidthLabel != null) ManualWidthLabel.Text = v.ToString(); break;
            case "d": if (ManualDepthLabel != null) ManualDepthLabel.Text = v.ToString(); break;
            case "h": if (ManualHeightLabel != null) ManualHeightLabel.Text = v.ToString(); break;
            case "p": if (ManualPitchLabel != null) ManualPitchLabel.Text = v.ToString(); break;
        }
        ManualScheduleRender();
    }

    // 開口の本数スライダー変更（窓/ドア/アーチ共通） → 対応ラベル更新＋再描画予約。
    private void ManualWinCount_Changed(object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider sl) return;
        int v = (int)Math.Round(e.NewValue);

        // 名前でラベルを引く（対応するラベルがあれば更新）。
        var label = FindName(sl.Name.Replace("Slider", "Label")) as TextBlock;
        if (label != null) label.Text = v.ToString();

        ManualScheduleRender();
    }

    // 屋根タイプ ComboBox 変更 → 再描画予約。
    private void ManualParam_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ManualScheduleRender();

    // 棟の向きトグル。OFF=X軸 / ON=Z軸。
    private void ManualRidge_Toggled(object sender, RoutedEventArgs e)
    {
        if (ManualRidgeToggle != null)
            ManualRidgeToggle.Content = (ManualRidgeToggle.IsChecked == true) ? "Z軸" : "X軸";
        ManualScheduleRender();
    }

    // 面チェック ON/OFF → 再描画予約（スライダー出現は XAML の BoolToVis が担当）。
    private void ManualParam_Toggled(object sender, RoutedEventArgs e)
        => ManualScheduleRender();

    // ===== 使用ブロック選択（壁/床/屋根 個別） =====
    // BlockPicker は複数選択専用なので、結果の先頭1件を採用する。
    private string? PickSingleBlock(string current)
    {
        var win = new BlockPickerWindow(new[] { current }) { Owner = this };
        bool? ok = win.ShowDialog();
        if (ok == true && !string.IsNullOrWhiteSpace(win.ResultCsv))
        {
            var first = win.ResultCsv
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0);
            return first;
        }
        return null;
    }

    private void ManualPickWall_Click(object sender, RoutedEventArgs e)
    {
        var b = PickSingleBlock(_manualWallBlock);
        if (b != null) { _manualWallBlock = b; Log($"手動生成: 壁ブロック = {b}"); ManualScheduleRender(); }
    }

    private void ManualPickFloor_Click(object sender, RoutedEventArgs e)
    {
        var b = PickSingleBlock(_manualFloorBlock);
        if (b != null) { _manualFloorBlock = b; Log($"手動生成: 床ブロック = {b}"); ManualScheduleRender(); }
    }

    private void ManualPickRoof_Click(object sender, RoutedEventArgs e)
    {
        var b = PickSingleBlock(_manualRoofBlock);
        if (b != null) { _manualRoofBlock = b; Log($"手動生成: 屋根ブロック = {b}"); ManualScheduleRender(); }
    }

    // 再描画をデバウンス（250ms）して予約。
    private void ManualScheduleRender()
    {
        if (!_manualPreviewReady) return;

        if (_manualDebounce == null)
        {
            _manualDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _manualDebounce.Tick += async (_, __) =>
            {
                _manualDebounce!.Stop();
                await ManualRebuildAndRenderAsync();
            };
        }
        _manualDebounce.Stop();
        _manualDebounce.Start();
    }

    // 指定面に、kind の開口を count 個、角を避けた内側へ等間隔配置する。
    private static void AddOpeningsForFace(
        List<Opening> ops, string face, string kind, int count, int span)
    {
        if (count <= 0) return;
        int lo = 1, hi = span - 2;
        if (hi < lo) { lo = 0; hi = span - 1; }
        int usable = hi - lo + 1;
        if (usable <= 0) return;

        int n = Math.Min(count, usable);
        // 窓は腰高(Level=2)、ドア・アーチは床から(Level=1)。
        int level = (kind == "window") ? 2 : 1;
        for (int i = 0; i < n; i++)
        {
            int offset = (n == 1)
                ? (lo + hi) / 2
                : lo + (int)Math.Round((double)(hi - lo) * i / (n - 1));
            ops.Add(new Opening { Face = face, Kind = kind, Offset = offset, Level = level });
        }
    }

    // チェックONの面の本数スライダー値を読んで開口を積む共通ヘルパ。
    // faceChecks: (面名, チェックボックス, スライダー) の4面ぶん。
    private void CollectOpenings(
        List<Opening> ops, string kind, int w, int d,
        (string face, CheckBox chk, Slider sld)[] faceChecks)
    {
        foreach (var (face, chk, sld) in faceChecks)
        {
            if (chk?.IsChecked != true || sld == null) continue;
            int span = (face == "north" || face == "south") ? w : d;
            AddOpeningsForFace(ops, face, kind, (int)Math.Round(sld.Value), span);
        }
    }

    // 現在のUI値から StructureSpec を作って展開し、タブ内プレビューへ描画。
    private async System.Threading.Tasks.Task ManualRebuildAndRenderAsync()
    {
        if (!_manualPreviewReady) return;

        int w = (int)Math.Round(ManualWidthSlider.Value);
        int d = (int)Math.Round(ManualDepthSlider.Value);
        int h = (int)Math.Round(ManualHeightSlider.Value);
        int pitch = Math.Clamp((int)Math.Round(ManualPitchSlider.Value), 1, 4);

        string roofType = (ManualRoofCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "flat";
        string ridgeAxis = (ManualRidgeToggle?.IsChecked == true) ? "z" : "x";

        // 許可リストには壁・床・屋根の3種を必ず入れる（Pick がこの中からしか採用しないため）。
        var allowed = new List<string> { _manualWallBlock, _manualFloorBlock, _manualRoofBlock }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (allowed.Count == 0) allowed.Add("minecraft:oak_planks");

        // ===== 開口の収集（窓→ドア→アーチの順。被りは後勝ち） =====
        var openings = new List<Opening>();
        CollectOpenings(openings, "window", w, d, new[]
        {
            ("north", ManualWinNorth, ManualWinNorthSlider),
            ("south", ManualWinSouth, ManualWinSouthSlider),
            ("east",  ManualWinEast,  ManualWinEastSlider),
            ("west",  ManualWinWest,  ManualWinWestSlider),
        });
        CollectOpenings(openings, "door", w, d, new[]
        {
            ("north", ManualDoorNorth, ManualDoorNorthSlider),
            ("south", ManualDoorSouth, ManualDoorSouthSlider),
            ("east",  ManualDoorEast,  ManualDoorEastSlider),
            ("west",  ManualDoorWest,  ManualDoorWestSlider),
        });
        CollectOpenings(openings, "arch", w, d, new[]
        {
            ("north", ManualArchNorth, ManualArchNorthSlider),
            ("south", ManualArchSouth, ManualArchSouthSlider),
            ("east",  ManualArchEast,  ManualArchEastSlider),
            ("west",  ManualArchWest,  ManualArchWestSlider),
        });

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = roofType,
            RidgeAxis = ridgeAxis,
            RoofPitch = pitch,
            WallBlock = _manualWallBlock,
            FloorBlock = _manualFloorBlock,
            RoofBlock = _manualRoofBlock,
            Openings = openings
        };

        _manualBlocks = StructureExpander.Expand(spec, allowed);
        await ManualRenderAsync(_manualBlocks);
        ManualStatus.Text =
            $"{w}×{d}×{h} / 屋根={roofType}(勾配1:{pitch}) / 開口{openings.Count}件 / {_manualBlocks.Count} ブロック";
    }

    // タブ内 WebView2 へ描画（setTextures→renderBlocks）。
    private async System.Threading.Tasks.Task ManualRenderAsync(List<GeneratedBlock> blocks)
    {
        if (!_manualPreviewReady) return;

        string json = System.Text.Json.JsonSerializer.Serialize(
            blocks.Select(b => new { x = b.X, y = b.Y, z = b.Z, id = b.Id }));

        try
        {
            var texMap = BuildTextureMap(blocks);
            string texJson = System.Text.Json.JsonSerializer.Serialize(texMap);
            string texArg = System.Text.Json.JsonSerializer.Serialize(texJson);
            await ManualPreviewWeb.ExecuteScriptAsync($"setTextures({texArg})");
        }
        catch (Exception ex)
        {
            Log($"手動生成テクスチャ取得をスキップ: {ex.Message}");
        }

        try
        {
            string blocksArg = System.Text.Json.JsonSerializer.Serialize(json);
            await ManualPreviewWeb.ExecuteScriptAsync($"renderBlocks({blocksArg})");
        }
        catch (Exception) { }
    }

    // 「NBT出力」ボタン。
    private void ManualExport_Click(object sender, RoutedEventArgs e)
    {
        if (_manualBlocks == null || _manualBlocks.Count == 0)
        {
            ManualStatus.Text = "まだ生成物がありません。パラメータを調整してください。";
            return;
        }

        string outPath = ResolveSchematicOutPath(ManualNameBox?.Text ?? "", "manual_building");
        if (outPath.Length == 0)
        {
            ManualStatus.Text = "出力をキャンセルしました。";
            return;
        }

        var nbtBlocks = _manualBlocks
            .Select(b => new StructureNbtWriter.Block { Name = b.Id, X = b.X, Y = b.Y, Z = b.Z })
            .ToList();

        try
        {
            StructureNbtWriter.Save(nbtBlocks, outPath);
            _lastMachineNbtPath = outPath;
            ManualStatus.Text = $"出力しました（{nbtBlocks.Count} ブロック）: {outPath}";
            Log($"手動生成の構造NBTを出力: {outPath}");
        }
        catch (Exception ex)
        {
            ManualStatus.Text = $"出力に失敗: {ex.Message}";
            Log($"手動生成の出力に失敗: {ex.Message}");
        }
    }

    // 「出力フォルダを開く」ボタン。
    private void ManualOpenFolder_Click(object sender, RoutedEventArgs e)
        => MachineOpenFolder_Click(sender, e);
}
