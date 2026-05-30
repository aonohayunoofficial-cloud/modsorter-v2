using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

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
        SettingsStatus.Text = "保存しました。";
        Log("設定を保存しました。");
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

        ScanStatus.Text = $"完了: {matched}/{total} 件ヒット";
        ScanProgress.Value = 100;
        Log($"Modrinth照合完了: {matched}/{total} 件ヒットしました。");
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

    private void ShowDetail(ModEntry mod)
    {
        DetailName.Text = mod.DisplayName;
        DetailId.Text = $"ID: {mod.ModId}";
        DetailVersion.Text = $"バージョン: {mod.Version}";
        DetailLoader.Text = $"ローダー: {mod.Loader}";
        DetailBody.Text = string.IsNullOrEmpty(mod.Body)
            ? "(説明なし / 未照合)"
            : (mod.Body.Length > 800 ? mod.Body.Substring(0, 800) + "..." : mod.Body);
        _mrUrl = mod.ModrinthUrl;
        _cfUrl = mod.CurseForgeUrl;
        UrlModrinth.Text = string.IsNullOrEmpty(mod.ModrinthUrl)
            ? "Modrinth: (見つかりません)" : $"Modrinth: {mod.ModrinthUrl}";
        UrlCurseForge.Text = string.IsNullOrEmpty(mod.CurseForgeUrl)
            ? "CurseForge: (Day 3後半)" : $"CurseForge: {mod.CurseForgeUrl}";
        Log($"選択: {mod.FileName}");
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
