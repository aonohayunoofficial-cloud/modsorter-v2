using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ModSorter.Architect.Manual;

// 中分類パラメータUIの部品組み立て。
// 各中分類は独立クラスのまま、見た目と配線だけここに集約する。
// HouseParamsControl / ApartmentParamsControl は既存 XAML のままでよい。
public sealed class ParamPanelBuilder
{
    // McButtonGray は TargetType="Button" のため ToggleButton には適用できない。
    // Button を継承し、チェック状態だけ自前で持つトグル。
    public sealed class ToggleChip : Button
    {
        private bool _checked;
        public string OnText { get; set; } = "ON";
        public string OffText { get; set; } = "OFF";

        public bool? IsChecked
        {
            get => _checked;
            set
            {
                bool v = value == true;
                if (_checked == v) return;
                _checked = v;
                Content = _checked ? OnText : OffText;
                Toggled?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? Toggled;

        protected override void OnClick()
        {
            base.OnClick();
            IsChecked = !_checked;
        }
    }

    private readonly StackPanel _root = new();
    private readonly List<Panel> _stack = new();
    private readonly FrameworkElement _owner;
    private readonly Action _onChanged;

    private readonly Dictionary<string, Slider> _sliders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleChip> _toggles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComboBox> _combos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _blocks = new(StringComparer.Ordinal);

    // トグル/選択に連動して表示・非表示するグループ。
    private readonly Dictionary<string, List<Panel>> _toggleGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(Panel Panel, HashSet<string> Values)>> _choiceGroups
        = new(StringComparer.Ordinal);

    public ParamPanelBuilder(FrameworkElement owner, Action onChanged)
    {
        _owner = owner;
        _onChanged = onChanged;
        _stack.Add(_root);
    }

    public UIElement Root => _root;
    private Panel Current => _stack[_stack.Count - 1];

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

    public ParamPanelBuilder Heading(string text)
    {
        var tb = Label(text, "GrassGreen");
        tb.Margin = new Thickness(0, 10, 0, 4);
        Current.Children.Add(tb);
        return this;
    }

    public ParamPanelBuilder Note(string text)
    {
        var tb = Label(text, "TextDim", 11);
        tb.TextWrapping = TextWrapping.Wrap;
        tb.Margin = new Thickness(0, 0, 0, 8);
        Current.Children.Add(tb);
        return this;
    }

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
        Current.Children.Add(dock);

        _sliders[key] = slider;
        return this;
    }

    public ParamPanelBuilder Toggle(string key, string onText, string offText, bool value)
    {
        var toggle = new ToggleChip
        {
            OnText = onText,
            OffText = offText,
            Content = value ? onText : offText,
            Style = (Style)_owner.FindResource("McButtonGray"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 8)
        };
        toggle.IsChecked = value;
        toggle.Toggled += (_, __) => { ApplyToggleGroup(key); _onChanged(); };

        Current.Children.Add(toggle);
        _toggles[key] = toggle;
        return this;
    }

    public ParamPanelBuilder Choice(
        string key, string label, IEnumerable<(string Text, string Value)> items, string value)
    {
        Current.Children.Add(Label(label, "GrassGreen"));

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
        combo.SelectionChanged += (_, __) => { ApplyChoiceGroup(key); _onChanged(); };

        Current.Children.Add(combo);
        _combos[key] = combo;
        return this;
    }

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

        Current.Children.Add(button);
        return this;
    }

    // ===== 連動グループ =====

    // toggleKey が ON のときだけ表示される入れ子パネルを開く。
    public ParamPanelBuilder BeginGroup(string toggleKey)
    {
        var panel = NewGroupPanel();
        if (!_toggleGroups.TryGetValue(toggleKey, out var list))
            _toggleGroups[toggleKey] = list = new List<Panel>();
        list.Add(panel);
        panel.Visibility = GetBool(toggleKey) ? Visibility.Visible : Visibility.Collapsed;
        return this;
    }

    // comboKey の選択値が values のいずれかのときだけ表示される入れ子パネルを開く。
    public ParamPanelBuilder BeginChoiceGroup(string comboKey, params string[] values)
    {
        var panel = NewGroupPanel();
        var set = new HashSet<string>(values, StringComparer.Ordinal);
        if (!_choiceGroups.TryGetValue(comboKey, out var list))
            _choiceGroups[comboKey] = list = new List<(Panel, HashSet<string>)>();
        list.Add((panel, set));
        panel.Visibility = set.Contains(GetChoice(comboKey, "")) ? Visibility.Visible : Visibility.Collapsed;
        return this;
    }

    public ParamPanelBuilder EndGroup()
    {
        if (_stack.Count > 1) _stack.RemoveAt(_stack.Count - 1);
        return this;
    }

    private StackPanel NewGroupPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        Current.Children.Add(panel);
        _stack.Add(panel);
        return panel;
    }

    private void ApplyToggleGroup(string key)
    {
        if (!_toggleGroups.TryGetValue(key, out var list)) return;
        var v = GetBool(key) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var p in list) p.Visibility = v;
    }

    private void ApplyChoiceGroup(string key)
    {
        if (!_choiceGroups.TryGetValue(key, out var list)) return;
        string cur = GetChoice(key, "");
        foreach (var (panel, values) in list)
            panel.Visibility = values.Contains(cur) ? Visibility.Visible : Visibility.Collapsed;
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

    public List<string> BlockIds() => _blocks.Values.Distinct().ToList();
}
