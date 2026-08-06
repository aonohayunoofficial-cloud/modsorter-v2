using ModSorter.Clients;
using ModSorter.Models;
using ModSorter.Services;
using System.IO;
using System.Windows;

namespace ModSorter;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _langPackCts;

    // 1.21.1 の pack_format。バージョン追従できるよう定数で保持。
    private const int LangPackFormat = 34;

    // 「日本語化パックを生成」ボタン
    private async void LangPackGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _mods.Count == 0)
        {
            MessageBox.Show("先に MOD をスキャンしてください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // DeepL キー確認
        var deepLKey = Settings.Decrypt(_settings.DeepLKeyEnc);
        if (string.IsNullOrEmpty(deepLKey))
        {
            MessageBox.Show("設定で DeepL API キーを設定してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DeepLClient.Init(deepLKey);

        // 用語辞書を読み込む。初回はファイルが無いので既定辞書が書き出される。
        // 生成のたびに読み直すことで、アプリを再起動しなくても編集内容が反映される。
        GlossaryService.Load(force: true);

        bool skipIfJa = LangPackSkipJaCheck.IsChecked == true;
        const string engine = "deepl";

        var result = new LangPackService.LangPackResult();
        var jarPaths = _mods.Select(m => m.FilePath).ToList();

        // 1) 抽出+除外(重いので別スレッド)
        LangPackStatus.Text = "抽出中...";
        List<LangPackService.NamespaceLang> targets = await Task.Run(
            () => LangPackService.ExtractTargets(jarPaths, skipIfJa, result));

        if (targets.Count == 0)
        {
            LangPackStatus.Text = "不足キーなし(同梱の日本語で完備、または en_us なし)";
            MessageBox.Show(
                $"翻訳が必要なキーがありませんでした。\n" +
                $"除外(同梱 ja_jp が完備): {result.SkippedJaExisting} 件",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 2) 見積もり + DeepL 残量照合
        int estimate = LangPackService.EstimateChars(targets);
        var usage = await DeepLClient.GetUsageAsync();

        string usageMsg;
        bool overLimit = false;
        if (usage.HasValue)
        {
            long remaining = usage.Value.Limit - usage.Value.Count;
            usageMsg = $"DeepL 残り {remaining:N0} / 上限 {usage.Value.Limit:N0} 文字";
            if (estimate > remaining) overLimit = true;
            else if (estimate > remaining * 0.8) usageMsg += "(残量の80%超の見込み)";
        }
        else
        {
            usageMsg = "DeepL 残量取得に失敗(続行は可能)";
        }

        LangPackUsage.Text = usageMsg;

        var confirm = MessageBox.Show(
            $"翻訳対象: {result.NamespaceCount} 名前空間 / {result.EntryCount} エントリ\n" +
            $"見積もり文字数(ユニーク): {estimate:N0}\n" +
            $"{usageMsg}\n" +
            (overLimit ? "\n⚠ 残量を超える見込みです。続行しますか?" : "\n生成を開始しますか?"),
            overLimit ? "DeepL 残量超過の警告" : "生成確認",
            MessageBoxButton.OKCancel,
            overLimit ? MessageBoxImage.Warning : MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        // 出力先を先に確定し、既存ファイルがあれば上書き確認する（翻訳前に確認する）。
        var outPath = ResolveLangPackOutPath();
        if (File.Exists(outPath))
        {
            var ow = MessageBox.Show(
                $"既存のパックを上書きします。よろしいですか?\n{outPath}",
                "上書き確認", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (ow != MessageBoxResult.OK) return;
        }

        // 3) 翻訳
        _langPackCts = new CancellationTokenSource();
        LangPackGenBtn.IsEnabled = false;
        LangPackCancelBtn.IsEnabled = true;
        LangPackProgress.Visibility = Visibility.Visible;
        LangPackProgress.Value = 0;

        try
        {
            var translations = await LangPackService.TranslateAsync(
                targets, engine, result,
                (done, total, msg) => Dispatcher.Invoke(() =>
                {
                    LangPackProgress.Value = total == 0 ? 100 : done * 100.0 / total;
                    LangPackStatus.Text = msg;
                }),
                _langPackCts.Token);

            // 4) パック生成（出力先は翻訳前に確定済みの outPath を使う）
            LangPackStatus.Text = "パック生成中...";
            await Task.Run(() => LangPackService.BuildPack(
                targets, translations, outPath, LangPackFormat, result));

            LangPackProgress.Value = 100;
            LangPackStatus.Text = "完了";

            var summary =
                $"生成完了\n" +
                $"MOD: {result.ModCount} / 名前空間: {result.NamespaceCount} / " +
                $"翻訳したエントリ: {result.EntryCount}\n" +
                $"同梱訳の引き継ぎ: {result.PreservedEntries} エントリ\n" +
                $"部分補完した名前空間: {result.PartialNamespaces} 件\n" +
                $"用語辞書: {GlossaryService.Count} 語を適用\n" +
                $"翻訳文字数: {result.TranslatedChars:N0}\n" +
                $"除外(同梱 ja_jp が完備): {result.SkippedJaExisting} 件\n" +
                $"スキップ(解析失敗): {result.SkippedBroken} 件\n" +
                $"復元漏れ警告: {result.RestoreWarnings} 件\n" +
                $"出力先: {result.OutputPath}";
            Log(summary);
            // 復元漏れした原文をログに一覧出力(枠消費なし、原因特定用)
            if (result.RestoreWarningSources.Count > 0)
            {
                Log($"--- 復元漏れした原文 {result.RestoreWarningSources.Count} 件 ---");
                foreach (var s in result.RestoreWarningSources)
                    Log("  復元漏れ: " + s);
            }
            AddActivity($"日本語化パック生成: {result.NamespaceCount} 名前空間");
            MessageBox.Show(summary, "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            LangPackStatus.Text = "中断しました(翻訳済み分はキャッシュ保存済み)";
            Log("日本語化パック生成を中断しました。");
        }
        catch (Exception ex)
        {
            LangPackStatus.Text = "エラー";
            Log("日本語化パック生成でエラー: " + ex.Message);
            MessageBox.Show("エラー: " + ex.Message, "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LangPackGenBtn.IsEnabled = true;
            LangPackCancelBtn.IsEnabled = false;
            _langPackCts?.Dispose();
            _langPackCts = null;
        }
    }

    // 中断ボタン
    private void LangPackCancel_Click(object sender, RoutedEventArgs e)
    {
        _langPackCts?.Cancel();
    }

    // 復元漏れ再検査ボタン(DeepL枠を使わない)。
    // 既存キャッシュを再検査し、プレースホルダが欠けている原文を洗い出してログに出す。
    private async void LangPackRecheck_Click(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _mods.Count == 0)
        {
            MessageBox.Show("先に MOD をスキャンしてください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool skipIfJa = LangPackSkipJaCheck.IsChecked == true;
        const string engine = "deepl";

        var result = new LangPackService.LangPackResult();
        var jarPaths = _mods.Select(m => m.FilePath).ToList();

        LangPackStatus.Text = "再検査中(翻訳送信なし)...";

        List<string> broken = await Task.Run(() =>
        {
            var targets = LangPackService.ExtractTargets(jarPaths, skipIfJa, result);
            return LangPackService.RecheckCache(targets, engine);
        });

        LangPackStatus.Text = $"再検査完了: 復元漏れ {broken.Count} 件";
        Log($"=== 復元漏れ再検査(枠消費なし): {broken.Count} 件 ===");
        foreach (var s in broken)
            Log("  復元漏れ: " + s);

        MessageBox.Show(
            $"復元漏れ(プレースホルダ欠落)の可能性がある原文: {broken.Count} 件\n" +
            "詳細はログに出力しました。",
            "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // printf系の復元漏れを原形維持で修復するボタン(DeepL枠を使わない・案2)。
    // 再検査で見つかった復元漏れのうち、%s %d 等を含む原文を原文どおりに上書きする。
    private async void LangPackRepairPrintf_Click(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _mods.Count == 0)
        {
            MessageBox.Show("先に MOD をスキャンしてください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool skipIfJa = LangPackSkipJaCheck.IsChecked == true;
        const string engine = "deepl";

        var result = new LangPackService.LangPackResult();
        var jarPaths = _mods.Select(m => m.FilePath).ToList();

        LangPackStatus.Text = "printf修復中(翻訳送信なし)...";

        List<string> repaired = await Task.Run(() =>
        {
            var targets = LangPackService.ExtractTargets(jarPaths, skipIfJa, result);
            var broken = LangPackService.RecheckCache(targets, engine);
            return LangPackService.RepairPrintfPlaceholders(broken, engine);
        });

        LangPackStatus.Text = $"printf修復完了: {repaired.Count} 件";
        Log($"=== printf復元漏れ修復(枠消費なし): {repaired.Count} 件 ===");
        foreach (var s in repaired)
            Log("  修復(原形維持): " + s);

        MessageBox.Show(
            $"printf系プレースホルダを原形維持で修復: {repaired.Count} 件\n" +
            "この後、生成し直すと反映されます(枠消費なし)。\n" +
            "詳細はログに出力しました。",
            "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // 復元漏れ(色コード等)を新方式で再翻訳するボタン(案1-B)。
    // 再検査で見つかった復元漏れのうち printf系を除いた分を、
    // キャッシュから消してXMLタグ方式で翻訳し直す。枠消費は対象分のみ。
    private async void LangPackRetranslate_Click(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _mods.Count == 0)
        {
            MessageBox.Show("先に MOD をスキャンしてください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var deepLKey = Settings.Decrypt(_settings.DeepLKeyEnc);
        if (string.IsNullOrEmpty(deepLKey))
        {
            MessageBox.Show("設定で DeepL API キーを設定してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DeepLClient.Init(deepLKey);

        // 再翻訳も ProtectXml を通るので用語辞書が効く。生成と同じく読み直して、
        // 同一セッション中に辞書を編集した場合も新しい訳語が当たるようにする。
        GlossaryService.Load(force: true);

        bool skipIfJa = LangPackSkipJaCheck.IsChecked == true;
        const string engine = "deepl";

        var result = new LangPackService.LangPackResult();
        var jarPaths = _mods.Select(m => m.FilePath).ToList();

        // 対象を洗い出す(printf系は除く。printfは案2の修復ボタンで対応済み)
        LangPackStatus.Text = "再検査中...";
        var printfRegex = new System.Text.RegularExpressions.Regex(@"%(\d+\$)?[sd]");

        List<string> targetsToRe = await Task.Run(() =>
        {
            var tg = LangPackService.ExtractTargets(jarPaths, skipIfJa, result);
            var broken = LangPackService.RecheckCache(tg, engine);
            // printf系を含むものは除外(色コード等だけを対象)
            return broken.Where(s => !printfRegex.IsMatch(s)).ToList();
        });

        if (targetsToRe.Count == 0)
        {
            LangPackStatus.Text = "再翻訳対象なし";
            MessageBox.Show("再翻訳が必要な復元漏れ(色コード等)はありませんでした。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int estChars = targetsToRe.Sum(s => s.Length);
        var confirm = MessageBox.Show(
            $"色コード等の復元漏れを新方式で再翻訳します。\n" +
            $"対象: {targetsToRe.Count} 件 / 約 {estChars:N0} 文字\n" +
            "DeepL 枠を対象分だけ消費します。続行しますか?",
            "再翻訳の確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        _langPackCts = new CancellationTokenSource();
        LangPackProgress.Visibility = Visibility.Visible;
        LangPackProgress.Value = 0;

        try
        {
            var reTargets = await Task.Run(
                () => LangPackService.ExtractTargets(jarPaths, skipIfJa,
                    new LangPackService.LangPackResult()));

            int redone = await LangPackService.RetranslateAsync(
                targetsToRe, reTargets, engine, result,
                (done, total, msg) => Dispatcher.Invoke(() =>
                {
                    LangPackProgress.Value = total == 0 ? 100 : done * 100.0 / total;
                    LangPackStatus.Text = msg;
                }),
                _langPackCts.Token);

            LangPackProgress.Value = 100;
            LangPackStatus.Text = $"再翻訳完了: {redone} 件(復元漏れ {result.RestoreWarnings} 件)";
            Log($"=== 色コード再翻訳(新方式): {redone} 件 / 残復元漏れ {result.RestoreWarnings} 件 ===");
            MessageBox.Show(
                $"再翻訳完了: {redone} 件\n" +
                $"再翻訳後の復元漏れ: {result.RestoreWarnings} 件\n" +
                "この後、生成し直すと反映されます。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            LangPackStatus.Text = "再翻訳を中断しました(済み分はキャッシュ保存済み)";
        }
        catch (Exception ex)
        {
            LangPackStatus.Text = "エラー";
            Log("再翻訳でエラー: " + ex.Message);
            MessageBox.Show("エラー: " + ex.Message, "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LangPackProgress.Visibility = Visibility.Collapsed;
            _langPackCts?.Dispose();
            _langPackCts = null;
        }
    }

    // 訳文に指定語を含むキャッシュだけを削除する(部分クリア)。
    // 「春」のような誤訳語を指定すれば、該当原文だけが次回再翻訳される。
    private void LangPackCacheRemoveWord_Click(object sender, RoutedEventArgs e)
    {
        if (_langPackCts != null)
        {
            MessageBox.Show("生成/再翻訳の実行中は操作できません。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        const string engine = "deepl";
        var word = LangPackCacheWordBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(word))
        {
            MessageBox.Show("削除したい訳語(例: 春)を入力してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int hit = TranslationCache.CountWhereTranslatedContains(engine, word);
        if (hit == 0)
        {
            LangPackStatus.Text = $"「{word}」を含む訳はキャッシュにありません";
            return;
        }

        var ok = MessageBox.Show(
            $"訳文に「{word}」を含むキャッシュ {hit:N0} 件を削除します。\n" +
            "次回の生成で、この分だけ翻訳し直されます(DeepL 枠を消費)。\n" +
            "続行しますか?",
            "翻訳キャッシュの部分削除", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (ok != MessageBoxResult.OK) return;

        int removed = TranslationCache.RemoveWhereTranslatedContains(engine, word);
        LangPackStatus.Text = $"キャッシュ削除: 「{word}」を含む {removed:N0} 件";
        Log($"翻訳キャッシュ部分削除: 「{word}」 {removed} 件");
        AddActivity($"翻訳キャッシュ部分削除: {removed} 件");
    }

    // 翻訳キャッシュを全削除する。次回生成で全件再翻訳になるため枠消費が大きい。
    private void LangPackCacheClear_Click(object sender, RoutedEventArgs e)
    {
        if (_langPackCts != null)
        {
            MessageBox.Show("生成/再翻訳の実行中は操作できません。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        const string engine = "deepl";
        TranslationCache.Load(engine);
        int count = TranslationCache.Count;

        var ok = MessageBox.Show(
            $"翻訳キャッシュを全削除します({count:N0} 件)。\n" +
            $"保存先: {TranslationCache.PathOf(engine)}\n\n" +
            "次回の生成では全ての原文を翻訳し直すため、DeepL 枠を大きく消費します。\n" +
            "続行しますか?",
            "翻訳キャッシュの全削除", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.OK) return;

        int removed = TranslationCache.ClearAll(engine);
        LangPackStatus.Text = $"翻訳キャッシュを全削除しました({removed:N0} 件)";
        Log($"翻訳キャッシュ全削除: {removed} 件");
        AddActivity($"翻訳キャッシュ全削除: {removed} 件");
    }

    // 「用語辞書を開く」ボタン。多義語の訳を固定するための辞書を既定エディタで開く。
    // 例: "Spring" を「バネ」に固定する。ファイルが無ければ既定辞書を作ってから開く。
    // 編集後は保存するだけでよく、次回の生成時に読み直される。
    // 用語を変えた語は、キャッシュを消さないと前回の訳が使われる点だけ添える。
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
                "訳を変えた語は「キャッシュを消去」後に再生成すると反映されます。";
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
