using ModSorter.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ModSorter;

// Mods タブの一覧表示。1ファイル9KB以下の規約に合わせ partial へ分割している。
//   MainWindow.View.cs        表示モード・並び順・ツリーとカードの組み立て（このファイル）
//   MainWindow.View.Filter.cs ツリー選択・カテゴリ/検索/配布元の絞り込み
//   MainWindow.Cover.cs       カバーアート帯の構築・配置・画像読み込み
//   MainWindow.Cover.Slide.cs カバーアート帯のスライド・ドラッグ・クリック
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
        // 配布元(CurseForge / Modrinth)の絞り込みをツリーとカードの両方に効かせる。
        // カバーアート帯は「最近更新された MOD」の表示なので絞り込みの対象外。
        var sorted = GetVisibleMods();

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
            // フラット表示: 配布元で絞り込み中は「表示数 / 全数」にする
            var header = sorted.Count == _mods.Count
                ? $"全 {_mods.Count} 件"
                : $"表示 {sorted.Count} / 全 {_mods.Count} 件";
            var root = new TreeViewItem { Header = header, IsExpanded = true };
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
        UpdateSourceFilterInfo(sorted.Count);
        RefreshCoverArts();
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
}
