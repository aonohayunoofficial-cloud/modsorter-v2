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

    public MainWindow()
    {
        InitializeComponent();
        MainTabs.SelectedIndex = 0;

        _settings = Settings.Load();
        if (!string.IsNullOrEmpty(_settings.InstancePath))
        {
            _instancePath = _settings.InstancePath;
            PathBox.Text = _instancePath;
        }

        if (!string.IsNullOrEmpty(_settings.CurseForgeKeyEnc))
        {
            CfKeyBox.Password = "";
            SettingsStatus.Text = "CurseForge APIキー: 保存済み(変更する場合のみ再入力)";
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
        SettingsStatus.Text = $"保存しました。(CurseForge APIキー: {cfState})";
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
        Log($"{_mods.Count} 個の .jar を読み取りました。Modrinth照合を開始します...");

        // 進捗UIを表示
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;
        ScanStatus.Text = "照合中...";

        int total = _mods.Count;
        int done = 0;
        int matched = 0;
        var lockObj = new object();

        // 同時実行数を5に制限
        var semaphore = new SemaphoreSlim(5);
        var tasks = _mods.Select(async mod =>
        {
            await semaphore.WaitAsync();
            try
            {
                var r = await ModrinthClient.GetByHashAsync(mod.FilePath);
                if (r != null)
                {
                    mod.ModrinthUrl = r.Url;
                    mod.Body = r.Body;
                    mod.BodyIsHtml = false; // ModrinthはMarkdown
                    if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                    lock (lockObj) matched++;
                }
            }
            finally
            {
                semaphore.Release();
                int currentDone;
                lock (lockObj) currentDone = ++done;
                // UIスレッドで進捗更新
                Dispatcher.Invoke(() =>
                {
                    ScanProgress.Value = total == 0 ? 100 : (currentDone * 100.0 / total);
                    ScanStatus.Text = $"照合中... {currentDone}/{total}";
                });
            }
        });

        await Task.WhenAll(tasks);
        Log($"Modrinth照合完了: {matched}/{total} 件ヒットしました。");

        // ===== CurseForge照合 =====
        var cfKey = Settings.Decrypt(_settings.CurseForgeKeyEnc);
        if (string.IsNullOrEmpty(cfKey))
        {
            ScanStatus.Text = $"完了(Modrinthのみ): {matched}/{total} 件";
            Log("CurseForge APIキー未設定のため、CurseForge照合はスキップしました。");
            RefreshModViews();
            return;
        }

        CurseForgeClient.Init(cfKey);
        var cfTargets = _mods;
        Log($"CurseForge照合を開始します... 対象 {cfTargets.Count} 件");
        ScanProgress.Value = 0;

        int cfDone = 0, cfMatched = 0;
        var cfSem = new SemaphoreSlim(3);
        var cfTasks = cfTargets.Select(async mod =>
        {
            await cfSem.WaitAsync();
            try
            {
                var r = await CurseForgeClient.GetByFingerprintAsync(mod.FilePath);
                if (r != null && !string.IsNullOrEmpty(r.Url))
                {
                    mod.CurseForgeUrl = r.Url;
                    if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                    // Modrinthで本文が取れていなければCurseForgeのHTML説明を使う
                    if (string.IsNullOrEmpty(mod.Body))
                    {
                        if (!string.IsNullOrEmpty(r.DescriptionHtml))
                        {
                            mod.Body = r.DescriptionHtml;
                            mod.BodyIsHtml = true; // CurseForgeはHTML
                        }
                        else
                        {
                            mod.Body = r.Summary;
                            mod.BodyIsHtml = false;
                        }
                    }
                    lock (lockObj) cfMatched++;
                }
                else
                {
                    // 最初の5件だけ理由をログ
                    int n;
                    lock (lockObj) n = cfDone;
                    if (n < 5)
                    {
                        var err = CurseForgeClient.LastError;
                        Dispatcher.Invoke(() => Log($"CF未ヒット [{mod.FileName}]: {err}"));
                    }
                }
            }
            finally
            {
                cfSem.Release();
                int cur;
                lock (lockObj) cur = ++cfDone;
                Dispatcher.Invoke(() =>
                {
                    ScanProgress.Value = cfTargets.Count == 0 ? 100 : (cur * 100.0 / cfTargets.Count);
                    ScanStatus.Text = $"CurseForge照合中... {cur}/{cfTargets.Count}";
                });
            }
        });
        await Task.WhenAll(cfTasks);

        int totalMatched = matched + cfMatched;
        ScanStatus.Text = $"完了: {totalMatched}/{total} 件ヒット(MR:{matched} CF:{cfMatched})";
        ScanProgress.Value = 100;
        Log($"CurseForge照合完了: {cfMatched} 件追加ヒット。合計 {totalMatched}/{total} 件。");
        RefreshModViews();
    }


    private void RefreshModViews()
    {
        ModTree.Items.Clear();
        var root = new TreeViewItem { Header = $"全 {_mods.Count} 件", IsExpanded = true };
        foreach (var mod in _mods)
        {
            root.Items.Add(new TreeViewItem
            {
                Header = $"{mod.DisplayName} ({mod.Loader})",
                Tag = mod
            });
        }
        ModTree.Items.Add(root);
        CardList.ItemsSource = null;
        CardList.ItemsSource = _mods;
    }

    private void ModTree_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is ModEntry mod)
            ShowDetail(mod);
    }

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ModEntry mod)
            ShowDetail(mod);
    }

    private async void ShowDetail(ModEntry mod)
    {
        DetailName.Text = mod.DisplayName;
        DetailId.Text = $"ID: {mod.ModId}";
        DetailVersion.Text = $"バージョン: {mod.Version}";
        DetailLoader.Text = $"ローダー: {mod.Loader}";

        _mrUrl = mod.ModrinthUrl;
        _cfUrl = mod.CurseForgeUrl;
        UrlModrinth.Text = string.IsNullOrEmpty(mod.ModrinthUrl)
            ? "Modrinth: ―" : $"Modrinth: {mod.ModrinthUrl}";
        UrlCurseForge.Text = string.IsNullOrEmpty(mod.CurseForgeUrl)
            ? "CurseForge: ―" : $"CurseForge: {mod.CurseForgeUrl}";

        await ShowBodyAsync(mod);
        Log($"選択: {mod.FileName}");
        Log($"IconUrl: {mod.IconUrl}");

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
