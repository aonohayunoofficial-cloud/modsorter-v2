using ModSorter.Architect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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


    // Ponder 隣接ルールのキャッシュ。重い解析(178件)を初回だけ行う。
    private string? _ponderRulesCache;          // 抽出済みルール文(全許可ブロック分の素)
    private Dictionary<string, ModSorter.Architect.Generation.PonderRuleExtractor.BlockStat>?
        _ponderStatsCache;                       // 解析済み統計(許可リストが変わっても使い回せる)
    private string _ponderCacheKey = "";         // 構成フィンガープリント(jarパス+更新日時)

    // Create本体jarのフィンガープリント(パス + 最終更新日時)。
    private static string ComputePonderKey(string? createJar)
    {
        if (string.IsNullOrEmpty(createJar) || !System.IO.File.Exists(createJar))
            return "";
        try
        {
            var fi = new System.IO.FileInfo(createJar);
            return $"{fi.FullName}|{fi.LastWriteTimeUtc.Ticks}";
        }
        catch { return createJar; }
    }

    // 許可ブロックの隣接ルールを返す。初回のみ Ponder を解析し、以降はキャッシュを使う。
    // 構成(Create本体jar)が変わっていればキャッシュを破棄して再解析する。
    private string GetPonderAdjacencyRules(
        List<string> modJars, IEnumerable<string> allowedIds)
    {
        try
        {
            string? createJar = modJars.FirstOrDefault(j =>
            {
                string fn = System.IO.Path.GetFileName(j).ToLowerInvariant();
                return fn.StartsWith("create-") && !fn.Contains("aeronautics");
            });

            string key = ComputePonderKey(createJar);
            if (string.IsNullOrEmpty(key)) return "";

            // 構成が変わっていたら統計キャッシュを破棄。
            if (key != _ponderCacheKey)
            {
                _ponderStatsCache = null;
                _ponderCacheKey = key;
            }

            // 統計が未キャッシュなら解析する(ここが重い処理。初回 or 再スキャン時のみ)。
            if (_ponderStatsCache == null)
            {
                Log("Ponder を解析中...(初回のみ・数秒)");
                var entries = ModSorter.Architect.Generation.StructureNbtReader
                    .ListPonderNbtEntries(createJar!, "create");

                var structures =
                    new List<(string, ModSorter.Architect.Generation.StructureNbtReader.Structure)>();
                foreach (var ep in entries)
                {
                    try
                    {
                        string scene = ep.Replace("assets/create/ponder/", "")
                                         .Replace(".nbt", "");
                        var st = ModSorter.Architect.Generation.StructureNbtReader
                            .ReadFromJar(createJar!, ep);
                        structures.Add((scene, st));
                    }
                    catch { /* 個別失敗は無視 */ }
                }

                _ponderStatsCache = ModSorter.Architect.Generation.PonderRuleExtractor
                    .Analyze(structures);
                Log($"Ponder解析完了: {structures.Count} シーン / {_ponderStatsCache.Count} ブロック種。");
            }

            // ルール文は許可リストに依存するので毎回作る(軽い処理)。
            string rules = ModSorter.Architect.Generation.PonderRuleExtractor.ToRuleText(
                _ponderStatsCache, allowedIds,
                perBlockDirs: 6, topPerDir: 2, minCount: 1);
            _ponderRulesCache = rules;
            return rules;
        }
        catch (Exception ex)
        {
            Log($"Ponder隣接ルール抽出をスキップ: {ex.Message}");
            return "";
        }
    }

    // 「Ponder再スキャン」ボタン。キャッシュを破棄して次回生成時に再解析させる。
    private void MachineRescanPonder_Click(object sender, RoutedEventArgs e)
    {
        _ponderStatsCache = null;
        _ponderCacheKey = "";
        _ponderRulesCache = null;
        MachineStatus.Text = "Ponderキャッシュを破棄しました。次回生成時に再スキャンします。";
        Log("Ponderキャッシュを破棄(手動再スキャン)。");
    }

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

                // [一時確認] press/mixer/basin/belt等のプロパティをログ出力。仕様確定後に削除する。
                foreach (var checkId in new[]
                {
                    "create:mechanical_press", "create:mechanical_mixer", "create:basin",
                    "create:depot", "create:belt", "create:andesite_funnel", "create:chute",
                })
                {
                    // fullPalette(生)に存在するか。存在すればプロパティ、無ければ「パレット自体に無い」。
                    if (fullPalette.TryGetValue(checkId, out var pp))
                    {
                        string dump = pp.Count == 0
                            ? "(プロパティなし=向き等を持たないブロック)"
                            : string.Join(", ",
                                pp.Select(kv => $"{kv.Key}=[{string.Join("|", kv.Value)}]"));
                        Log($"[プロパティ] {checkId}: fullPaletteにあり / {dump}");
                    }
                    else
                    {
                        Log($"[プロパティ] {checkId}: fullPalette自体に無い(blockstates未抽出)");
                    }
                }

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

            string outPath = DiagPath("module_machine.nbt");
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
}
