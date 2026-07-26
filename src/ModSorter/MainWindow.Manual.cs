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
// 中分類パラメータUIは UserControl（IManualParamControl）に分離済み。
// この partial は「プレビュー基盤・デバウンス・展開/描画・NBT出力・セレクタ配線」を担う。
public partial class MainWindow
{
    private bool _manualPreviewReady = false;
    private DispatcherTimer? _manualDebounce;
    private List<GeneratedBlock>? _manualBlocks;

    // 戸建てUserControlの変更通知を購読済みか（1回だけ購読するため）。
    private bool _manualParamsHooked = false;

    // トップメニューの「手動生成」ボタン → Tab 6。初回にプレビュー初期化＋初描画。
    private async void NavManual_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 6;
        Log("手動生成モードを開きました。");

        // 中分類UserControlの変更通知を購読（1回だけ）。発火で再描画予約する。
        if (!_manualParamsHooked && ManualHouseParams != null)
        {
            ManualHouseParams.ParamsChanged += (_, __) => ManualScheduleRender();
            _manualParamsHooked = true;
        }

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

    // アクティブな中分類UserControlの BuildSpec からspecを得て展開し、タブ内プレビューへ描画。
    private async System.Threading.Tasks.Task ManualRebuildAndRenderAsync()
    {
        if (!_manualPreviewReady) return;

        // 現状は戸建て(ManualHouseParams)のみ。中分類が増えたら
        // ManualParamHost.Content を IManualParamControl として拾う形へ広げる。
        var active = ManualParamHost?.Content as ModSorter.Architect.Manual.IManualParamControl;
        if (active == null) return;

        var spec = active.BuildSpec(out var allowed, out var summary);

        _manualBlocks = StructureExpander.Expand(spec, allowed);
        await ManualRenderAsync(_manualBlocks);
        ManualStatus.Text = $"{summary} / {_manualBlocks.Count} ブロック";
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

    // ===== 大分類 → 中分類 セレクタ(フェーズ1.5) =====
    // 現状は建築物→戸建ての1経路のみ。中分類が増えたら、選択に応じて
    // ManualParamHost.Content へ対応UserControlを差し込む形へ広げる。

    private void ManualCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_manualPreviewReady) return;
        ManualScheduleRender();
    }

    private void ManualSubCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_manualPreviewReady) return;
        ManualScheduleRender();
    }

    // 「出力フォルダを開く」ボタン。
    private void ManualOpenFolder_Click(object sender, RoutedEventArgs e)
        => MachineOpenFolder_Click(sender, e);
}
