using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModSorter.Architect;

namespace ModSorter;

public partial class BlockPickerWindow : Window
{
    // 全チェックボックスを id とともに保持
    private readonly List<(string id, CheckBox box)> _checks = new();

    // 決定時に確定する選択ID（カンマ区切り）。キャンセル時は null。
    public string? ResultCsv { get; private set; }

    public BlockPickerWindow(IEnumerable<string> currentSelected)
    {
        InitializeComponent();
        var selectedSet = new HashSet<string>(
            currentSelected.Select(s => s.Trim()).Where(s => s.Length > 0),
            System.StringComparer.OrdinalIgnoreCase);

        BuildCategories(selectedSet);

        // 既存選択のうちカタログに無いIDは、ID直接指定欄へ復元する
        var known = new HashSet<string>(_checks.Select(c => c.id),
            System.StringComparer.OrdinalIgnoreCase);
        var extras = selectedSet.Where(s => !known.Contains(s)).ToList();
        if (extras.Count > 0) ExtraIdBox.Text = string.Join(", ", extras);
    }

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

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var ids = _checks.Where(c => c.box.IsChecked == true).Select(c => c.id).ToList();

        // ID直接指定欄を取り込む
        var extras = (ExtraIdBox.Text ?? "")
            .Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
        foreach (var ex in extras)
            if (!ids.Contains(ex, System.StringComparer.OrdinalIgnoreCase))
                ids.Add(ex);

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
}
