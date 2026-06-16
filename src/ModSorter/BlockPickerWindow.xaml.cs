using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModSorter.Architect;

namespace ModSorter;

public partial class BlockPickerWindow : Window
{
    // 系統1: 手書きカタログのチェックボックスを id とともに保持
    private readonly List<(string id, CheckBox box)> _checks = new();
    // 系統2: MODツリー側のチェックを別管理(決定時に手書きカタログと合算する)
    private readonly List<(string id, CheckBox box)> _modChecks = new();
    // 絞り込み用に、読み込んだパレットを保持しておく
    private ModSorter.Architect.Generation.BlockPaletteCache? _palette;
    private HashSet<string> _initialSelected = new(StringComparer.OrdinalIgnoreCase);

    public string? ResultCsv { get; private set; }

    public BlockPickerWindow(IEnumerable<string> currentSelected)
    {
        InitializeComponent();

        var selectedSet = new HashSet<string>(
            currentSelected.Select(s => s.Trim()).Where(s => s.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        _initialSelected = selectedSet;

        // 系統1: 手書きカタログ(従来通り)
        BuildCategories(selectedSet);

        // 系統2: MODブロックの自動列挙ツリー
        BuildModTree(selectedSet);

        // どちらにも無い初期選択IDだけ、ID直接指定欄へ復元する
        RestoreExtras(selectedSet);
    }

    // ===== 系統1: 手書きカタログ =====
    private void BuildCategories(HashSet<string> selectedSet)
    {
        var categories = BlockCatalog.Load();
        if (categories.Count == 0)
        {
            CategoryPanel.Children.Add(new TextBlock
            {
                Text = "カタログを読み込めませんでした。" + BlockCatalog.LastError,
                Foreground = (System.Windows.Media.Brush)FindResource("TextDim"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var cat in categories)
        {
            // このカテゴリのチェックボックスを覚えておき、一括操作の対象にする。
            var catChecks = new List<CheckBox>();

            // 見出し行: カテゴリ名(左) と 全選択/全解除ボタン(右) を横並びにする。
            var headerRow = new DockPanel { Margin = new Thickness(0, 10, 0, 4) };

            var allBtn = new Button
            {
                Content = "全選択",
                Style = (Style)FindResource("McButton"),
                FontSize = 10,
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var noneBtn = new Button
            {
                Content = "全解除",
                Style = (Style)FindResource("McButtonGray"),
                FontSize = 10,
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // クリックで、このカテゴリのチェックボックスだけ一括変更する。
            allBtn.Click += (_, __) => { foreach (var c in catChecks) c.IsChecked = true; };
            noneBtn.Click += (_, __) => { foreach (var c in catChecks) c.IsChecked = false; };

            // ボタンは右端に寄せる。
            DockPanel.SetDock(noneBtn, Dock.Right);
            DockPanel.SetDock(allBtn, Dock.Right);
            headerRow.Children.Add(noneBtn);
            headerRow.Children.Add(allBtn);

            var header = new TextBlock
            {
                Text = cat.DisplayName,
                Foreground = (System.Windows.Media.Brush)FindResource("GrassGreen"),
                FontFamily = (System.Windows.Media.FontFamily)FindResource("PixelFont"),
                VerticalAlignment = VerticalAlignment.Center
            };
            // 見出しテキストは残り幅いっぱい(左側)に置く。
            headerRow.Children.Add(header);

            CategoryPanel.Children.Add(headerRow);

            if (!string.IsNullOrWhiteSpace(cat.Note))
            {
                CategoryPanel.Children.Add(new TextBlock
                {
                    Text = cat.Note,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextDim"),
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            foreach (var b in cat.Blocks)
            {
                var cb = new CheckBox
                {
                    Content = $"{b.Name}  ({b.Id})",
                    IsChecked = selectedSet.Contains(b.Id),
                    Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                    Margin = new Thickness(8, 1, 0, 1)
                };
                _checks.Add((b.Id, cb));
                catChecks.Add(cb);   // 一括操作の対象に登録
                CategoryPanel.Children.Add(cb);
            }
        }
    }

    // ===== 系統2: MODブロック自動列挙ツリー =====
    private void BuildModTree(HashSet<string> selectedSet)
    {
        _palette = ModSorter.Architect.Generation.BlockPaletteCache.TryLoadAny();
        ModTree.Items.Clear();
        _modChecks.Clear();
        _groupHeaders.Clear();

        if (_palette == null || _palette.Entries.Count == 0)
        {
            ModTree.Items.Add(new TreeViewItem
            {
                Header = "MODブロックのキャッシュがありません。" +
                         "建築モードの「テクスチャ取得テスト」を一度押して生成してください。",
                Foreground = (System.Windows.Media.Brush)FindResource("TextDim"),
                IsEnabled = false
            });
            return;
        }

        // MOD名 → カテゴリ → ブロック の順にグループ化する。
        var byMod = _palette.Entries
            .GroupBy(e => e.Mod)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var modGroup in byMod)
        {
            var modNode = new TreeViewItem
            {
                Header = BuildNodeHeader($"{modGroup.Key}  ({modGroup.Count()})",
                                          modGroup.Select(e => e.Id).ToList()),
                Tag = modGroup.Key
            };

            var byCat = modGroup
                .GroupBy(e => string.IsNullOrEmpty(e.Category) ? "normal" : e.Category)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var catGroup in byCat)
            {
                var catNode = new TreeViewItem
                {
                    Header = BuildNodeHeader($"{catGroup.Key}  ({catGroup.Count()})",
                                              catGroup.Select(e => e.Id).ToList())
                };

                foreach (var entry in catGroup.OrderBy(e => e.Id, StringComparer.Ordinal))
                {
                    var cb = new CheckBox
                    {
                        Content = entry.Id,
                        IsChecked = selectedSet.Contains(entry.Id),
                        Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                        Margin = new Thickness(0, 1, 0, 1),
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    _modChecks.Add((entry.Id, cb));

                    // 子チェックを直接いじったら親ノードの三状態・件数を更新する。
                    cb.Checked += (_, __) => RefreshAllHeaders();
                    cb.Unchecked += (_, __) => RefreshAllHeaders();

                    // 代表色の小さな色見本 + チェックボックスを横並びにする。
                    var swatch = new System.Windows.Shapes.Rectangle
                    {
                        Width = 12,
                        Height = 12,
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Fill = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                ClampByte(entry.Color, 0),
                                ClampByte(entry.Color, 1),
                                ClampByte(entry.Color, 2)))
                    };

                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(swatch);
                    row.Children.Add(cb);

                    catNode.Items.Add(new TreeViewItem { Header = row });
                }

                modNode.Items.Add(catNode);
            }

            ModTree.Items.Add(modNode);
        }
    }

    // ノードのチェックボックス(親) を保持し、子の変化で更新する。
    private readonly List<(CheckBox box, List<string> ids, string baseLabel)> _groupHeaders = new();

    // ノード見出しを「三状態チェックボックス + 選択数カウント」にする。
    // ids はそのノード配下の全ブロックID。
    private object BuildNodeHeader(string baseLabel, List<string> ids)
    {
        var cb = new CheckBox
        {
            IsThreeState = true,
            Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
            FontFamily = (System.Windows.Media.FontFamily)FindResource("PixelFont"),
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = baseLabel
        };

        // 見出しチェックのクリックで配下を一括トグル。
        // 三状態の循環(false→true→null)に依存せず、実データから判定する。
        // 配下が全部ONなら全解除、そうでなければ全選択。
        cb.Click += (_, __) =>
        {
            bool allOn = AreAllChecked(ids);
            SetChecksFor(ids, !allOn);
            RefreshAllHeaders();   // 親・子で重複する場合に備え全ヘッダ更新
        };

        _groupHeaders.Add((cb, ids, baseLabel));
        RefreshHeader(cb, ids, baseLabel);   // 初期状態を反映
        return cb;
    }

    // 配下の選択数に応じて、親チェックの三状態とラベル(件数)を更新する。
    private void RefreshHeader(CheckBox header, List<string> ids, string baseLabel)
    {
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        int total = 0, on = 0;
        foreach (var (id, box) in _modChecks)
        {
            if (!set.Contains(id)) continue;
            total++;
            if (box.IsChecked == true) on++;
        }

        // 値の変更だけ。Click は IsChecked 変更では発火しないので再帰しない。
        if (on == 0) header.IsChecked = false;
        else if (on == total) header.IsChecked = true;
        else header.IsChecked = null;   // 一部選択 = 中間表示(◼)

        header.Content = $"{baseLabel}   [{on} / {total}]";
    }

    // 指定IDの配下が(1件以上あって)全部チェック済みかどうか。
    private bool AreAllChecked(List<string> ids)
    {
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        int total = 0, on = 0;
        foreach (var (id, box) in _modChecks)
        {
            if (!set.Contains(id)) continue;
            total++;
            if (box.IsChecked == true) on++;
        }
        return total > 0 && on == total;
    }

    // すべての親ノード見出しの三状態・件数を更新する。
    private void RefreshAllHeaders()
    {
        foreach (var (box, ids, baseLabel) in _groupHeaders)
            RefreshHeader(box, ids, baseLabel);
    }

    // 指定IDの集合に対応するツリーのチェックを一括設定する。
    private void SetChecksFor(List<string> ids, bool value)
    {
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        foreach (var (id, box) in _modChecks)
            if (set.Contains(id)) box.IsChecked = value;
    }

    private static byte ClampByte(int[] rgb, int idx)
    {
        if (rgb == null || idx >= rgb.Length) return 128;
        return (byte)Math.Clamp(rgb[idx], 0, 255);
    }

    // MOD名 or ブロックID 部分一致でツリーを絞り込む。
    private void ModFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        string q = ModFilterBox.Text.Trim().ToLowerInvariant();
        bool empty = q.Length == 0;

        foreach (var obj in ModTree.Items)
        {
            if (obj is not TreeViewItem modNode) continue;
            string mod = (modNode.Tag as string ?? "").ToLowerInvariant();
            bool modHit = !empty && mod.Contains(q);

            bool anyChildVisible = false;
            foreach (var catObj in modNode.Items)
            {
                if (catObj is not TreeViewItem catNode) continue;

                bool anyBlockVisible = false;
                foreach (var blockObj in catNode.Items)
                {
                    if (blockObj is not TreeViewItem blockNode) continue;
                    string id = ExtractId(blockNode).ToLowerInvariant();
                    bool hit = empty || modHit || id.Contains(q);
                    blockNode.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
                    if (hit) anyBlockVisible = true;
                }

                catNode.Visibility = anyBlockVisible ? Visibility.Visible : Visibility.Collapsed;
                catNode.IsExpanded = !empty && anyBlockVisible;
                if (anyBlockVisible) anyChildVisible = true;
            }

            modNode.Visibility = anyChildVisible ? Visibility.Visible : Visibility.Collapsed;
            modNode.IsExpanded = !empty && anyChildVisible;
        }
    }

    // ブロックノードのヘッダ(StackPanel)からチェックボックスのIDを取り出す。
    private static string ExtractId(TreeViewItem blockNode)
    {
        if (blockNode.Header is StackPanel sp)
            foreach (var child in sp.Children)
                if (child is CheckBox cb && cb.Content is string s) return s;
        return "";
    }

    // どちらの系統にも無い初期選択IDだけを、ID直接指定欄へ復元する。
    private void RestoreExtras(HashSet<string> selectedSet)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in _checks) known.Add(c.id);
        foreach (var c in _modChecks) known.Add(c.id);

        var extras = selectedSet.Where(id => !known.Contains(id)).ToList();
        if (extras.Count > 0)
            ExtraIdBox.Text = string.Join(", ", extras);
    }

    // ===== 決定 / キャンセル =====
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddId(string id)
        {
            id = id.Trim();
            if (id.Length == 0) return;
            if (seen.Add(id)) ids.Add(id);
        }

        // 系統1: 手書きカタログ
        foreach (var c in _checks)
            if (c.box.IsChecked == true) AddId(c.id);

        // 系統2: MODツリー
        foreach (var c in _modChecks)
            if (c.box.IsChecked == true) AddId(c.id);

        // ID直接指定欄
        var extras = (ExtraIdBox.Text ?? "")
            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var ex in extras) AddId(ex);

        ResultCsv = string.Join(", ", ids);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ResultCsv = null;
        DialogResult = false;
        Close();
    }

    // 表示倍率スライダー。Grid 全体に ScaleTransform をかけて拡大する。
    private void ZoomSlider_ValueChanged(object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomScale == null) return;   // InitializeComponent 前のガード
        ZoomScale.ScaleX = e.NewValue;
        ZoomScale.ScaleY = e.NewValue;
        if (ZoomLabel != null)
            ZoomLabel.Text = $"{(int)System.Math.Round(e.NewValue * 100)}%";
    }
}
