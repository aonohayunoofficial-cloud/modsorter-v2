using ModSorter.Services;
using System.Text.RegularExpressions;
using System.Windows;

namespace ModSorter;

public partial class MainWindow : Window
{
    // 復元漏れ再検査ボタン(DeepL枠を使わない)。
    // 既存キャッシュを再検査し、プレースホルダが欠けている原文を洗い出してログに出す。
    private async void LangPackRecheck_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureModsScanned()) return;
        if (!EnsureLangPackIdle()) return;

        bool skipIfJa = LangPackSkipIfJa;
        var result = new LangPackService.LangPackResult();
        var jarPaths = LangPackJarPaths();

        LangPackStatus.Text = "再検査中(翻訳送信なし)...";

        List<string> broken = await Task.Run(() =>
        {
            var targets = LangPackService.ExtractTargets(jarPaths, skipIfJa, result);
            return LangPackService.RecheckCache(targets, LangPackEngine);
        });

        LangPackStatus.Text = $"再検査完了: 復元漏れ {broken.Count} 件";
        Log($"=== 復元漏れ再検査(枠消費なし): {broken.Count} 件 ===");
        foreach (var s in broken)
            Log("  復元漏れ: " + s);
        LogSkipDetails(result);

        MessageBox.Show(
            $"復元漏れ(プレースホルダ欠落)の可能性がある原文: {broken.Count} 件\n" +
            FormatSkipLine(result) + "\n" +
            "詳細はログに出力しました。",
            "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // printf系の復元漏れを原形維持で修復するボタン(DeepL枠を使わない)。
    // 再検査で見つかった復元漏れのうち、%s %d %.2f 等を含む原文を原文どおりに上書きする。
    private async void LangPackRepairPrintf_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureModsScanned()) return;
        if (!EnsureLangPackIdle()) return;

        bool skipIfJa = LangPackSkipIfJa;
        var result = new LangPackService.LangPackResult();
        var jarPaths = LangPackJarPaths();

        LangPackStatus.Text = "printf修復中(翻訳送信なし)...";

        List<string> repaired = await Task.Run(() =>
        {
            var targets = LangPackService.ExtractTargets(jarPaths, skipIfJa, result);
            var broken = LangPackService.RecheckCache(targets, LangPackEngine);
            return LangPackService.RepairPrintfPlaceholders(broken, LangPackEngine);
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

    // 復元漏れ(色コード等)を新方式で再翻訳するボタン。
    // 再検査で見つかった復元漏れのうち printf系を除いた分を、
    // キャッシュから消してXMLタグ方式で翻訳し直す。枠消費は対象分のみ。
    private async void LangPackRetranslate_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureModsScanned()) return;
        if (!EnsureLangPackIdle()) return;
        if (!EnsureDeepLReady()) return;

        // 再翻訳も ProtectXml を通るので用語辞書が効く。生成と同じく読み直して、
        // 同一セッション中に辞書を編集した場合も新しい訳語が当たるようにする。
        GlossaryService.Load(force: true);

        bool skipIfJa = LangPackSkipIfJa;
        var result = new LangPackService.LangPackResult();
        var jarPaths = LangPackJarPaths();

        // 対象を洗い出す(printf系は除く。printfは修復ボタンで対応する)
        LangPackStatus.Text = "再検査中...";
        var printfRegex = new Regex(@"%(\d+\$)?\d*(\.\d+)?[sdf]");

        var (allTargets, targetsToRe) = await Task.Run(() =>
        {
            var tg = LangPackService.ExtractTargets(jarPaths, skipIfJa, result);
            var broken = LangPackService.RecheckCache(tg, LangPackEngine);
            return (tg, broken.Where(s => !printfRegex.IsMatch(s)).ToList());
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
        LangPackCancelBtn.IsEnabled = true;
        LangPackProgress.Visibility = Visibility.Visible;
        LangPackProgress.Value = 0;

        try
        {
            int redone = await LangPackService.RetranslateAsync(
                targetsToRe, allTargets, LangPackEngine, result,
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
            LangPackCancelBtn.IsEnabled = false;
            LangPackProgress.Visibility = Visibility.Collapsed;
            _langPackCts?.Dispose();
            _langPackCts = null;
        }
    }
}
