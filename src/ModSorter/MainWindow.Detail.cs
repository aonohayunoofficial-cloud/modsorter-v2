using System.Diagnostics;
using System.Windows;
using Markdig;
using ModSorter.Clients;

namespace ModSorter;

public partial class MainWindow : Window
{
    // ===== 詳細表示・再取得・翻訳・URL =====
    private async void ShowDetail(ModEntry mod)
    {
        _currentMod = mod;
        _showingTranslation = false;
        TranslateBtn.Content = "翻訳";
        DetailName.Text = mod.DisplayName;
        DetailId.Text = $"ID: {mod.ModId}";
        DetailVersion.Text = $"バージョン: {mod.Version}";
        DetailLoader.Text = $"ローダー: {mod.Loader}";
        string apiLine = mod.Categories.Count == 0
            ? "カテゴリ: ―"
            : $"カテゴリ ({mod.CategorySource}): {mod.CategoryText}";
        string llmLine = mod.HasLlmCategory
            ? $"\nLLM分類: {string.Join(", ", mod.LlmCategories)}"
            : "";
        DetailCategory.Text = apiLine + llmLine;

        _mrUrl = mod.ModrinthUrl;
        _cfUrl = mod.CurseForgeUrl;
        UrlModrinth.Text = string.IsNullOrEmpty(mod.ModrinthUrl)
            ? "Modrinth: ―" : $"Modrinth: {mod.ModrinthUrl}";
        UrlCurseForge.Text = string.IsNullOrEmpty(mod.CurseForgeUrl)
            ? "CurseForge: ―" : $"CurseForge: {mod.CurseForgeUrl}";

        await ShowBodyAsync(mod);
        Log($"選択: {mod.FileName}");
    }

    private async void Refetch_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMod == null)
        {
            MessageBox.Show("先にMODを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var mod = _currentMod;

        RefetchBtn.IsEnabled = false;
        RefetchBtn.Content = "取得中...";
        Log($"再取得: {mod.FileName}");

        bool hit = await FetchOneAsync(mod);

        RefetchBtn.IsEnabled = true;
        RefetchBtn.Content = "このMODを再取得";

        // 表示を更新
        ShowDetail(mod);
        RefreshModViews();

        Log(hit ? $"再取得完了(ヒット): {mod.FileName}"
                : $"再取得完了(該当なし): {mod.FileName}");
    }

    private async Task ShowBodyAsync(ModEntry mod)
    {
        try
        {
            await DetailWeb.EnsureCoreWebView2Async();

            string innerHtml;
            if (string.IsNullOrEmpty(mod.Body))
            {
                innerHtml = "<p style='color:#9A8F7E'>(説明なし / 未照合)</p>";
            }
            else if (mod.BodyIsHtml)
            {
                innerHtml = mod.Body;
            }
            else
            {
                // Markdown を HTML に変換
                innerHtml = Markdown.ToHtml(mod.Body);
            }

            // ダークテーマに合わせたページ全体
            string html = $@"<!DOCTYPE html>
                <html><head><meta charset='utf-8'>
                <style>
                body {{ background:#1E1B17; color:#E8E0D4; font-family:sans-serif;
                font-size:13px; padding:10px; margin:0; }}
                a {{ color:#6FA8DC; }}
                img {{ max-width:100%; height:auto; }}
                h1,h2,h3 {{ color:#7FB238; }}
                code,pre {{ background:#2B2620; padding:2px 4px; border-radius:3px; }}
                </style></head>
                <body>{innerHtml}</body></html>";

            DetailWeb.NavigateToString(html);
        }
        catch (Exception ex)
        {
            Log($"説明の表示に失敗: {ex.Message}");
        }
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMod == null) return;
        var mod = _currentMod;

        // 既に翻訳表示中なら原文に戻す
        if (_showingTranslation)
        {
            _showingTranslation = false;
            TranslateBtn.Content = "翻訳";
            await ShowBodyAsync(mod);
            return;
        }

        if (string.IsNullOrEmpty(mod.Body))
        {
            Log("翻訳対象の本文がありません。");
            return;
        }

        // DeepL初期化
        var key = Settings.Decrypt(_settings.DeepLKeyEnc);
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show("設定でDeepL APIキーを保存してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!DeepLClient.IsReady) DeepLClient.Init(key);

        // キャッシュ済みならそれを使う
        string? translated = !string.IsNullOrEmpty(mod.TranslatedHtml)
            ? mod.TranslatedHtml
            : null;

        if (translated == null)
        {
            TranslateBtn.Content = "翻訳中...";
            TranslateBtn.IsEnabled = false;

            // 表示用HTML(本文部分)を作って翻訳に投げる
            string innerHtml = mod.BodyIsHtml ? mod.Body : Markdig.Markdown.ToHtml(mod.Body);
            translated = await DeepLClient.TranslateHtmlAsync(innerHtml);

            TranslateBtn.IsEnabled = true;

            if (translated == null)
            {
                Log($"翻訳失敗: {DeepLClient.LastError}");
                TranslateBtn.Content = "翻訳";
                MessageBox.Show($"翻訳に失敗しました。\n{DeepLClient.LastError}", "ModSorter",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            mod.TranslatedHtml = translated; // セッション内キャッシュ
        }

        // 翻訳HTMLをダークテーマで表示
        string page = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'>
<style>
  body {{ background:#1E1B17; color:#E8E0D4; font-family:sans-serif;
          font-size:13px; padding:10px; margin:0; }}
  a {{ color:#6FA8DC; }}
  img {{ max-width:100%; height:auto; }}
  h1,h2,h3 {{ color:#7FB238; }}
  code,pre {{ background:#2B2620; padding:2px 4px; border-radius:3px; }}
</style></head>
<body>{translated}</body></html>";

        await DetailWeb.EnsureCoreWebView2Async();
        DetailWeb.NavigateToString(page);
        _showingTranslation = true;
        TranslateBtn.Content = "原文";
        Log($"翻訳表示: {mod.FileName}");
    }

    private void UrlCf_Click(object sender, RoutedEventArgs e) => OpenUrl(_cfUrl);
    private void UrlMr_Click(object sender, RoutedEventArgs e) => OpenUrl(_mrUrl);

    private void OpenUrl(string url)
    {
        if (url.StartsWith("http"))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
