using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ModSorter.Architect.Manual;

// 中分類パラメータUIの部品組み立て。
// 各中分類は独立したクラスとして実装する（分類ごとにパラメータが違うため）が、
// スライダー・トグル・選択・ブロック選択ボタンの見た目と配線はここに集約して
// XAML を書かずに済ませる。HouseParamsControl / ApartmentParamsControl は
// 既存の XAML 実装のままで、こちらへ移行する必要はない。
public sealed class ParamPanelBuilder
{
    private readonly StackPanel _root = new();
    private readonly FrameworkElement _owner;
    private readonly Action _onChanged;

    // 追加した入力を名前で引けるようにしておく（BuildSpec から値を読むため）。
    private readonly Dictionary<string, Slider> _sliders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleButton> _toggles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComboBox> _combos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _blocks = new(StringComparer.Ordinal);

    // owner: リソース解決とダイアログのオーナー用（呼び出し元の UserControl）。
    // onChanged: 値が変わったときに呼ぶ通知（各コントロールの RaiseParamsChanged）。
    public ParamPanelBuilder(FrameworkElement owner, Action onChanged)
    {
        _owner = owner;
        _onChanged = onChanged;
    }

    // 組み上がったパネル。UserControl.Content に入れる。
    public UIElement Root => _root;

    private Brush Brush(string key) => (Brush)_owner.FindResource(key);
    private FontFamily PixelFont => (FontFamily)_owner.FindResource("PixelFont");

    private TextBlock Label(string text, string colorKey, double size = 12)
        => new()
        {
            Text = text,
            Foreground = Brush(colorKey),
            FontFamily = PixelFont,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center
        };

    // 見出し行。区切りとして使う。
    public ParamPanelBuilder Heading(string text)
    {
        var tb = Label(text, "GrassGreen");
        tb.Margin = new Thickness(0, 10, 0, 4);
        _root.Children.Add(tb);
        return this;
    }

    // 補足説明の小さい行。
    public ParamPanelBuilder Note(string text)
    {
        var tb = Label(text, "TextDim", 11);
        tb.TextWrapping = TextWrapping.Wrap;
        tb.Margin = new Thickness(0, 0, 0, 8);
        _root.Children.Add(tb);
        return this;
    }

    // 整数スライダー1行（ラベル・現在値・スライダー）。
    // key で GetInt から読める。
    public ParamPanelBuilder IntSlider(
        string key, string label, int min, int max, int value, string? tooltip = null)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 4, 0, 2) };

        var name = Label(label, "TextMain");
        name.Width = 68;
        if (tooltip != null) name.ToolTip = tooltip;
        DockPanel.SetDock(name, Dock.Left);

        var valueText = Label(value.ToString(), "GrassGreen");
        valueText.Width = 34;
        valueText.TextAlignment = TextAlignment.Right;
        DockPanel.SetDock(valueText, Dock.Right);

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = ((int)Math.Round(e.NewValue)).ToString();
            _onChanged();
        };

        dock.Children.Add(name);
        dock.Children.Add(valueText);
        dock.Children.Add(slider);
        _root.Children.Add(dock);

        _sliders[key] = slider;
        return this;
    }

    // ON/OFF トグル1行。onText/offText が Content に出る。
    public ParamPanelBuilder Toggle(
        string key, string onText, string offText, bool value)
    {
        var toggle = new ToggleButton
        {
            Content = value ? onText : offText,
            IsChecked = value,
            Style = (Style)_owner.FindResource("McButtonGray"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 8)
        };
        void Sync(object? s, RoutedEventArgs e)
        {
            toggle.Content = (toggle.IsChecked == true) ? onText : offText;
            _onChanged();
        }
        toggle.Checked += Sync;
        toggle.Unchecked += Sync;

        _root.Children.Add(toggle);
        _toggles[key] = toggle;
        return this;
    }

    // 選択肢1行。items は (表示名, 値) の並び。値は GetChoice で取れる。
    public ParamPanelBuilder Choice(
        string key, string label, IEnumerable<(string Text, string Value)> items, string value)
    {
        _root.Children.Add(Label(label, "GrassGreen"));

        var list = items.ToList();
        var combo = new ComboBox
        {
            Style = (Style)_owner.FindResource("McComboBox"),
            ItemContainerStyle = (Style)_owner.FindResource("McComboBoxItem"),
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 8)
        };
        foreach (var (text, val) in list)
            combo.Items.Add(new ComboBoxItem { Content = text, Tag = val });

        int index = list.FindIndex(i => i.Value == value);
        combo.SelectedIndex = index >= 0 ? index : 0;
        combo.SelectionChanged += (_, __) => _onChanged();

        _root.Children.Add(combo);
        _combos[key] = combo;
        return this;
    }

    // ブロック選択ボタン1行。押すと BlockPickerWindow が開き、先頭1件を採用する。
    // 現在の選択IDはボタン本文にも出す（何が選ばれているか一目で分かるように）。
    public ParamPanelBuilder BlockPick(string key, string label, string defaultId)
    {
        _blocks[key] = defaultId;

        var button = new Button
        {
            Content = $"{label}: {defaultId}",
            Style = (Style)_owner.FindResource("McButtonGray"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 4)
        };
        button.Click += (_, __) =>
        {
            var win = new BlockPickerWindow(new[] { _blocks[key] })
            {
                Owner = Window.GetWindow(_owner)
            };
            if (win.ShowDialog() == true && !string.IsNullOrWhiteSpace(win.ResultCsv))
            {
                var picked = win.ResultCsv
                    .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .FirstOrDefault(s => s.Length > 0);
                if (picked != null)
                {
                    _blocks[key] = picked;
                    button.Content = $"{label}: {picked}";
                    _onChanged();
                }
            }
        };

        _root.Children.Add(button);
        return this;
    }

    // ===== 値の読み出し =====

    public int GetInt(string key)
        => _sliders.TryGetValue(key, out var s) ? (int)Math.Round(s.Value) : 0;

    public bool GetBool(string key)
        => _toggles.TryGetValue(key, out var t) && t.IsChecked == true;

    public string GetChoice(string key, string fallback)
    {
        if (_combos.TryGetValue(key, out var c) &&
            c.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return tag;
        return fallback;
    }

    public string GetBlock(string key, string fallback)
        => _blocks.TryGetValue(key, out var id) && id.Length > 0 ? id : fallback;

    // allowed 用に、選択されているブロックIDを重複なく集める。
    public List<string> BlockIds() => _blocks.Values.Distinct().ToList();
}
