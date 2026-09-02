using ModSorter.Services;
using System.Windows;

namespace ModSorter;

public partial class MainWindow : Window
{
    // 訳文に指定語を含むキャッシュだけを削除する(部分クリア)。
    // 「春」のような誤訳語を指定すれば、該当原文だけが次回再翻訳される。
    private void LangPackCacheRemoveWord_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureLangPackIdle()) return;

        var word = LangPackCacheWordBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(word))
        {
            MessageBox.Show("削除したい訳語(例: 春)を入力してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int hit = TranslationCache.CountWhereTranslatedContains(LangPackEngine, word);
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

        int removed = TranslationCache.RemoveWhereTranslatedContains(LangPackEngine, word);
        LangPackStatus.Text = $"キャッシュ削除: 「{word}」を含む {removed:N0} 件";
        Log($"翻訳キャッシュ部分削除: 「{word}」 {removed} 件");
        AddActivity($"翻訳キャッシュ部分削除: {removed} 件");
    }

    // 翻訳キャッシュを全削除する。次回生成で全件再翻訳になるため枠消費が大きい。
    private void LangPackCacheClear_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureLangPackIdle()) return;

        TranslationCache.Load(LangPackEngine);
        int count = TranslationCache.Count;

        var ok = MessageBox.Show(
            $"翻訳キャッシュを全削除します({count:N0} 件)。\n" +
            $"保存先: {TranslationCache.PathOf(LangPackEngine)}\n\n" +
            "次回の生成では全ての原文を翻訳し直すため、DeepL 枠を大きく消費します。\n" +
            "続行しますか?",
            "翻訳キャッシュの全削除", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.OK) return;

        int removed = TranslationCache.ClearAll(LangPackEngine);
        LangPackStatus.Text = $"翻訳キャッシュを全削除しました({removed:N0} 件)";
        Log($"翻訳キャッシュ全削除: {removed} 件");
        AddActivity($"翻訳キャッシュ全削除: {removed} 件");
    }

    // 直らないキャッシュを落とすボタン(DeepL枠を使わない)。
    // 訳文＝原文で固定された分(送信失敗フォールバックの残骸)と、
    // 用語辞書の訳語と食い違う分の2種を対象にする。
    // 用語辞書は翻訳の直前に適用されるため、辞書を直しても既存キャッシュが
    // 優先されて何度生成しても直らない。ここで該当分だけ落とすことで、
    // 全消しと違い枠の消費を最小にしたまま新しい訳語を当てられる。
    private async void LangPackCachePurge_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureModsScanned()) return;
        if (!EnsureLangPackIdle()) return;

        bool skipIfJa = LangPackSkipIfJa;
        var result = new LangPackService.LangPackResult();
        var jarPaths = LangPackJarPaths();

        LangPackStatus.Text = "直らないキャッシュを走査中(翻訳送信なし)...";

        var (english, glossary) = await Task.Run(() =>
        {
            var targets = LangPackService.ExtractTargets(jarPaths, skipIfJa, result);
            int a = LangPackService.PurgeUntranslatedCache(targets, LangPackEngine);
            int b = LangPackService.PurgeGlossaryMismatchCache(targets, LangPackEngine);
            return (a, b);
        });

        int total = english + glossary;
        LangPackStatus.Text =
            $"キャッシュ掃除: 英語固定 {english:N0} 件 / 用語辞書ずれ {glossary:N0} 件";
        Log($"英語のまま固定されたキャッシュを削除: {english} 件");
        Log($"用語辞書と食い違う訳のキャッシュを削除: {glossary} 件");
        AddActivity($"翻訳キャッシュ掃除: {total} 件");

        MessageBox.Show(
            $"訳文が原文と同一だったキャッシュ: {english:N0} 件\n" +
            $"用語辞書の訳語が入っていなかったキャッシュ: {glossary:N0} 件\n" +
            "次回の生成でこの分が翻訳し直されます(DeepL 枠を消費)。\n" +
            "printf系(%s %d 等)を含む原文は修復結果なので対象外です。",
            "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
