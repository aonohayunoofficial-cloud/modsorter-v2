using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ModSorter;

public partial class MainWindow : Window
{
    // ===== 表示モード・ソート・カテゴリ表示・検索 =====
    private string _viewMode = "medium";

    // ツリー表示モード: false=すべて表示(フラット), true=カテゴリ表示
    private bool _categoryView = false;
    // 現在のカテゴリ絞り込み(カテゴリ表示でカテゴリ名クリック中のみ非null)
    private string? _activeCategory = null;
    // 検索入力のデバウンス用タイマー
    private DispatcherTimer? _searchTimer;

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
        _activeCategory = null;   // カテゴリ絞り込み解除
        RefreshModViews();        // ツリーをフラットへ
        ApplyCardFilter();        // 中央は検索語を反映した全件(検索が空なら全件)
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
                    DispatcherPriority.Loaded);
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
        _activeCategory = category;   // カテゴリ絞り込みを記憶(検索と併用)
        ApplyCardFilter();
    }

    // 検索ボックスの入力変更(デバウンス: 入力が止まって250ms後に実行)
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_searchTimer == null)
        {
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchTimer.Tick += (_, __) =>
            {
                _searchTimer!.Stop();
                ApplyCardFilter();
            };
        }
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    // AND/ORトグルの変更
    private void SearchMode_Changed(object sender, RoutedEventArgs e)
    {
        // 初期化中(SearchBox未生成)はスキップ
        if (SearchBox == null) return;
        ApplyCardFilter();
    }

    // 現在のカテゴリ絞り込み＋検索語で中央カードを絞り込む
    private void ApplyCardFilter()
    {
        var sorted = GetSortedMods();

        // 1) カテゴリ絞り込み(カテゴリ表示でカテゴリ名選択中のみ)
        IEnumerable<ModEntry> baseList = sorted;
        if (_activeCategory != null)
        {
            if (_activeCategory.Length == 0)
            {
                baseList = sorted.Where(m => m.Categories == null || m.Categories.Count == 0);
            }
            else
            {
                baseList = sorted.Where(m => m.Categories != null &&
                    m.Categories.Any(c => string.Equals(c, _activeCategory,
                        StringComparison.OrdinalIgnoreCase)));
            }
        }

        // 2) 検索語で絞り込み(名前・カテゴリ・ModId、AND/OR)
        string raw = SearchBox?.Text?.Trim() ?? "";
        if (raw.Length > 0)
        {
            var terms = raw.Split(new[] { ' ', '　' },
                StringSplitOptions.RemoveEmptyEntries);
            bool andMode = SearchAndRadio?.IsChecked == true;

            baseList = baseList.Where(m =>
            {
                bool MatchTerm(string term)
                {
                    if (m.DisplayName != null &&
                        m.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (m.ModId != null &&
                        m.ModId.Contains(term, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (m.Categories != null &&
                        m.Categories.Any(c => c != null &&
                            c.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        return true;
                    return false;
                }
                return andMode ? terms.All(MatchTerm) : terms.Any(MatchTerm);
            });
        }
        var result = baseList.ToList();
        CardList.ItemsSource = result;
    }
}
