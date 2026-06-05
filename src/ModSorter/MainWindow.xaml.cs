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
    // 1件のMODをオンライン照合し、アイコン保存とキャッシュ書き戻しまで行う
    private async Task<bool> FetchOneAsync(ModEntry mod)
    {
        bool hit = false;

        // 一旦クリア(古いキャッシュ由来データを消して取り直す)
        mod.ModrinthUrl = "";
        mod.CurseForgeUrl = "";
        mod.Body = "";
        mod.BodyIsHtml = false;
        mod.IconUrl = "";
        mod.IconFile = "";
        mod.Categories = new();
        mod.CategorySource = "";
        mod.TranslatedHtml = "";

        // SHA1とファイル情報が未取得なら取得
        if (string.IsNullOrEmpty(mod.Sha1))
            mod.Sha1 = ModrinthClient.Sha1(mod.FilePath);

        // Modrinth照合
        try
        {
            var r = await ModrinthClient.GetByHashAsync(mod.FilePath);
            if (r != null)
            {
                mod.ModrinthUrl = r.Url;
                mod.Body = r.Body;
                mod.BodyIsHtml = false;
                if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                if (mod.Categories.Count == 0 && r.Categories.Count > 0)
                {
                    mod.Categories = r.Categories;
                    mod.CategorySource = "Modrinth";
                }
                hit = true;
            }
        }
        catch { }

        // CurseForge照合(APIキーがあれば)
        var cfKey = Settings.Decrypt(_settings.CurseForgeKeyEnc);
        if (!string.IsNullOrEmpty(cfKey))
        {
            if (!CurseForgeClient.IsReady) CurseForgeClient.Init(cfKey);
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
                    if (r.Categories.Count > 0)
                    {
                        mod.Categories = r.Categories;
                        mod.CategorySource = "CurseForge";
                    }
                    hit = true;
                }
            }
            catch { }
        }

        // アイコンをローカル保存
        if (!string.IsNullOrEmpty(mod.IconUrl))
            mod.IconFile = await ModCache.EnsureIconAsync(mod.Sha1, mod.IconUrl);

        // キャッシュに書き戻し
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
        ModCache.Save();

        return hit;
    }

    private string _viewMode = "medium";

    private void RefreshModViews()
    {
        var sorted = GetSortedMods();

        ModTree.Items.Clear();

        if (_categoryView)
        {
            // カテゴリ別ツリー: カテゴリ名 → 配下にそのカテゴリのMOD
            // 1MODが複数カテゴリを持つ場合は該当する全カテゴリに重複表示
            var byCategory = new SortedDictionary<string, List<ModEntry>>(
                StringComparer.OrdinalIgnoreCase);
            var uncategorized = new List<ModEntry>();

            foreach (var mod in sorted)
            {
                if (mod.Categories == null || mod.Categories.Count == 0)
                {
                    uncategorized.Add(mod);
                    continue;
                }
                foreach (var cat in mod.Categories)
                {
                    if (string.IsNullOrWhiteSpace(cat)) continue;
                    if (!byCategory.TryGetValue(cat, out var list))
                    {
                        list = new List<ModEntry>();
                        byCategory[cat] = list;
                    }
                    list.Add(mod);
                }
            }

            foreach (var kv in byCategory)
            {
                var catNode = new TreeViewItem
                {
                    Header = $"{kv.Key} ({kv.Value.Count})",
                    Tag = kv.Key,          // カテゴリ名(中央フィルタ用)
                    IsExpanded = false
                };
                foreach (var mod in kv.Value)
                {
                    catNode.Items.Add(new TreeViewItem
                    {
                        Header = $"{mod.DisplayName} ({mod.Loader})",
                        Tag = mod
                    });
                }
                ModTree.Items.Add(catNode);
            }

            // 未分類は末尾
            if (uncategorized.Count > 0)
            {
                var node = new TreeViewItem
                {
                    Header = $"(未分類) ({uncategorized.Count})",
                    Tag = "",              // 空文字=未分類(中央フィルタ用)
                    IsExpanded = false
                };
                foreach (var mod in uncategorized)
                {
                    node.Items.Add(new TreeViewItem
                    {
                        Header = $"{mod.DisplayName} ({mod.Loader})",
                        Tag = mod
                    });
                }
                ModTree.Items.Add(node);
            }
        }
        else
        {
            // フラット表示: 全MOD
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
        }

        // 中央カード: カテゴリ表示でも初期は全件(カテゴリ名クリックで絞り込む)
        CardList.ItemsSource = null;
        CardList.ItemsSource = sorted;
        SetViewMode(_viewMode);
    }
    private void ViewAll_Click(object sender, RoutedEventArgs e)
    {
        _categoryView = false;
        CardList.ItemsSource = null;
        CardList.ItemsSource = GetSortedMods();   // 中央は全件に戻す
        SetViewMode(_viewMode);
        RefreshModViews();
    }

    private void ViewByCategory_Click(object sender, RoutedEventArgs e)
    {
        _categoryView = true;
        RefreshModViews();   // ツリーをカテゴリ別へ
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
        if (e.NewValue is not TreeViewItem item) return;

        if (item.Tag is ModEntry mod)
        {
            // MOD名クリック: 中央へスクロール強調＋右パネルに詳細
            ShowDetail(mod);

            // カテゴリ表示で絞り込み中だと対象が中央に居ない場合があるので
            // 念のため、その時点のCardListに含まれていればスクロール強調する
            if (CardList.Items.Contains(mod))
            {
                CardList.ScrollIntoView(mod);
                Dispatcher.BeginInvoke(new Action(() => HighlightCard(mod)),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        else if (item.Tag is string category)
        {
            // カテゴリ名クリック: 中央をそのカテゴリのMODだけに絞り込む
            FilterCardsByCategory(category);
        }
    }

    // 中央カードを指定カテゴリのMODだけに絞り込む(空文字=未分類)
    private void FilterCardsByCategory(string category)
    {
        var sorted = GetSortedMods();
        List<ModEntry> filtered;

        if (string.IsNullOrEmpty(category))
        {
            // 未分類
            filtered = sorted.Where(m => m.Categories == null || m.Categories.Count == 0)
                             .ToList();
        }
        else
        {
            filtered = sorted.Where(m => m.Categories != null &&
                                         m.Categories.Any(c =>
                                             string.Equals(c, category,
                                                 StringComparison.OrdinalIgnoreCase)))
                             .ToList();
        }

        CardList.ItemsSource = null;
        CardList.ItemsSource = filtered;
        SetViewMode(_viewMode);
    }

    private bool _selectionMode = false;
    // ツリー表示モード: false=すべて表示(フラット), true=カテゴリ表示
    private bool _categoryView = false;

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
