using ModSorter.Models;
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

    // ===== カバーアート（軽量カルーセル / 最近5日間更新） =====
    private readonly System.Collections.Generic.List<ModEntry> _coverMods = new();
    private System.Windows.Threading.DispatcherTimer? _coverTimer;
    private double _coverCardWidth = 0;       // 1枚幅(窓幅/3)
    private bool _coverAnimating = false;

    // 物理スロット(0..SLOTS-1)に置くカードと、各スロットが指す論理index
    private const int SLOTS = 5;              // 予備1 + 見える3 + 予備1
    private readonly System.Windows.Controls.Border[] _coverSlots =
        new System.Windows.Controls.Border[SLOTS];
    private int _coverHead = 0;               // スロット0が指す _coverMods のindex
    private double _coverOffset = 0;          // スライドオフセット(px, 左へ負)

    // ドラッグ
    private bool _coverDragging = false;
    private bool _coverDragMoved = false;
    private double _coverDragStartX = 0;
    private double _coverDragBaseOffset = 0;

    private readonly System.Collections.Generic.Dictionary<string,
        System.Windows.Media.Imaging.BitmapImage> _coverBmpCache = new();

    // 5日以内に更新があったMODを集めてカバーを構築
    private void RefreshCoverArts()
    {
        _coverMods.Clear();
        var since = System.DateTime.Now.AddDays(-5);
        foreach (var m in _mods
                     .Where(m => m.FileModified >= since)
                     .OrderByDescending(m => m.FileModified))
        {
            _coverMods.Add(m);
        }

        _coverHead = 0;
        _coverOffset = 0;
        BuildCoverSlots();

        _coverTimer?.Stop();
        if (_coverMods.Count > 3)
        {
            if (_coverTimer == null)
            {
                _coverTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = System.TimeSpan.FromSeconds(3)
                };
                _coverTimer.Tick += (s, e) => AnimateStep(forward: true);
            }
            _coverTimer.Start();
        }
    }

    // スロット5枚を生成して Canvas に配置
    private void BuildCoverSlots()
    {
        if (CoverCanvas == null) return;
        CoverCanvas.Children.Clear();

        if (_coverCardWidth <= 0 && CoverCanvas.ActualWidth > 0)
            _coverCardWidth = CoverCanvas.ActualWidth / 3.0;

        if (_coverMods.Count == 0) return;

        for (int slot = 0; slot < SLOTS; slot++)
        {
            var border = new System.Windows.Controls.Border
            {
                Background = (System.Windows.Media.Brush)FindResource("StoneDark"),
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            border.MouseLeftButtonUp += CoverCard_Click;
            border.Child = new System.Windows.Controls.Image
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };
            _coverSlots[slot] = border;
            CoverCanvas.Children.Add(border);
        }
        LayoutCoverSlots();
    }

    // 各スロットの位置・サイズ・画像を現在の _coverHead と _coverOffset で更新
    private void LayoutCoverSlots()
    {
        if (_coverMods.Count == 0 || _coverCardWidth <= 0) return;
        double h = CoverCanvas.ActualHeight;

        for (int slot = 0; slot < SLOTS; slot++)
        {
            var b = _coverSlots[slot];
            if (b == null) continue;

            // スロット0を左予備(-1枚)とする: 実表示は slot=1,2,3 が窓内
            double left = (slot - 1) * _coverCardWidth + _coverOffset + 3;
            System.Windows.Controls.Canvas.SetLeft(b, left);
            System.Windows.Controls.Canvas.SetTop(b, 3);
            b.Width = _coverCardWidth - 6;
            b.Height = h - 6;

            int idx = Mod(_coverHead + (slot - 1), _coverMods.Count);
            var mod = _coverMods[idx];
            b.Tag = mod;
            if (b.Child is System.Windows.Controls.Image img)
                img.Source = LoadCoverBitmap(mod.IconSource);
        }
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    // 1枚ぶんスライド(forward=true:次へ, false:前へ)
    private void AnimateStep(bool forward)
    {
        if (_coverAnimating || _coverDragging) return;
        if (_coverMods.Count <= 3 || _coverCardWidth <= 0) return;

        _coverAnimating = true;
        double from = _coverOffset;
        double to = forward ? from - _coverCardWidth : from + _coverCardWidth;
        AnimateOffsetTo(from, to, () =>
        {
            // 論理indexを進退してオフセットを0へリセット
            _coverHead = Mod(_coverHead + (forward ? 1 : -1), _coverMods.Count);
            _coverOffset = 0;
            LayoutCoverSlots();
            _coverAnimating = false;
        });
    }

    // オフセットをアニメ。各フレームで LayoutCoverSlots を呼んでスロットを動かす
    private void AnimateOffsetTo(double from, double to, System.Action onDone)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double durMs = 300;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(15)
        };
        timer.Tick += (s, e) =>
        {
            double t = sw.ElapsedMilliseconds / durMs;
            if (t >= 1.0)
            {
                timer.Stop();
                _coverOffset = to;
                LayoutCoverSlots();
                onDone();
                return;
            }
            double eased = 1 - System.Math.Pow(1 - t, 3); // easeOutCubic
            _coverOffset = from + (to - from) * eased;
            LayoutCoverSlots();
        };
        timer.Start();
    }

    private void CoverCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (CoverCanvas == null || CoverCanvas.ActualWidth <= 0) return;
        double newWidth = CoverCanvas.ActualWidth / 3.0;
        if (System.Math.Abs(newWidth - _coverCardWidth) < 0.5 && _coverSlots[0] != null)
        {
            LayoutCoverSlots();
            return;
        }
        _coverCardWidth = newWidth;
        if (_coverSlots[0] == null) BuildCoverSlots();
        else LayoutCoverSlots();
    }

    // ===== ドラッグ =====
    private void CoverCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_coverMods.Count <= 3 || _coverAnimating) return;
        _coverDragging = true;
        _coverDragMoved = false;
        _coverDragStartX = e.GetPosition(CoverCanvas).X;
        _coverDragBaseOffset = _coverOffset;
        _coverTimer?.Stop();
        CoverCanvas.CaptureMouse();
    }

    private void CoverCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_coverDragging) return;
        double cur = e.GetPosition(CoverCanvas).X;
        double delta = cur - _coverDragStartX;
        if (System.Math.Abs(delta) > 4) _coverDragMoved = true;

        _coverOffset = _coverDragBaseOffset + delta;
        // 1枚幅を超えたらその場で論理indexを巻き取り、offsetを範囲内に収める
        while (_coverOffset <= -_coverCardWidth)
        {
            _coverOffset += _coverCardWidth;
            _coverHead = Mod(_coverHead + 1, _coverMods.Count);
        }
        while (_coverOffset >= _coverCardWidth)
        {
            _coverOffset -= _coverCardWidth;
            _coverHead = Mod(_coverHead - 1, _coverMods.Count);
        }
        LayoutCoverSlots();
    }

    private void CoverCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_coverDragging) return;
        _coverDragging = false;
        CoverCanvas.ReleaseMouseCapture();
        SettleDrag();
    }

    private void CoverCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_coverDragging) return;
        _coverDragging = false;
        CoverCanvas.ReleaseMouseCapture();
        SettleDrag();
    }

    // 離した位置から最寄り境界(0 or ±cardW)へ収束
    private void SettleDrag()
    {
        if (_coverCardWidth <= 0 || _coverMods.Count == 0)
        {
            if (_coverMods.Count > 3) _coverTimer?.Start();
            return;
        }
        _coverAnimating = true;
        double from = _coverOffset;
        // offsetは -cardW..+cardW の範囲。半分超なら1枚送る方向へ、未満なら0へ
        double to;
        int headDelta;
        if (from <= -_coverCardWidth / 2.0) { to = -_coverCardWidth; headDelta = 1; }
        else if (from >= _coverCardWidth / 2.0) { to = _coverCardWidth; headDelta = -1; }
        else { to = 0; headDelta = 0; }

        AnimateOffsetTo(from, to, () =>
        {
            if (headDelta != 0)
                _coverHead = Mod(_coverHead + headDelta, _coverMods.Count);
            _coverOffset = 0;
            LayoutCoverSlots();
            _coverAnimating = false;
            if (_coverMods.Count > 3) _coverTimer?.Start();
        });
    }

    private void CoverCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_coverDragMoved) { _coverDragMoved = false; return; }
        if (sender is not System.Windows.Controls.Border b) return;
        if (b.Tag is not ModEntry mod) return;

        ShowDetail(mod);
        if (CardList.Items.Contains(mod))
        {
            CardList.ScrollIntoView(mod);
            Dispatcher.BeginInvoke(new Action(() => HighlightCard(mod)),
                DispatcherPriority.Loaded);
        }
    }

    private System.Windows.Media.Imaging.BitmapImage? LoadCoverBitmap(string src)
    {
        if (string.IsNullOrEmpty(src)) return null;
        if (_coverBmpCache.TryGetValue(src, out var cached)) return cached;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            if (System.IO.File.Exists(src))
            {
                var bytes = System.IO.File.ReadAllBytes(src);
                using var ms = new System.IO.MemoryStream(bytes);
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            else
            {
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new System.Uri(src, System.UriKind.RelativeOrAbsolute);
                bmp.EndInit();
            }
            bmp.Freeze();
            _coverBmpCache[src] = bmp;
            return bmp;
        }
        catch { return null; }
    }
}
