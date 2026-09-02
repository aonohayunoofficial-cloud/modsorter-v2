using ModSorter.Clients;
using ModSorter.Services;
using System.IO;
using System.Windows;

namespace ModSorter;

public partial class MainWindow : Window
{
    // 「日本語化パックを生成」ボタン
    private async void LangPackGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureModsScanned()) return;
        if (!EnsureDeepLReady()) return;

        // 用語辞書を読み込む。初回はファイルが無いので既定辞書が書き出される。
        // 生成のたびに読み直すことで、アプリを再起動しなくても編集内容が反映される。
        GlossaryService.Load(force: true);

        bool skipIfJa = LangPackSkipIfJa;
        var result = new LangPackService.LangPackResult();
        var jarPaths = LangPackJarPaths();

        // 1) 抽出+除外(重いので別スレッド)
        LangPackStatus.Text = "抽出中...";
        List<LangPackService.NamespaceLang> targets = await Task.Run(
            () => LangPackService.ExtractTargets(jarPaths, skipIfJa, result));

        LogSkipDetails(result);

        if (targets.Count == 0)
        {
            LangPackStatus.Text = "不足キーなし(同梱の日本語で完備、または en_us なし)";
            MessageBox.Show(
                $"翻訳が必要なキーがありませんでした。\n" +
                $"除外(同梱 ja_jp が完備): {result.SkippedJaExisting} 件\n" +
                FormatSkipLine(result),
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 2) 見積もり + DeepL 残量照合
        // unique はキャッシュ済みを含む理論値、send は実際に送信する分。
        // 残量と突き合わせるのは send 側。unique で比べると、2回目以降の生成で
        // 送らない分まで数えて残量超過の警告が出てしまう。
        int unique = LangPackService.EstimateChars(targets);
        int send = await Task.Run(
            () => LangPackService.EstimateChars(targets, LangPackEngine));
        var usage = await DeepLClient.GetUsageAsync();

        string usageMsg;
        bool overLimit = false;
        if (usage.HasValue)
        {
            long remaining = usage.Value.Limit - usage.Value.Count;
            usageMsg = $"DeepL 残り {remaining:N0} / 上限 {usage.Value.Limit:N0} 文字";
            if (send > remaining) overLimit = true;
            else if (send > remaining * 0.8) usageMsg += "(残量の80%超の見込み)";
        }
        else
        {
            usageMsg = "DeepL 残量取得に失敗(続行は可能)";
        }

        LangPackUsage.Text = usageMsg;

        var confirm = MessageBox.Show(
            $"翻訳対象: {result.NamespaceCount} 名前空間 / {result.EntryCount} エントリ\n" +
            $"ユニーク原文: {unique:N0} 文字\n" +
            $"今回送信する分(キャッシュ済みを除く): {send:N0} 文字\n" +
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
                targets, LangPackEngine, result,
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

            var failMsg = result.FailedEntries > 0
                ? $"⚠ 送信失敗: {result.FailedEntries} 件({result.FailedBatches} バッチ)\n" +
                  $"　原因: {result.LastApiError}\n" +
                  (result.AbortedByApiError
                      ? "　連続失敗のため翻訳を打ち切りました。失敗分はキャッシュに書いて\n" +
                        "　いないので、原因解消後にもう一度生成すれば翻訳されます。\n"
                      : "　失敗分はキャッシュに書いていないので、次回生成で再送信されます。\n")
                : "";

            var summary =
                $"生成完了\n" +
                $"MOD: {result.ModCount} / 名前空間: {result.NamespaceCount} / " +
                $"翻訳したエントリ: {result.EntryCount}\n" +
                $"同梱 jar(jar-in-jar)の走査: {result.NestedJars} 件\n" +
                $"同梱訳の引き継ぎ: {result.PreservedEntries} エントリ\n" +
                $"部分補完した名前空間: {result.PartialNamespaces} 件\n" +
                $"用語辞書: {GlossaryService.Count} 語を適用\n" +
                $"翻訳文字数: {result.TranslatedChars:N0}\n" +
                $"除外(同梱 ja_jp が完備): {result.SkippedJaExisting} 件\n" +
                FormatSkipLine(result) + "\n" +
                $"復元漏れ警告: {result.RestoreWarnings} 件\n" +
                failMsg +
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
            LangPackProgress.Visibility = Visibility.Collapsed;
            _langPackCts?.Dispose();
            _langPackCts = null;
        }
    }
}
