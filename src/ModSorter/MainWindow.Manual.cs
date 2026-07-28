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

        // アクティブな中分類UserControlの変更通知を購読する。
        HookActiveParams();

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
        ApplyManualFenceStates(_manualBlocks);
        await ManualRenderAsync(_manualBlocks);
        ManualStatus.Text = $"{summary} / {_manualBlocks.Count} ブロック";
    }

    // 手動生成で配置した接続系ブロック(フェンス／塀(wall)／板ガラス・鉄格子)へ
    // 接続状態を確定して持たせる。プレビューとNBT出力で同じ状態を使う。
    //
    // 実機準拠の接続規則:
    //  fence … 同族フェンス(木製同士 / nether_brick_fence は単独)と、硬い面を持つ
    //          フルブロックに接続する。wall とは接続しない。
    //  wall  … 他の wall・板ガラス/鉄格子・フルブロックに接続する。
    //          side は none|low|tall、支柱 up は true|false。
    //  pane  … 他の板ガラス/鉄格子・wall・フルブロックに接続する(値は true|false)。
    private static void ApplyManualFenceStates(List<GeneratedBlock> blocks)
    {
        // 座標 → baseId。同一座標が重複したら後勝ち(Expand の後勝ち規約に合わせる)。
        var byPos = new Dictionary<(int X, int Y, int Z), string>();
        foreach (var b in blocks)
            byPos[(b.X, b.Y, b.Z)] = ManualBaseId(b.Id);

        foreach (var block in blocks)
        {
            string selfId = ManualBaseId(block.Id);
            var kind = GetManualConnectKind(selfId);
            if (kind == ManualConnectKind.None) continue;

            bool north = ManualConnectsTo(byPos, kind, selfId, block.X, block.Y, block.Z - 1);
            bool south = ManualConnectsTo(byPos, kind, selfId, block.X, block.Y, block.Z + 1);
            bool west = ManualConnectsTo(byPos, kind, selfId, block.X - 1, block.Y, block.Z);
            bool east = ManualConnectsTo(byPos, kind, selfId, block.X + 1, block.Y, block.Z);

            block.Properties ??= new Dictionary<string, string>(StringComparer.Ordinal);

            if (kind == ManualConnectKind.Wall)
            {
                // 実機 WallBlock は直上の当たり判定で側面の高さを決める。
                // 直上にブロックがあれば tall、無ければ low。
                bool hasAbove = byPos.ContainsKey((block.X, block.Y + 1, block.Z));
                string side = hasAbove ? "tall" : "low";

                block.Properties["north"] = north ? side : "none";
                block.Properties["south"] = south ? side : "none";
                block.Properties["west"] = west ? side : "none";
                block.Properties["east"] = east ? side : "none";

                // 実機 shouldRaisePost 相当。南北または東西へ直線に貫通している
                // ときだけ支柱が消える。孤立・L字・T字・十字では支柱が立つ。
                // 直線かつ直上ありのときは側面が tall になり実機でも支柱は立たない
                // ため、直線判定だけで一致する。
                bool straight = (north && south && !east && !west)
                             || (east && west && !north && !south);
                block.Properties["up"] = straight ? "false" : "true";
            }
            else
            {
                block.Properties["north"] = north ? "true" : "false";
                block.Properties["south"] = south ? "true" : "false";
                block.Properties["west"] = west ? "true" : "false";
                block.Properties["east"] = east ? "true" : "false";
            }
        }
    }

    // 接続系ブロックの種別。
    private enum ManualConnectKind { None, Fence, Wall, Pane }

    // 状態付きID("minecraft:oak_fence[north=true]")からベースIDを取る。
    private static string ManualBaseId(string id) => id.Split('[')[0];

    // baseId から namespace を除いたブロック名を取る。
    private static string ManualBlockName(string baseId)
    {
        int separator = baseId.IndexOf(':');
        return separator >= 0 ? baseId[(separator + 1)..] : baseId;
    }

    private static ManualConnectKind GetManualConnectKind(string baseId)
    {
        string name = ManualBlockName(baseId);
        // "_fence_gate" は "_gate" で終わるためフェンスには一致しない。
        if (name.EndsWith("_fence", StringComparison.Ordinal)) return ManualConnectKind.Fence;
        // "wall_torch" 等は "_wall" で終わらないため塀には一致しない。
        if (name.EndsWith("_wall", StringComparison.Ordinal)) return ManualConnectKind.Wall;
        if (name.EndsWith("_pane", StringComparison.Ordinal) || name == "iron_bars")
            return ManualConnectKind.Pane;
        return ManualConnectKind.None;
    }

    // 実機ではネザーレンガのフェンスは木製フェンスと接続しない。
    private static bool SameManualFenceGroup(string aBaseId, string bBaseId)
        => (ManualBlockName(aBaseId) == "nether_brick_fence")
        == (ManualBlockName(bBaseId) == "nether_brick_fence");

    // 指定座標の隣接ブロックへ接続するかを判定する。
    private static bool ManualConnectsTo(
        Dictionary<(int X, int Y, int Z), string> byPos,
        ManualConnectKind kind, string selfId, int x, int y, int z)
    {
        if (!byPos.TryGetValue((x, y, z), out var otherId)) return false;

        var otherKind = GetManualConnectKind(otherId);
        if (otherKind != ManualConnectKind.None)
        {
            return kind switch
            {
                ManualConnectKind.Fence =>
                    otherKind == ManualConnectKind.Fence && SameManualFenceGroup(selfId, otherId),
                ManualConnectKind.Wall =>
                    otherKind == ManualConnectKind.Wall || otherKind == ManualConnectKind.Pane,
                ManualConnectKind.Pane =>
                    otherKind == ManualConnectKind.Pane || otherKind == ManualConnectKind.Wall,
                _ => false
            };
        }
        return IsManualFullCube(otherId);
    }

    // 形状ブロック・装飾ブロックを名前で除外し、残りを「硬い面を持つフルブロック」
    // とみなす。手動生成のパレットは壁/床/屋根＋ガラス＋接続系に限られるため、
    // モデル解析まで踏み込まずこの判定で足りる。
    private static readonly string[] ManualNonCubeSuffixes =
    {
        "_slab", "_stairs", "_fence", "_wall", "_pane", "_gate", "_door", "_trapdoor",
        "_button", "_pressure_plate", "_sign", "_torch", "_carpet", "_rod", "_chain",
        "_ladder", "_vine", "_head", "_banner", "_candle", "_bars", "_lantern",
        "_leaves", "_shulker_box", "_pot", "_bed"
    };

    // 実機の isExceptionForConnection 相当。フルキューブでも接続しないブロック。
    private static readonly HashSet<string> ManualConnectionExceptions =
        new(StringComparer.Ordinal)
        {
            "barrier", "pumpkin", "carved_pumpkin", "jack_o_lantern", "melon",
            "shulker_box", "air", "cave_air", "void_air", "water", "lava"
        };

    private static bool IsManualFullCube(string baseId)
    {
        string name = ManualBlockName(baseId);
        if (name.Length == 0) return false;
        if (ManualConnectionExceptions.Contains(name)) return false;
        if (name == "iron_bars" || name == "chain" || name == "ladder" || name == "torch")
            return false;
        foreach (var suffix in ManualNonCubeSuffixes)
            if (name.EndsWith(suffix, StringComparison.Ordinal)) return false;
        return true;
    }

    // 他 partial からの参照互換のため残す。フェンス種別かどうかだけを返す。
    private static bool IsManualFence(string id)
        => GetManualConnectKind(ManualBaseId(id)) == ManualConnectKind.Fence;

    // タブ内 WebView2 へ描画（setTextures→renderBlocks）。
    // モデルJSONから形状(elements)と面別テクスチャを解決し、機械プレビューと同じ
    // {x,y,z,id,elements:[{from,to,faces:{面名:{tex,uv,rot}}}],rotX,rotY} の形で渡す。
    // 形状が取れないブロックは elements 無し → JS側で 1×1×1 にフォールバック。
    private async System.Threading.Tasks.Task ManualRenderAsync(List<GeneratedBlock> blocks)
    {
        if (!_manualPreviewReady) return;

        var vanilla = FindVanillaJar();
        var modJars = (_mods ?? new List<ModSorter.Models.ModEntry>())
            .Select(m => m.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        string blocksJson;
        var texMap = new Dictionary<string, string>();

        using (var tp = new ModSorter.Architect.Generation.BlockTextureProvider(vanilla, modJars))
        {
            // 面別テクスチャPNGをキーで集めるローカル関数（機械プレビューと同じ）。
            void AddFaceTexture(string texKey)
            {
                if (string.IsNullOrEmpty(texKey)) return;
                if (texMap.ContainsKey(texKey)) return;
                var png = tp.GetTextureByPath(texKey);
                if (png != null && png.Length > 0)
                    texMap[texKey] = "data:image/png;base64," + System.Convert.ToBase64String(png);
            }

            var payload = new List<object>(blocks.Count);
            foreach (var b in blocks)
            {
                string baseId = b.Id.Split('[')[0];

                var shape = tp.GetBlockShape(b.Id, b.Properties);
                if (shape != null && shape.Elements.Count > 0)
                {
                    payload.Add(BuildManualElementPayload(
                        b, shape.Elements, shape.RotX, shape.RotY, AddFaceTexture));
                }
                else
                {
                    // 形状不明 → elements 無し。JS側で 1×1×1。
                    payload.Add(new { x = b.X, y = b.Y, z = b.Z, id = b.Id });
                }

                // フォールバック用に baseId のテクスチャも入れておく。
                if (!texMap.ContainsKey(baseId))
                {
                    var png = tp.GetTexture(baseId);
                    if (png != null && png.Length > 0)
                        texMap[baseId] = "data:image/png;base64," + System.Convert.ToBase64String(png);
                }
            }

            blocksJson = System.Text.Json.JsonSerializer.Serialize(payload);
        }

        try
        {
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
            string blocksArg = System.Text.Json.JsonSerializer.Serialize(blocksJson);
            await ManualPreviewWeb.ExecuteScriptAsync($"renderBlocks({blocksArg})");
        }
        catch (Exception) { }
    }

    // GeneratedBlock 用の elements payload 組み立て。機械側 BuildElementPayload の
    // GeneratedBlock 版（機械側は PlacedBlock 引数固定のため流用不可・別実装）。
    // faces の各 texKey を addFaceTex で texMap に集めつつ from/to/faces/要素回転を載せる。
    private static object BuildManualElementPayload(
        GeneratedBlock b,
        List<ModSorter.Architect.Generation.BlockTextureProvider.ShapeElement> elements,
        int rotX, int rotY,
        Action<string> addFaceTex)
    {
        var elems = new List<object>(elements.Count);
        foreach (var el in elements)
        {
            var faces = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var f in el.Faces)
            {
                addFaceTex(f.Value.Tex);
                faces[f.Key] = new
                {
                    tex = f.Value.Tex,
                    uv = f.Value.Uv,
                    rot = f.Value.Rotation
                };
            }
            elems.Add(new
            {
                from = el.From,
                to = el.To,
                faces,
                rotAngle = el.RotAngle,
                rotAxis = el.RotAxis,
                rotOrigin = el.RotOrigin
            });
        }
        return new
        {
            x = b.X,
            y = b.Y,
            z = b.Z,
            id = b.Id,
            elements = elems,
            rotX = rotX,
            rotY = rotY
        };
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
            .Select(b => new StructureNbtWriter.Block
            {
                Name = b.Id,
                X = b.X,
                Y = b.Y,
                Z = b.Z,
                Properties = b.Properties
            })
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
        // 選択中分類に応じて ManualParamHost.Content を差し替える。
        string sub = (ManualSubCategoryCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "house";

        if (ManualParamHost != null)
        {
            var current = ManualParamHost.Content as ModSorter.Architect.Manual.IManualParamControl;
            string currentTag = current switch
            {
                ModSorter.Architect.Manual.ApartmentParamsControl => "apartment",
                ModSorter.Architect.Manual.HouseParamsControl => "house",
                _ => ""
            };

            if (currentTag != sub)
            {
                ManualParamHost.Content = sub switch
                {
                    "apartment" => new ModSorter.Architect.Manual.ApartmentParamsControl(),
                    _ => new ModSorter.Architect.Manual.HouseParamsControl()
                };
                HookActiveParams();
            }
        }

        if (!_manualPreviewReady) return;
        ManualScheduleRender();
    }

    // ManualParamHost.Content（アクティブな中分類UserControl）の
    // ParamsChanged を購読する。差し替えのたびに呼ぶ（重複購読を避けるため一旦外す）。
    private void HookActiveParams()
    {
        if (ManualParamHost?.Content is ModSorter.Architect.Manual.IManualParamControl active)
        {
            active.ParamsChanged -= OnActiveParamsChanged;
            active.ParamsChanged += OnActiveParamsChanged;
        }
    }

    private void OnActiveParamsChanged(object? sender, EventArgs e) => ManualScheduleRender();

    // 「出力フォルダを開く」ボタン。
    private void ManualOpenFolder_Click(object sender, RoutedEventArgs e)
        => MachineOpenFolder_Click(sender, e);
}
