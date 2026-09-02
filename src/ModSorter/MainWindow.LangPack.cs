using ModSorter.Clients;
using ModSorter.Models;
using ModSorter.Services;
using System.IO;
using System.Windows;

namespace ModSorter;

// 日本語化パック機能の UI 側。1ファイル9KB以下の規約に合わせ partial へ分割している。
//   MainWindow.LangPack.cs          共通の状態・前提チェック・出力先解決（このファイル）
//   MainWindow.LangPack.Generate.cs 生成ボタン
//   MainWindow.LangPack.Repair.cs   復元漏れ再検査・printf修復・再翻訳
//   MainWindow.LangPack.Cache.cs    翻訳キャッシュの削除・英語固定解除
public partial class MainWindow : Window
{
    private CancellationTokenSource? _langPackCts;

    // 翻訳エンジン名。TranslationCache のファイル名にも使われる。
    private const string LangPackEngine = "deepl";

    // pack.mcmeta に書く pack_format。lang だけのパックは構造がバージョンに依存しないため、
    // LangPackService 側で supported_formats(範囲宣言)も併せて書き出し、
    // 値が合わないバージョンで「非対応」に落ちないようにしている。
    private const int LangPackFormat = 34;

    // UI の「同梱 ja_jp を尊重する」チェック。
    private bool LangPackSkipIfJa => LangPackSkipJaCheck.IsChecked == true;

    // 走査対象の jar パス一覧。
    private List<string> LangPackJarPaths()
        => _mods.Select(m => m.FilePath).ToList();

    // スキャン済みかを確かめる。未スキャンなら警告して false。
    private bool EnsureModsScanned()
    {
        if (_mods != null && _mods.Count > 0) return true;
        MessageBox.Show("先に MOD をスキャンしてください。", "ModSorter",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    // DeepL キーを読み出して初期化する。未設定なら警告して false。
    private bool EnsureDeepLReady()
    {
        var key = Settings.Decrypt(_settings.DeepLKeyEnc);
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show("設定で DeepL API キーを設定してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        DeepLClient.Init(key);
        return true;
    }

    // 生成/再翻訳の実行中でないかを確かめる。
    private bool EnsureLangPackIdle()
    {
        if (_langPackCts == null) return true;
        MessageBox.Show("生成/再翻訳の実行中は操作できません。", "ModSorter",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    // 解析失敗の表示行。総数だけでは救える失敗(壊れた JSON 等)と
    // 救えない失敗(開けない jar)の区別がつかないため、内訳を添える。
    // 崩れた JSON を救済して読めた件数と、中身が空だった件数も並べる。
    private static string FormatSkipLine(LangPackService.LangPackResult r)
        => $"スキップ(解析失敗): {r.SkippedBroken} 件" +
           (r.SkippedBroken > 0
               ? $"(jar {r.SkippedBrokenJars} / langファイル {r.SkippedBrokenEntries})"
               : "") +
           $"\n救済して読めた langファイル: {r.RepairedFiles} 件 / " +
           $"中身が空だった langファイル: {r.EmptyLangFiles} 件";

    // 解析失敗と救済の内訳をログへ出す。どのファイルがなぜ落ちたか、
    // どのファイルを何キー救済したかを残す。
    private void LogSkipDetails(LangPackService.LangPackResult r)
    {
        if (r.SkippedDetails.Count > 0)
        {
            Log($"--- 解析失敗の内訳 {r.SkippedDetails.Count} 件 ---");
            foreach (var d in r.SkippedDetails) Log("  失敗: " + d);
        }
        if (r.RepairDetails.Count > 0)
        {
            Log($"--- 救済・空判定の内訳 {r.RepairDetails.Count} 件 ---");
            foreach (var d in r.RepairDetails) Log("  救済: " + d);
        }
    }

    // 中断ボタン
    private void LangPackCancel_Click(object sender, RoutedEventArgs e)
    {
        _langPackCts?.Cancel();
    }

    // 「用語辞書を開く」ボタン。多義語の訳を固定するための辞書を既定エディタで開く。
    // 例: "Spring" を「バネ」に固定する。ファイルが無ければ既定辞書を作ってから開く。
    // 編集後は保存するだけでよく、次回の生成時に読み直される。
    // 訳語を変えた語は既存キャッシュが優先されるため、「英語固定を解除」で
    // 辞書と食い違う分だけを落としてから再生成すると反映される。
    private void LangPackGlossary_Click(object sender, RoutedEventArgs e)
    {
        GlossaryService.Load(force: true);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = GlossaryService.FilePath,
                UseShellExecute = true
            });
            LangPackStatus.Text =
                $"用語辞書を開きました（{GlossaryService.Count} 語）。" +
                "訳を変えた語は「英語固定を解除」後に再生成すると反映されます。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"用語辞書を開けませんでした。\n{GlossaryService.FilePath}\n{ex.Message}",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // 出力先を決める。既定は対象インスタンスの resourcepacks フォルダ。
    private string ResolveLangPackOutPath()
    {
        // UI でパス指定があればそれを優先
        var custom = LangPackOutPath.Text?.Trim();
        if (!string.IsNullOrEmpty(custom))
        {
            // フォルダ指定ならファイル名を補う
            if (Directory.Exists(custom))
                return Path.Combine(custom, "modsorter_ja_jp.zip");
            return custom;
        }

        var baseDir = !string.IsNullOrEmpty(_instancePath)
            ? Path.Combine(_instancePath, "resourcepacks")
            : Environment.CurrentDirectory;
        return Path.Combine(baseDir, "modsorter_ja_jp.zip");
    }
}
