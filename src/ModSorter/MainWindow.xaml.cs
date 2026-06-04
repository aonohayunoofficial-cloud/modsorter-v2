using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Markdig;


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


    // ===== Mods スキャン =====
    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_instancePath))
        {
            MessageBox.Show("先に設定で .minecraft フォルダを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var modsDir = Path.Combine(_instancePath, "mods");
        if (!Directory.Exists(modsDir))
        {
            Log("mods\\ フォルダが見つかりません。");
            return;
        }

        var jars = Directory.GetFiles(modsDir, "*.jar");
        _mods = jars.Select(JarReader.Read).ToList();
        RefreshModViews();
        Log($"{_mods.Count} 個の .jar を読み取りました。オンライン照合を開始します...");

        // SHA1とファイル情報を先に取得してModEntryに保持
        foreach (var mod in _mods)
        {
            mod.Sha1 = ModrinthClient.Sha1(mod.FilePath);
            try
            {
                var fi = new FileInfo(mod.FilePath);
                mod.FileSize = fi.Length;
                mod.FileCreated = fi.CreationTime;
                mod.FileModified = fi.LastWriteTime;
            }
            catch { }
        }

        // キャッシュ適用: ヒットしたものはAPI対象から外す
        var toFetch = new List<ModEntry>();
        int fromCache = 0;
        foreach (var mod in _mods)
        {
            var c = ModCache.Get(mod.Sha1);
            if (c != null)
            {
                mod.ModrinthUrl = c.ModrinthUrl;
                mod.CurseForgeUrl = c.CurseForgeUrl;
                mod.Body = c.Body;
                mod.BodyIsHtml = c.BodyIsHtml;
                mod.IconUrl = c.IconUrl;
                mod.IconFile = (!string.IsNullOrEmpty(c.IconFile) && File.Exists(c.IconFile))
                    ? c.IconFile : "";
                mod.Categories = c.Categories ?? new();
                mod.CategorySource = c.CategorySource;
                fromCache++;
            }
            else
            {
                toFetch.Add(mod);
            }
        }
        RefreshModViews();
        Log($"キャッシュ適用: {fromCache} 件。新規照合対象: {toFetch.Count} 件。");

        if (toFetch.Count == 0)
        {
            ScanStatus.Text = $"完了(全てキャッシュ): {_mods.Count} 件";
            ScanProgress.Visibility = Visibility.Collapsed;
            ModCache.Save();
            return;
        }

        // 進捗UIを表示
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;
        ScanStatus.Text = "照合中...";

        int total = toFetch.Count;
        var lockObj = new object();
        int mrMatched = 0, cfMatched = 0, done = 0;

        var cfKey = Settings.Decrypt(_settings.CurseForgeKeyEnc);
        bool useCf = !string.IsNullOrEmpty(cfKey);
        if (useCf) CurseForgeClient.Init(cfKey);
        else Log("CurseForge APIキー未設定のため、Modrinthのみ照合します。");

        int grandTotal = total + (useCf ? total : 0);

        void Bump()
        {
            int cur;
            lock (lockObj) cur = ++done;
            Dispatcher.Invoke(() =>
            {
                ScanProgress.Value = grandTotal == 0 ? 100 : (cur * 100.0 / grandTotal);
                int shown = useCf ? (cur + 1) / 2 : cur;
                if (shown > total) shown = total;
                ScanStatus.Text = $"照合中... {shown}/{total}";
            });
        }

        var mrSem = new SemaphoreSlim(5);
        var cfSem = new SemaphoreSlim(3);

        var mrTasks = toFetch.Select(async mod =>
        {
            await mrSem.WaitAsync();
            try
            {
                var r = await ModrinthClient.GetByHashAsync(mod.FilePath);
                if (r != null)
                {
                    mod.ModrinthUrl = r.Url;
                    mod.Body = r.Body;
                    mod.BodyIsHtml = false;
                    if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                    // カテゴリはCurseForge優先。CFがまだ設定していなければModrinthで埋める
                    if (mod.Categories.Count == 0 && r.Categories.Count > 0)
                    {
                        mod.Categories = r.Categories;
                        mod.CategorySource = "Modrinth";
                    }
                    lock (lockObj) mrMatched++;
                }

            }
            finally { mrSem.Release(); Bump(); }
        });

        IEnumerable<Task> cfTasks = Array.Empty<Task>();
        if (useCf)
        {
            cfTasks = toFetch.Select(async mod =>
            {
                await cfSem.WaitAsync();
                try
                {
                    var r = await CurseForgeClient.GetByFingerprintAsync(mod.FilePath);
                    if (r != null && !string.IsNullOrEmpty(r.Url))
                    {
                        mod.CurseForgeUrl = r.Url;
                        if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                        if (string.IsNullOrEmpty(mod.Body))
                        {
                            if (!string.IsNullOrEmpty(r.DescriptionHtml))
                            {
                                mod.Body = r.DescriptionHtml;
                                mod.BodyIsHtml = true;
                            }
                            else
                            {
                                mod.Body = r.Summary;
                                mod.BodyIsHtml = false;
                            }
                        }
                        // CurseForgeのカテゴリを優先採用(Modrinthが設定済みでも上書き)
                        if (r.Categories.Count > 0)
                        {
                            mod.Categories = r.Categories;
                            mod.CategorySource = "CurseForge";
                        }
                        lock (lockObj) cfMatched++;
                    }

                }
                finally { cfSem.Release(); Bump(); }
            });
        }

        await Task.WhenAll(mrTasks.Concat(cfTasks));

        // アイコンをローカル保存
        ScanStatus.Text = "アイコンを保存中...";
        foreach (var mod in toFetch)
        {
            if (!string.IsNullOrEmpty(mod.IconUrl))
                mod.IconFile = await ModCache.EnsureIconAsync(mod.Sha1, mod.IconUrl);
        }

        // キャッシュに保存
        foreach (var mod in toFetch)
        {
            ModCache.Put(new CacheEntry
            {
                Sha1 = mod.Sha1,
                ModId = mod.ModId,
                Version = mod.Version,
                Loader = mod.Loader,
                ModrinthUrl = mod.ModrinthUrl,
                CurseForgeUrl = mod.CurseForgeUrl,
                Body = mod.Body,
                BodyIsHtml = mod.BodyIsHtml,
                IconUrl = mod.IconUrl,
                IconFile = mod.IconFile,
                Categories = mod.Categories,
                CategorySource = mod.CategorySource
            });
        }
        ModCache.Save();

        ScanStatus.Text = $"完了: MR {mrMatched} / CF {cfMatched}(新規 {total} 件)";
        ScanProgress.Value = 100;
        Log($"照合完了: Modrinth {mrMatched} 件、CurseForge {cfMatched} 件。キャッシュ保存済み。");
        RefreshModViews();
    }

    private string _viewMode = "medium";

    private void RefreshModViews()
    {
        var sorted = GetSortedMods();

        ModTree.Items.Clear();
        var root = new TreeViewItem { Header = $"全 {_mods.Count} 件", IsExpanded = true };
        foreach (var mod in sorted)
        {
            root.Items.Add(new TreeViewItem
            {
                Header = $"{mod.DisplayName} ({mod.Loader})",
                Tag = mod
            });
        }
        ModTree.Items.Add(root);
        CardList.ItemsSource = null;
        CardList.ItemsSource = sorted;
        SetViewMode(_viewMode);
    }



    private void ViewLarge_Click(object sender, RoutedEventArgs e) => SetViewMode("large");
    private void ViewMedium_Click(object sender, RoutedEventArgs e) => SetViewMode("medium");
    private void ViewList_Click(object sender, RoutedEventArgs e) => SetViewMode("list");

    private void SetViewMode(string mode)
    {
        _viewMode = mode;
        if (mode == "list")
        {
            CardList.ItemTemplate = (DataTemplate)FindResource("ListTemplate");
            var panel = new ItemsPanelTemplate();
            panel.VisualTree = new System.Windows.FrameworkElementFactory(typeof(StackPanel));
            CardList.ItemsPanel = panel;
        }
        else
        {
            CardList.ItemTemplate = (DataTemplate)FindResource(
                mode == "large" ? "CardLargeTemplate" : "CardMediumTemplate");
            var panel = new ItemsPanelTemplate();
            var f = new System.Windows.FrameworkElementFactory(typeof(WrapPanel));
            f.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            panel.VisualTree = f;
            CardList.ItemsPanel = panel;
        }
    }
    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 初期化中(_modsが空)は何もしない
        if (_mods.Count == 0) return;
        RefreshModViews();
    }

    // 現在のComboBox選択に応じて並べ替えたリストを返す
    private List<ModEntry> GetSortedMods()
    {
        int idx = SortCombo?.SelectedIndex ?? 0;
        return idx switch
        {
            0 => _mods.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            1 => _mods.OrderByDescending(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            2 => _mods.OrderBy(m => m.Categories.Count == 0 ? "\uFFFF" : m.Categories[0],
                                StringComparer.OrdinalIgnoreCase)
                      .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            3 => _mods.OrderByDescending(m => m.FileModified).ToList(),
            4 => _mods.OrderByDescending(m => m.FileSize).ToList(),
            5 => _mods.OrderByDescending(m => m.FileCreated).ToList(),
            6 => _mods.OrderBy(m => m.Loader, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => _mods.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }


    private void ModTree_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is ModEntry mod)
            ShowDetail(mod);
    }
    private void CardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CardList.SelectedItem is ModEntry mod)
            ShowDetail(mod);
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
        DetailCategory.Text = mod.Categories.Count == 0
            ? "カテゴリ: ―"
            : $"カテゴリ ({mod.CategorySource}): {mod.CategoryText}";


        _mrUrl = mod.ModrinthUrl;
        _cfUrl = mod.CurseForgeUrl;
        UrlModrinth.Text = string.IsNullOrEmpty(mod.ModrinthUrl)
            ? "Modrinth: ―" : $"Modrinth: {mod.ModrinthUrl}";
        UrlCurseForge.Text = string.IsNullOrEmpty(mod.CurseForgeUrl)
            ? "CurseForge: ―" : $"CurseForge: {mod.CurseForgeUrl}";

        await ShowBodyAsync(mod);
        Log($"選択: {mod.FileName}");

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

    // ===== クラッシュレポート =====
    private void LoadCrashFiles()
    {
        CrashFileList.Items.Clear();
        if (string.IsNullOrEmpty(_instancePath)) return;
        var dir = Path.Combine(_instancePath, "crash-reports");
        if (!Directory.Exists(dir))
        {
            Log("crash-reports\\ が見つかりません。");
            return;
        }
        foreach (var f in Directory.GetFiles(dir, "*.txt")
                                   .OrderByDescending(File.GetLastWriteTime))
        {
            CrashFileList.Items.Add(new CrashFileItem
            {
                FullPath = f,
                Display = $"{File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}  {Path.GetFileName(f)}"
            });
        }
        Log($"{CrashFileList.Items.Count} 件のクラッシュレポートを検出しました。");
    }

    private void CrashFile_Selected(object sender, SelectionChangedEventArgs e)
    {
    }

    private void AnalyzeCrash_Click(object sender, RoutedEventArgs e)
    {
        if (CrashFileList.SelectedItem is not CrashFileItem item)
        {
            MessageBox.Show("レポートを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CrashResult.Text = $"選択: {item.Display}\n\n(解析処理は Day 5 で実装)";
        Log($"クラッシュ解析(仮): {Path.GetFileName(item.FullPath)}");
    }
}

public class CrashFileItem
{
    public string FullPath { get; set; } = "";
    public string Display { get; set; } = "";
    public override string ToString() => Display;
}
