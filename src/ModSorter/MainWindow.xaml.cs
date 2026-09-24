using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Markdig;
using System.Windows.Threading;
using ModSorter.Services;
using ModSorter.Models;



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

        // 起動時に DeepL の消費を1回だけ取る。v2/usage は文字を消費しない。
        // 起動直後に枠の状態が見えていれば、生成を回してから 456 で気づく事態を避けられる。
        if (!string.IsNullOrEmpty(_settings.DeepLKeyEnc))
            _ = RefreshDeepLUsageAsync();

        // アプリ終了時に、自動起動した ComfyUI を止める。
        // (手動で起動していた場合は ComfyUiLauncher.Stop が何もしないので安全)
        this.Closed += (_, __) => ModSorter.Architect.Generation.ComfyUiLauncher.Stop();
        this.Closed += (_, __) => ModSorter.Architect.Generation.Trellis2Launcher.Stop();

        Log("ModSorter v0.1 を起動しました。");
    }


    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogBox.ScrollToEnd();
    }

    // Tab0「最近の動作」に1件追加(新しいものを上に、最大20件保持)
    private readonly List<string> _activity = new();
    private void AddActivity(string msg)
    {
        _activity.Insert(0, $"[{DateTime.Now:MM/dd HH:mm}] {msg}");
        if (_activity.Count > 20) _activity.RemoveAt(_activity.Count - 1);

        // ActivityList が初期化済みなら表示更新
        if (ActivityList != null)
        {
            ActivityList.ItemsSource = null;
            ActivityList.ItemsSource = _activity;
        }
    }

    // ===== 進捗バー共通ヘルパー =====
    // 確定進捗(value/max が分かる)と不定進捗(グルグル)の両方に対応。
    // UIスレッド外から呼ばれても落ちないよう Dispatcher で包む。

    private void ProgressShow(string label, bool indeterminate)
    {
        Dispatcher.Invoke(() =>
        {
            ArchProgressArea.Visibility = Visibility.Visible;
            ArchProgressBar.IsIndeterminate = indeterminate;
            if (!indeterminate) ArchProgressBar.Value = 0;
            ArchProgressText.Text = label;
        });
    }

    private void ProgressUpdate(int current, int total, string? label = null)
    {
        Dispatcher.Invoke(() =>
        {
            if (total <= 0) return;
            ArchProgressBar.IsIndeterminate = false;
            ArchProgressBar.Value = current * 100.0 / total;
            ArchProgressText.Text = label
                ?? $"{current} / {total} ({current * 100 / total}%)";
        });
    }

    private void ProgressHide()
    {
        Dispatcher.Invoke(() =>
        {
            ArchProgressArea.Visibility = Visibility.Collapsed;
            ArchProgressBar.IsIndeterminate = false;
            ArchProgressBar.Value = 0;
            ArchProgressText.Text = "";
        });
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
            _settings.CurseForgeKeyEnc = Settings.Encrypt(CfKeyBox.Password.Trim());

        // 入力欄が空のときは既存のキーを残す作りなので、アカウントを替えたつもりで
        // 「保存」を押していないと古いキーが残る。画面は「保存済み」と出続けるため、
        // 枠を使い切った旧キーのまま HTTP 456（Quota exceeded）が返り続ける状態に
        // 気づけない。今回貼り替えたかどうかを表示で区別する。
        bool dlUpdated = !string.IsNullOrEmpty(DeepLKeyBox.Password);
        if (dlUpdated)
            _settings.DeepLKeyEnc = Settings.Encrypt(DeepLKeyBox.Password.Trim());
        _settings.Save();

        var cfState = string.IsNullOrEmpty(_settings.CurseForgeKeyEnc) ? "未設定" : "保存済み";
        var dlState = string.IsNullOrEmpty(_settings.DeepLKeyEnc)
            ? "未設定"
            : (dlUpdated ? "今回更新" : "保存済み(今回は変更なし)");
        SettingsStatus.Text = $"保存しました。(CurseForge: {cfState} / DeepL: {dlState})";
        Log($"設定を保存しました。(DeepL: {dlState})");
        CfKeyBox.Password = "";
        DeepLKeyBox.Password = "";

        // 保存したキーで残量を取り直す。キーを替えたのに消費や末尾4文字が動かない
        // ときは、貼り替えが効いていないか、別のキーが残っている。
        _ = RefreshDeepLUsageAsync();
    }

    // 「残量を確認」ボタン。
    private void DeepLUsage_Click(object sender, RoutedEventArgs e)
        => _ = RefreshDeepLUsageAsync();

    // DeepL の当月消費を取り直して設定画面へ出す。v2/usage は文字を消費しないので、
    // 何度呼んでも枠は減らない。どのキーで動いているかを末尾4文字で添える。
    private async Task RefreshDeepLUsageAsync()
    {
        var key = Settings.Decrypt(_settings.DeepLKeyEnc);
        if (string.IsNullOrEmpty(key))
        {
            DeepLUsageText.Text = "消費: (キー未設定)";
            return;
        }

        DeepLUsageText.Text = "消費: 取得中...";
        Clients.DeepLClient.Init(key);
        var usage = await Clients.DeepLClient.GetUsageAsync();

        if (usage.HasValue)
        {
            long used = usage.Value.Count, limit = usage.Value.Limit;
            int pct = limit <= 0 ? 0 : (int)(used * 100 / limit);
            DeepLUsageText.Text =
                $"消費: {used:N0} / {limit:N0} 文字（{pct}%） / キー末尾 …"
                + Clients.DeepLClient.KeyTail;
        }
        else
        {
            DeepLUsageText.Text =
                $"消費: 取得失敗（{Clients.DeepLClient.LastError}） / 宛先 "
                + Clients.DeepLClient.BaseUrl;
        }
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
}