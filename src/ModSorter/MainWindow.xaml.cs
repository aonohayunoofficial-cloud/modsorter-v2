using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Markdig;
using System.Windows.Threading;



namespace ModSorter;

public partial class MainWindow : Window
{
    private string? _instancePath;
    private string _cfUrl = "";
    private string _mrUrl = "";
    private Settings _settings = new();
    private List<ModEntry> _mods = new();
    private ModEntry? _currentMod;
    private bool _showingTranslation = false;


    public MainWindow()
    {
        InitializeComponent();
        MainTabs.SelectedIndex = 0;

        _settings = Settings.Load();
        ModCache.Load();
        if (!string.IsNullOrEmpty(_settings.InstancePath))
        {
            _instancePath = _settings.InstancePath;
            PathBox.Text = _instancePath;
        }

        if (!string.IsNullOrEmpty(_settings.CurseForgeKeyEnc))
        {
            var cf = string.IsNullOrEmpty(_settings.CurseForgeKeyEnc) ? "未設定" : "保存済み";
            var dl = string.IsNullOrEmpty(_settings.DeepLKeyEnc) ? "未設定" : "保存済み";
            SettingsStatus.Text = $"CurseForge: {cf} / DeepL: {dl}(変更する場合のみ再入力)";
        }

        Log("ModSorter v0.1 を起動しました。");
    }


    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogBox.ScrollToEnd();
    }

    // ===== ナビゲーション =====
    private void NavMods_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 1;
    private void NavCrash_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 2;
        LoadCrashFiles();
    }
    private void NavSettings_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 3;
    private void Back_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 0;

    // ===== フォルダ・設定 =====
    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = ".minecraft フォルダを選択" };
        if (dialog.ShowDialog() == true)
        {
            _instancePath = dialog.FolderName;
            PathBox.Text = _instancePath;
            Log($"フォルダを選択: {_instancePath}");
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings.InstancePath = _instancePath ?? "";
        if (!string.IsNullOrEmpty(CfKeyBox.Password))
            _settings.CurseForgeKeyEnc = Settings.Encrypt(CfKeyBox.Password);
        if (!string.IsNullOrEmpty(DeepLKeyBox.Password))
            _settings.DeepLKeyEnc = Settings.Encrypt(DeepLKeyBox.Password);
        _settings.Save();

        var cfState = string.IsNullOrEmpty(_settings.CurseForgeKeyEnc) ? "未設定" : "保存済み";
        var dlState = string.IsNullOrEmpty(_settings.DeepLKeyEnc) ? "未設定" : "保存済み";
        SettingsStatus.Text = $"保存しました。(CurseForge: {cfState} / DeepL: {dlState})";
        Log("設定を保存しました。");
        CfKeyBox.Password = "";
        DeepLKeyBox.Password = "";
    }
    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "MOD情報とアイコンのキャッシュを全て削除します。\n次回スキャンで全件を再取得します。よろしいですか?",
            "ModSorter - キャッシュ全削除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        int count = ModCache.ClearAll();
        CacheStatus.Text = $"キャッシュを削除しました({count} 件)。";
        Log($"キャッシュを全削除しました({count} 件)。");

        // 表示中のMODのキャッシュ由来データもクリアして見た目を揃える
        foreach (var mod in _mods)
        {
            mod.ModrinthUrl = "";
            mod.CurseForgeUrl = "";
            mod.Body = "";
            mod.IconUrl = "";
            mod.IconFile = "";
            mod.Categories = new();
            mod.CategorySource = "";
            mod.TranslatedHtml = "";
        }
        RefreshModViews();
    }

    private bool _selectionMode = false;

    private void CardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CardList.SelectedItem is not ModEntry mod) return;

        if (_selectionMode)
        {
            // 選択モード中: クリックでチェック切替(詳細は出さない)
            mod.IsSelected = !mod.IsSelected;
            RefreshSelectedList();
            UpdateRefetchSelectedLabel();
            // ListBoxの選択ハイライト自体は使わないので選択を解除しておく
            CardList.SelectedItem = null;
        }
        else
        {
            ShowDetail(mod);
        }
    }

    private void SelectMode_Click(object sender, RoutedEventArgs e)
    {
        _selectionMode = !_selectionMode;

        // 全MODのSelectionModeフラグを更新（カードのチェックボックス表示制御）
        foreach (var m in _mods)
            m.SelectionMode = _selectionMode;

        if (_selectionMode)
        {
            // 選択モードに入る
            SelectModeBtn.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Collapsed;
            SelectionPanel.Visibility = Visibility.Visible;

            RefreshSelectedList();
            UpdateRefetchSelectedLabel();
        }
        else
        {
            // 選択モードを抜ける：全チェック解除して通常表示へ
            foreach (var m in _mods)
                m.IsSelected = false;

            SelectModeBtn.Visibility = Visibility.Visible;
            SelectionPanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var mod in _mods) mod.IsSelected = true;
        RefreshSelectedList();
        UpdateRefetchSelectedLabel();
    }
    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var mod in _mods) mod.IsSelected = false;
        RefreshSelectedList();
        UpdateRefetchSelectedLabel();
    }

    // 選択中MOD一覧を再構築
    private void RefreshSelectedList()
    {
        var selected = _mods.Where(m => m.IsSelected)
                            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                            .ToList();
        SelectedList.ItemsSource = selected;
    }

    // 選択中リストの行クリック → 中央カードへスクロール＋黄枠ハイライト
    private void SelectedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedList.SelectedItem is not ModEntry mod) return;

        // クリックを単発扱いにするため選択状態はリセット
        SelectedList.SelectedItem = null;

        CardList.ScrollIntoView(mod);

        // 仮想化でコンテナ生成が間に合わないことがあるため遅延実行
        Dispatcher.BeginInvoke(new Action(() => HighlightCard(mod)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // 対象カードのcardBorderに黄枠フェードを2回かける
    private void HighlightCard(ModEntry mod)
    {
        var container = CardList.ItemContainerGenerator
                                .ContainerFromItem(mod) as FrameworkElement;
        if (container == null) return;

        var border = FindChild<System.Windows.Controls.Border>(container, "cardBorder");
        if (border == null) return;

        var originalBrush = border.BorderBrush;
        var originalThickness = border.BorderThickness;

        var highlight = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)); // 黄
        border.BorderBrush = highlight;
        border.BorderThickness = new Thickness(3);

        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 0.2,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true,
            RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(2)
        };
        anim.Completed += (_, __) =>
        {
            border.BorderBrush = originalBrush;
            border.BorderThickness = originalThickness;
            highlight.BeginAnimation(
                System.Windows.Media.SolidColorBrush.OpacityProperty, null);
        };

        highlight.BeginAnimation(
            System.Windows.Media.SolidColorBrush.OpacityProperty, anim);
    }

    // 指定名の子要素を再帰検索するヘルパー
    private static T? FindChild<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name)
                return fe;
            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void UpdateRefetchSelectedLabel()
    {
        int n = _mods.Count(m => m.IsSelected);
        RefetchSelectedBtn.Content = $"選択を再取得({n}件)";
        OllamaSelectedBtn.Content = $"Ollama取得({n}件)";
    }

    private async void RefetchSelected_Click(object sender, RoutedEventArgs e)
    {
        var targets = _mods.Where(m => m.IsSelected).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("再取得するMODを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"{targets.Count} 件のMODを再取得します。よろしいですか?",
            "ModSorter - 選択再取得",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        RefetchSelectedBtn.IsEnabled = false;
        SelectModeBtn.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;

        int done = 0, hit = 0;
        foreach (var mod in targets)
        {
            bool ok = await FetchOneAsync(mod);
            if (ok) hit++;
            done++;
            ScanProgress.Value = done * 100.0 / targets.Count;
            ScanStatus.Text = $"再取得中... {done}/{targets.Count}";
        }

        ScanProgress.Value = 100;
        ScanStatus.Text = $"再取得完了: {hit}/{targets.Count} 件ヒット";
        RefetchSelectedBtn.IsEnabled = true;
        SelectModeBtn.IsEnabled = true;
        Log($"選択再取得完了: {hit}/{targets.Count} 件ヒット。");

        RefreshModViews();
        RefreshSelectedList();
    }

    // 取得済みカテゴリを集計して候補リストを作る(案A)
    // CurseForge/Modrinthどちらの体系かは取得時点で決まっているので
    // _mods全体のCategoriesをユニーク化すれば、その出所の体系になる
    private List<string> BuildCategoryCandidates()
    {
        return _mods
            .Where(m => m.Categories != null)
            .SelectMany(m => m.Categories)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // 選択中MODをOllamaでまとめて再分類(直列)
    private async void OllamaSelected_Click(object sender, RoutedEventArgs e)
    {
        var targets = _mods.Where(m => m.IsSelected).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("分類するMODを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 候補カテゴリ(取得済みカテゴリの集計)
        var candidates = BuildCategoryCandidates();
        if (candidates.Count == 0)
        {
            MessageBox.Show(
                "候補となるカテゴリがありません。\n先にスキャンしてAPIカテゴリを取得してください。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Ollamaの起動確認
        Log("Ollamaの起動を確認中...");
        if (!await OllamaClient.IsAvailableAsync())
        {
            MessageBox.Show(
                "Ollamaに接続できません。\nOllamaが起動しているか確認してください(http://localhost:11434)。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Warning);
            Log("Ollama分類中止: 接続不可。");
            return;
        }

        OllamaSelectedBtn.IsEnabled = false;
        RefetchSelectedBtn.IsEnabled = false;
        SelectModeBtn.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;
        ScanProgress.Maximum = targets.Count;

        int done = 0, ok = 0;
        foreach (var mod in targets)
        {
            ScanStatus.Text = $"Ollama分類中: {mod.DisplayName} ({done + 1}/{targets.Count})";
            var result = await OllamaClient.ClassifyAsync(
                mod.DisplayName, mod.Body, candidates);

            if (result != null && result.Count > 0)
            {
                mod.LlmCategories = result;   // INotifyで表示は自動更新
                ok++;
                Log($"Ollama分類: {mod.DisplayName} → {string.Join(", ", result)}");
            }
            else
            {
                Log($"Ollama分類失敗({mod.DisplayName}): {OllamaClient.LastError}");
            }

            done++;
            ScanProgress.Value = done;
        }

        ScanStatus.Text = "";
        ScanProgress.Visibility = Visibility.Collapsed;
        OllamaSelectedBtn.IsEnabled = true;
        RefetchSelectedBtn.IsEnabled = true;
        SelectModeBtn.IsEnabled = true;

        Log($"Ollama分類完了: {targets.Count} 件中 {ok} 件成功。");
        if (ok < targets.Count)
        {
            MessageBox.Show(
                $"{targets.Count} 件中 {ok} 件を分類しました。\n失敗した分はログを確認してください。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        RefreshModViews();
        RefreshSelectedList();
    }
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