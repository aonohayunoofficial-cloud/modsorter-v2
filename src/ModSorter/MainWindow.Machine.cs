using ModSorter.Architect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ModSorter;

// Create 機械生成（独立パネル / Tab 5）の処理。
// 自然言語のお題 + 空間サイズ(X/Y/Z) を受け取り、ModuleGenerator で
// 向きの揃った動力機械の骨格を生成し、構造NBTとして出力する。
public partial class MainWindow
{
    // 直近に生成した機械のNBT出力先（フォルダを開くボタンで使う）。
    private string? _lastMachineNbtPath;

    // 直近に生成した機械のブロック配置。3Dビューを開き直すときに再描画する。
    private List<ModSorter.Clients.ModuleGenerator.PlacedBlock>? _lastMachinePlaced;

    // スキマティックNBTの出力先パスを決める共通処理（機械・建築で共用）。
    // .minecraft/schematics/<名前>.nbt を基本とし、同名があれば上書き確認モーダルを出す。
    // 上書き拒否なら空文字を返す（呼び出し側で保存中止）。
    // instancePath 未設定や schematics 作成失敗時は diagnostics にフォールバックする。
    // rawName: UI 欄の生入力。defaultName: 空/不正時に使う既定名。
    private string ResolveSchematicOutPath(string rawName, string defaultName)
    {
        // 名前のサニタイズ。空なら既定名。ファイル名に使えない文字は除去。
        string name = (rawName ?? "").Trim();
        if (name.Length == 0) name = defaultName;
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "");
        if (name.Length == 0) name = defaultName;
        if (name.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);

        // schematics フォルダを組み立てる。instancePath 未設定なら diagnostics へ。
        string? dir = null;
        if (!string.IsNullOrEmpty(_instancePath))
        {
            try
            {
                string sch = Path.Combine(_instancePath, "schematics");
                Directory.CreateDirectory(sch);
                dir = sch;
            }
            catch (Exception ex)
            {
                Log($"schematics フォルダを準備できませんでした。diagnostics に出力します: {ex.Message}");
            }
        }
        else
        {
            Log(".minecraft フォルダが未設定です。diagnostics に出力します（設定タブで指定可）。");
        }

        if (dir == null)
            return DiagPath(name + ".nbt");

        string path = Path.Combine(dir, name + ".nbt");

        // 同名があれば上書き確認。
        if (File.Exists(path))
        {
            var result = MessageBox.Show(
                $"同名のスキマティックが既に存在します。\n{path}\n上書きしますか?",
                "ModSorter - 上書き確認",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return "";
        }
        return path;
    }

    // 機械NBTの出力先パス。共通処理 ResolveSchematicOutPath に委譲する。
    private string ResolveMachineOutPath()
        => ResolveSchematicOutPath(MachineNameBox?.Text ?? "", "module_machine");

    // トップメニューの「Create機械」ボタン → Tab 5 へ遷移。
    private async void NavMachine_Click(object sender, RoutedEventArgs e)
    {
        // 建築モードと同じく、リソースは初回遷移時に遅延起動する。
        _architectHost ??= new ArchitectModeHost();
        MainTabs.SelectedIndex = 5;
        Log("Create機械モードを開きました。");

        // モデル一覧が未取得なら読み込む。
        if (MachineModelCombo.ItemsSource == null)
            await LoadMachineModelsAsync();
    }

    // Ollama のモデル一覧を Tab5 のコンボに読み込む。
    // 建築モードと同じ _architectHost.Generation.ListModelsAsync() を使い回す。
    private async Task LoadMachineModelsAsync()
    {
        if (_architectHost == null) return;

        MachineModelCombo.IsEnabled = false;
        string? previous = MachineModelCombo.SelectedItem as string;

        var models = await _architectHost.Generation.ListModelsAsync();
        MachineModelCombo.ItemsSource = models;
        MachineModelCombo.IsEnabled = true;

        if (models.Count == 0)
        {
            MachineStatus.Text = "モデル一覧を取得できません（Ollama未起動の可能性）。";
            return;
        }

        if (previous != null && models.Contains(previous))
            MachineModelCombo.SelectedItem = previous;
        else
            MachineModelCombo.SelectedIndex = 0;

        MachineStatus.Text = $"モデル {models.Count} 件を取得しました。";
    }

    private async void MachineModelReload_Click(object sender, RoutedEventArgs e)
    {
        await LoadMachineModelsAsync();
    }

    // 「機械を生成」ボタン。
    private async void MachineGenerate_Click(object sender, RoutedEventArgs e)
    {
        string prompt = MachinePromptBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MachineStatus.Text = "指示(プロンプト)が空です。";
            return;
        }

        // [テスト用・消してよい] プロンプトに "belttest" と入れると、
        // slope/part/facing を網羅した belt を直接プレビューへ流す。
        // LLM 生成を通さず GetBeltShape の全姿勢を確認するための一時導線。
        // 確認が済んだらこの if ブロックごと削除する。
        if (prompt.Equals("belttest", StringComparison.OrdinalIgnoreCase))
        {
            var testBlocks = new List<ModSorter.Clients.ModuleGenerator.PlacedBlock>();
            // (id, slope, part, facing, x) を並べて生成。横(x)に2マス間隔で配置。
            var cases = new (string slope, string part, string facing)[]
            {
                ("horizontal", "middle", "east"),   // 平たい帯(水平)
                ("horizontal", "start",  "east"),   // 端部品(始点)
                ("horizontal", "end",    "east"),   // 端部品(終点)
                ("upward",     "middle", "east"),   // 斜め(上り)
                ("downward",   "middle", "east"),   // 斜め(下り)
                ("vertical",   "middle", "east"),   // 垂直
                ("sideways",   "middle", "east"),   // 横倒し
                ("horizontal", "middle", "north"),  // 水平・facing=north(Y回転差の確認)
            };
            int tx = 0;
            foreach (var c in cases)
            {
                testBlocks.Add(new ModSorter.Clients.ModuleGenerator.PlacedBlock
                {
                    Id = "create:belt",
                    X = tx,
                    Y = 1,
                    Z = 2,
                    Properties = new Dictionary<string, string>
                    {
                        ["slope"] = c.slope,
                        ["part"] = c.part,
                        ["facing"] = c.facing
                    }
                });
                tx += 3;
            }
            _lastMachinePlaced = testBlocks;
            await RenderMachinePreviewAsync(testBlocks);
            MachineStatus.Text = $"belttest: {testBlocks.Count} 本の belt を描画しました。";
            return;
        }

        // 空間サイズの読み取り。数値でなければエラー。
        if (!int.TryParse(MachineSizeXBox.Text.Trim(), out int sx) ||
            !int.TryParse(MachineSizeYBox.Text.Trim(), out int sy) ||
            !int.TryParse(MachineSizeZBox.Text.Trim(), out int sz))
        {
            MachineStatus.Text = "X・Y・Z は数値で入力してください。";
            return;
        }
        if (sx < 1 || sy < 1 || sz < 1 || sx > 32 || sy > 32 || sz > 32)
        {
            MachineStatus.Text = "X・Y・Z は 1〜32 の範囲で入力してください。";
            return;
        }

        MachineGenBtn.IsEnabled = false;
        MachineResultBox.Text = "";
        MachineStatus.Text = "許可ブロックを準備中...";

        try
        {
            // 動力ブロックの許可リストを作る。テクスチャ実在の有無に依らず、
            // blockstates から抽出したフルパレットを使う。
            var vanilla = FindVanillaJar();
            var modJars = (_mods ?? new List<ModSorter.Models.ModEntry>())
                .Select(m => m.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            Dictionary<string, Dictionary<string, List<string>>> allowed;
            using (var tp = new ModSorter.Architect.Generation.BlockTextureProvider(vanilla, modJars))
            {
                var fullPalette = tp.ExtractBlockPalette();
                allowed = ModSorter.Clients.ModuleGenerator.BuildPowerPalette(fullPalette);

                // 入出力マーカー用の羊毛を許可リストに加える。
                foreach (var mk in new[] { "minecraft:magenta_wool", "minecraft:lime_wool" })
                {
                    if (fullPalette.TryGetValue(mk, out var mp))
                        allowed[mk] = mp;
                }
            }

            if (allowed.Count == 0)
            {
                MachineStatus.Text =
                    "動力ブロックの許可リストが空です。MODスキャン(Create本体)が必要です。";
                MachineGenBtn.IsEnabled = true;
                return;
            }

            MachineStatus.Text =
                $"許可ブロック {allowed.Count} 種。生成中...(数十秒かかることあり)";
            ProgressShow("機械を生成中...", indeterminate: true);

            string selectedModel = (MachineModelCombo.SelectedItem as string ?? "").Trim();

            // 選択されたジャンルを集める(チェックされたものだけ)。
            var genres = new List<string>();
            if (GenrePowerSource.IsChecked == true) genres.Add("動力源");
            if (GenrePowerTransmit.IsChecked == true) genres.Add("動力伝達・分配");
            if (GenrePowerControl.IsChecked == true) genres.Add("動力制御");
            if (GenreProcessing.IsChecked == true) genres.Add("加工");
            if (GenreStorage.IsChecked == true) genres.Add("保管");
            if (GenreFluid.IsChecked == true) genres.Add("流体");
            if (GenreContraption.IsChecked == true) genres.Add("可動・構造");
            if (GenreMeter.IsChecked == true) genres.Add("計測・表示");
            if (GenreRedstone.IsChecked == true) genres.Add("レッドストーン連動");

            const int MAX_ATTEMPTS = 3;
            var sw = Stopwatch.StartNew();

            List<ModSorter.Clients.ModuleGenerator.PlacedBlock>? placed = null;
            List<ModSorter.Architect.Generation.ValidationIssue> issues = new();
            string? refinementNotes = null;

            for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
            {
                MachineStatus.Text =
                    $"生成中... ({attempt}/{MAX_ATTEMPTS} 回目)";

                placed = await ModSorter.Clients.ModuleGenerator.GenerateAsync(
                    prompt, allowed, sx, sy, sz,
                    string.IsNullOrEmpty(selectedModel) ? null : selectedModel,
                    genres, refinementNotes);

                if (placed == null)
                {
                    Log($"生成失敗({attempt}回目): {ModSorter.Clients.ModuleGenerator.LastError}");
                    continue; // 次の試行へ
                }

                // 接続検証 → 自動補正。補正で新たな補正対象が出る(shaft削除→funnel位置再評価 等)
                // ため、AutoFixできるものが無くなるまで収束ループを回す。
                issues = ModSorter.Architect.Generation.ConnectionValidator.Validate(placed);
                int totalFixed = 0;
                for (int pass = 0; pass < 8 && issues.Count > 0; pass++)
                {
                    int fixedCount =
                        ModSorter.Architect.Generation.ConnectionValidator.AutoFix(placed, issues);
                    if (fixedCount == 0) break; // これ以上AutoFixできない → 残りは再生成行き
                    totalFixed += fixedCount;
                    issues = ModSorter.Architect.Generation.ConnectionValidator.Validate(placed);
                }

                if (totalFixed > 0)
                    Log($"接続検証({attempt}回目): 計 {totalFixed} 件を自動補正。残 {issues.Count} 件。");
                else if (issues.Count == 0)
                    Log($"接続検証({attempt}回目): 問題なし。");
                else
                    Log($"接続検証({attempt}回目): 自動補正できる項目なし。残 {issues.Count} 件。");

                foreach (var iss in issues)
                    Log($"  [接続] {iss.CategoryId}: {iss.HumanMessage}");

                if (issues.Count == 0) break; // 合格 → ループ終了

                // 残存(AutoFix不可)があれば不具合点を次回プロンプトへ渡して再生成。
                refinementNotes = string.Join("\n",
                    issues.Select(i => "- " + i.HumanMessage));
            }

            sw.Stop();
            ProgressHide();
            MachineGenBtn.IsEnabled = true;

            if (placed == null)
            {
                MachineStatus.Text =
                    $"生成失敗: {ModSorter.Clients.ModuleGenerator.LastError}";
                return;
            }

            // 直近結果を保持し、3Dプレビューへ描画する。
            _lastMachinePlaced = placed;
            await RenderMachinePreviewAsync(placed);


            if (issues.Count > 0)
                Log($"上限 {MAX_ATTEMPTS} 回でも結合不正が {issues.Count} 件残りました。最良案を出力します。");
            // 結果テキストを組み立て。
            var lines = new List<string>
            {
                $"[所要 {sw.Elapsed.TotalSeconds:F1} 秒] 生成ブロック数: {placed.Count}",
                $"空間サイズ: {sx} x {sy} x {sz}",
                ""
            };
            foreach (var b in placed)
            {
                string propStr = (b.Properties == null || b.Properties.Count == 0)
                    ? "{}"
                    : string.Join(", ", b.Properties.Select(kv => $"{kv.Key}={kv.Value}"));
                lines.Add($"  {b.Id} @({b.X},{b.Y},{b.Z}) {propStr}");
            }
            if (!string.IsNullOrEmpty(ModSorter.Clients.ModuleGenerator.LastError))
                lines.Add($"\n注記: {ModSorter.Clients.ModuleGenerator.LastError}");

            // 構造NBTに保存。
            var nbtBlocks = placed.Select(b =>
                new ModSorter.Architect.Generation.StructureNbtWriter.Block
                {
                    Name = b.Id,
                    X = b.X,
                    Y = b.Y,
                    Z = b.Z,
                    Properties = b.Properties
                }).ToList();

            // 出力先を決定。.minecraft/schematics に <名前>.nbt で保存する。
            // instancePath 未設定や保存不可時は diagnostics へフォールバックする。
            string outPath = ResolveMachineOutPath();
            if (outPath.Length == 0)
            {
                // ユーザーが上書きを拒否した場合は保存を中止し、生成結果だけ表示する。
                MachineResultBox.Text = string.Join("\n", lines);
                MachineStatus.Text = $"生成完了（保存はキャンセルされました）。{placed.Count} ブロック。";
                Log("構造NBTの保存をキャンセルしました（同名上書き拒否）。");
                return;
            }
            ModSorter.Architect.Generation.StructureNbtWriter.Save(nbtBlocks, outPath);
            _lastMachineNbtPath = outPath;
            lines.Add($"\n構造NBTを出力: {outPath}");

            MachineResultBox.Text = string.Join("\n", lines);
            MachineStatus.Text = $"完了。{placed.Count} ブロックを生成しました。";
            Log($"Create機械生成: {placed.Count} ブロック / {sw.Elapsed.TotalSeconds:F1} 秒");
        }
        catch (Exception ex)
        {
            ProgressHide();
            MachineGenBtn.IsEnabled = true;
            MachineStatus.Text = $"エラー: {ex.Message}";
        }
    }

    // 「出力フォルダを開く」ボタン。直近の出力先、無ければ diagnostics を開く。
    private void MachineOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = !string.IsNullOrEmpty(_lastMachineNbtPath)
                ? (Path.GetDirectoryName(_lastMachineNbtPath) ?? "")
                : Path.Combine(AppContext.BaseDirectory, "diagnostics");

            Directory.CreateDirectory(dir);

            // 直近ファイルがあれば選択状態で開く。無ければフォルダだけ開く。
            if (!string.IsNullOrEmpty(_lastMachineNbtPath) && File.Exists(_lastMachineNbtPath))
                Process.Start("explorer.exe", $"/select,\"{_lastMachineNbtPath}\"");
            else
                Process.Start("explorer.exe", $"\"{dir}\"");
        }
        catch (Exception ex)
        {
            MachineStatus.Text = $"フォルダを開けませんでした: {ex.Message}";
        }
    }


    // 「3Dビューを開く」ボタン。
    // ウィンドウが閉じられていても開き直し、直近に生成した機械を再描画する。
    private async void MachineOpenPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_lastMachinePlaced == null || _lastMachinePlaced.Count == 0)
        {
            MachineStatus.Text = "表示できる機械がありません。先に生成してください。";
            return;
        }
        await RenderMachinePreviewAsync(_lastMachinePlaced);
    }

    // Create機械の生成結果を3Dプレビューへ描画する。
    // モデルJSONから見た目形状(elements)と面別テクスチャ(faces)を解決し、
    // {x,y,z,id,elements:[{from,to,faces:{面名:{tex,uv,rot}}}], rotX,rotY} の形で渡す。
    // faces のテクスチャ参照(例 create:block/shaft_side)は texKey として使い、
    // その PNG を texMap[texKey] で送る。JS側は面ごとにこの texKey で貼り、uv でUVを合わせる。
    // 形状が取れないブロックは elements 無し → JS側で 1×1×1 にフォールバック。
    // water_wheel 等の動的描画(BlockEntityRenderer)ブロックは、モデルJSONに本体形状が
    // 無い(枠や別モデルの断片しか取れない)ため、GetBlockShape の結果を使わず
    // 強制的に専用の簡易形状(FallbackShapeFor)を当てて「それらしい塊」にする。
    private async Task RenderMachinePreviewAsync(
        List<ModSorter.Clients.ModuleGenerator.PlacedBlock> placed)
    {
        // プレビューウィンドウが無ければ開く(建築モードと共有する _previewWindow)。
        if (_previewWindow == null)
        {
            _previewWindow = new ModSorter.Architect.Preview.PreviewWindow { Owner = this };
            _previewWindow.Closed += (_, __) => _previewWindow = null;
            _previewWindow.Show();
            await _previewWindow.InitAsync();
        }

        // 準備完了まで少し待つ(最大5秒)。
        int waited = 0;
        while (!_previewWindow.IsReady && waited < 5000)
        {
            await Task.Delay(100);
            waited += 100;
        }
        if (!_previewWindow.IsReady)
        {
            Log("プレビュー描画中止: ウィンドウが準備完了になりませんでした。");
            return;
        }

        var vanilla = FindVanillaJar();
        var modJars = (_mods ?? new List<ModSorter.Models.ModEntry>())
            .Select(m => m.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        string blocksJson;
        // texMap: テクスチャキー(=解決済みテクスチャ参照 or baseId) → dataURI。
        var texMap = new Dictionary<string, string>();

        using (var tp = new ModSorter.Architect.Generation.BlockTextureProvider(vanilla, modJars))
        {
            // 面別テクスチャPNGをキーで集めるローカル関数。
            // texKey は "create:block/shaft_side" のような解決済み参照。
            void AddFaceTexture(string texKey)
            {
                if (string.IsNullOrEmpty(texKey)) return;
                if (texMap.ContainsKey(texKey)) return;
                var png = tp.GetTextureByPath(texKey);
                if (png != null && png.Length > 0)
                    texMap[texKey] = "data:image/png;base64," + System.Convert.ToBase64String(png);
            }

            var payload = new List<object>(placed.Count);
            int shapeHit = 0;
            foreach (var b in placed)
            {
                string baseId = b.Id.Split('[')[0];

                // belt は blockstates を持たない特殊ブロック。専用リゾルバで
                // slope/part/facing からモデルと姿勢を解決する。
                var beltShape = tp.GetBeltShape(b.Id, b.Properties);
                if (beltShape != null && beltShape.Elements.Count > 0)
                {
                    shapeHit++;
                    payload.Add(BuildElementPayload(
                        b, beltShape.Elements, beltShape.RotX, beltShape.RotY, beltShape.RotZ, AddFaceTexture));
                }
                else
                {
                    var shape = tp.GetBlockShape(b.Id, b.Properties);
                    if (shape != null && shape.Elements.Count > 0)
                    {
                        shapeHit++;
                        payload.Add(BuildElementPayload(
                            b, shape.Elements, shape.RotX, shape.RotY, shape.RotZ, AddFaceTexture));
                    }
                    else
                    {
                        // 形状不明(OBJ描画ブロックや未知ブロック) → elements 無し。
                        // JS側で 1×1×1、色は baseId のテクスチャ。水車系OBJは次段で対応。
                        payload.Add(new { x = b.X, y = b.Y, z = b.Z, id = b.Id });
                    }
                }

                // フォールバック用に baseId のテクスチャも入れておく
                // (elements 無しブロック、簡易形状、faces 欠落面の保険)。
                if (!texMap.ContainsKey(baseId))
                {
                    var png = tp.GetTexture(baseId);
                    if (png != null && png.Length > 0)
                        texMap[baseId] = "data:image/png;base64," + System.Convert.ToBase64String(png);
                }
            }

            blocksJson = JsonSerializer.Serialize(payload);
            Log($"プレビュー形状: {placed.Count} ブロック中 {shapeHit} 件を elements で描画。" +
                $"テクスチャ {texMap.Count} 種。");
        }

        // テクスチャを先に送ってから描画する。
        try
        {
            string texJson = JsonSerializer.Serialize(texMap);
            await _previewWindow.SetTexturesAsync(texJson);
        }
        catch (Exception ex)
        {
            Log($"テクスチャ送信をスキップ: {ex.Message}");
        }

        Log($"プレビュー描画: {placed.Count} ブロックを送信します。");
        await _previewWindow.RenderAsync(blocksJson);
        _previewWindow.Activate();
    }

    // elements(faces解決済み) から JS へ渡す payload オブジェクトを組み立てる。
    // 各面の texKey を addFaceTex で texMap に集めつつ、from/to/faces(tex,uv,rot)、
    // 要素の2段回転(rotAngle/rotAxis/rotOrigin と rot2Angle/rot2Axis)を載せる。
    private static object BuildElementPayload(
        ModSorter.Clients.ModuleGenerator.PlacedBlock b,
        List<ModSorter.Architect.Generation.BlockTextureProvider.ShapeElement> elements,
        int rotX, int rotY, double rotZ,
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
            rotY = rotY,
            rotZ = rotZ
        };
    }

}
