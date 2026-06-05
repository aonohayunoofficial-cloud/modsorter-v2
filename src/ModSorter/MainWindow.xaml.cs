using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Markdig;
using System.Windows.Threading;
using ModSorter.Services;



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