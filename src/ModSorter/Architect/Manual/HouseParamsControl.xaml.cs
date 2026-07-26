using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 戸建て住宅のパラメータUI。既存 Tab 6 インラインUIを移設したもの。
// UIの値 → StructureSpec + allowed への変換のみを担う。
// 展開・プレビュー・NBT出力・デバウンスは MainWindow 側。
public partial class HouseParamsControl : UserControl, IManualParamControl
{
    // 壁/床/屋根/煙突のブロック（各1種）。既定は移設前と同じ。
    private string _wallBlock = "minecraft:oak_planks";
    private string _floorBlock = "minecraft:spruce_planks";
    private string _roofBlock = "minecraft:dark_oak_planks";
    private string _chimneyBlock = "minecraft:dark_oak_planks";

    // パラメータ変更通知。MainWindow が購読して再描画予約する。
    public event EventHandler? ParamsChanged;
    private void RaiseParamsChanged() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public HouseParamsControl()
    {
        InitializeComponent();
    }

    // 寸法・勾配・軒スライダー → ラベル更新＋変更通知。
    private void ManualSlider_Changed(object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider sl || sl.Tag is not string which) return;
        int v = (int)Math.Round(e.NewValue);
        switch (which)
        {
            case "w": if (ManualWidthLabel != null) ManualWidthLabel.Text = v.ToString(); break;
            case "d": if (ManualDepthLabel != null) ManualDepthLabel.Text = v.ToString(); break;
            case "h": if (ManualHeightLabel != null) ManualHeightLabel.Text = v.ToString(); break;
            case "p": if (ManualPitchLabel != null) ManualPitchLabel.Text = v.ToString(); break;
            case "e": if (ManualEaveLabel != null) ManualEaveLabel.Text = v.ToString(); break;
        }
        RaiseParamsChanged();
    }

    // 棟の向きトグル。OFF=X軸 / ON=Z軸。
    private void ManualRidge_Toggled(object sender, RoutedEventArgs e)
    {
        if (ManualRidgeToggle != null)
            ManualRidgeToggle.Content = (ManualRidgeToggle.IsChecked == true) ? "Z軸" : "X軸";
        RaiseParamsChanged();
    }

    // 煙突トグル。OFF=煙突なし / ON=煙突あり。
    private void ManualChimney_Toggled(object sender, RoutedEventArgs e)
    {
        if (ManualChimneyToggle != null)
            ManualChimneyToggle.Content = (ManualChimneyToggle.IsChecked == true) ? "煙突あり" : "煙突なし";
        RaiseParamsChanged();
    }

    // 貫通トグル。OFF=屋根上のみ / ON=建物を貫く。
    private void ManualChimneyPierce_Toggled(object sender, RoutedEventArgs e)
    {
        if (ManualChimneyPierceToggle != null)
            ManualChimneyPierceToggle.Content =
                (ManualChimneyPierceToggle.IsChecked == true) ? "貫く（床から通す）" : "貫かない（屋根上のみ）";
        RaiseParamsChanged();
    }

    // 開口の本数スライダー（窓/ドア/アーチ/煙突共通） → 対応ラベル更新＋変更通知。
    private void ManualWinCount_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider sl) return;
        int v = (int)Math.Round(e.NewValue);
        var label = FindName(sl.Name.Replace("Slider", "Label")) as TextBlock;
        if (label != null) label.Text = v.ToString();
        RaiseParamsChanged();
    }

    // 屋根タイプ・寄せ方向・太さ ComboBox 変更 → 変更通知。
    private void ManualParam_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RaiseParamsChanged();

    // 面チェック ON/OFF → 変更通知。
    private void ManualParam_Toggled(object sender, RoutedEventArgs e)
        => RaiseParamsChanged();

    // ===== 使用ブロック選択（結果の先頭1件を採用） =====
    private string? PickSingleBlock(string current)
    {
        var win = new BlockPickerWindow(new[] { current }) { Owner = Window.GetWindow(this) };
        bool? ok = win.ShowDialog();
        if (ok == true && !string.IsNullOrWhiteSpace(win.ResultCsv))
        {
            return win.ResultCsv
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0);
        }
        return null;
    }

    private void ManualPickWall_Click(object sender, RoutedEventArgs e)
    {
        var b = PickSingleBlock(_wallBlock);
        if (b != null) { _wallBlock = b; RaiseParamsChanged(); }
    }

    private void ManualPickFloor_Click(object sender, RoutedEventArgs e)
    {
        var b = PickSingleBlock(_floorBlock);
        if (b != null) { _floorBlock = b; RaiseParamsChanged(); }
    }

    private void ManualPickRoof_Click(object sender, RoutedEventArgs e)
    {
        var b = PickSingleBlock(_roofBlock);
        if (b != null) { _roofBlock = b; RaiseParamsChanged(); }
    }

    private void ManualPickChimney_Click(object sender, RoutedEventArgs e)
    {
        var b = PickSingleBlock(_chimneyBlock);
        if (b != null) { _chimneyBlock = b; RaiseParamsChanged(); }
    }

    // 指定面に、kind の開口を count 個、角を避けた内側へ等間隔配置。
    private static void AddOpeningsForFace(List<Opening> ops, string face, string kind, int count, int span)
    {
        if (count <= 0) return;
        int lo = 1, hi = span - 2;
        if (hi < lo) { lo = 0; hi = span - 1; }
        int usable = hi - lo + 1;
        if (usable <= 0) return;

        int n = Math.Min(count, usable);
        int level = (kind == "window") ? 2 : 1;
        for (int i = 0; i < n; i++)
        {
            int offset = (n == 1)
                ? (lo + hi) / 2
                : lo + (int)Math.Round((double)(hi - lo) * i / (n - 1));
            ops.Add(new Opening { Face = face, Kind = kind, Offset = offset, Level = level });
        }
    }

    private void CollectOpenings(List<Opening> ops, string kind, int w, int d,
        (string face, CheckBox chk, Slider sld)[] faceChecks)
    {
        foreach (var (face, chk, sld) in faceChecks)
        {
            if (chk?.IsChecked != true || sld == null) continue;
            int span = (face == "north" || face == "south") ? w : d;
            AddOpeningsForFace(ops, face, kind, (int)Math.Round(sld.Value), span);
        }
    }

    // UIの値から spec を組み立てて返す。allowed と summary も出力する。
    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = (int)Math.Round(ManualWidthSlider.Value);
        int d = (int)Math.Round(ManualDepthSlider.Value);
        int h = (int)Math.Round(ManualHeightSlider.Value);
        int pitch = Math.Clamp((int)Math.Round(ManualPitchSlider.Value), 1, 4);

        string roofType = (ManualRoofCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "flat";
        string ridgeAxis = (ManualRidgeToggle?.IsChecked == true) ? "z" : "x";

        allowed = new List<string> { _wallBlock, _floorBlock, _roofBlock, _chimneyBlock }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (allowed.Count == 0) allowed.Add("minecraft:oak_planks");

        var openings = new List<Opening>();
        CollectOpenings(openings, "window", w, d, new[]
        {
            ("north", ManualWinNorth, ManualWinNorthSlider),
            ("south", ManualWinSouth, ManualWinSouthSlider),
            ("east",  ManualWinEast,  ManualWinEastSlider),
            ("west",  ManualWinWest,  ManualWinWestSlider),
        });
        CollectOpenings(openings, "door", w, d, new[]
        {
            ("north", ManualDoorNorth, ManualDoorNorthSlider),
            ("south", ManualDoorSouth, ManualDoorSouthSlider),
            ("east",  ManualDoorEast,  ManualDoorEastSlider),
            ("west",  ManualDoorWest,  ManualDoorWestSlider),
        });
        CollectOpenings(openings, "arch", w, d, new[]
        {
            ("north", ManualArchNorth, ManualArchNorthSlider),
            ("south", ManualArchSouth, ManualArchSouthSlider),
            ("east",  ManualArchEast,  ManualArchEastSlider),
            ("west",  ManualArchWest,  ManualArchWestSlider),
        });

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = roofType,
            RidgeAxis = ridgeAxis,
            RoofPitch = pitch,
            WallBlock = _wallBlock,
            FloorBlock = _floorBlock,
            RoofBlock = _roofBlock,
            Openings = openings,
            ChimneyCount = (ManualChimneyToggle?.IsChecked == true)
                ? (int)Math.Round(ManualChimneyCountSlider.Value) : 0,
            ChimneyPierce = (ManualChimneyPierceToggle?.IsChecked == true),
            ChimneyHeight = (int)Math.Round(ManualChimneyHeightSlider.Value),
            ChimneyAlign = (ManualChimneyAlignCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "center",
            ChimneyThickness = (ManualChimneyThickCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "thin",
            ChimneyBlock = _chimneyBlock,
            EaveOverhang = (int)Math.Round(ManualEaveSlider.Value),
            EaveNorth = ManualEaveNorth?.IsChecked == true,
            EaveSouth = ManualEaveSouth?.IsChecked == true,
            EaveEast = ManualEaveEast?.IsChecked == true,
            EaveWest = ManualEaveWest?.IsChecked == true
        };

        summary = $"{w}×{d}×{h} / 屋根={roofType}(勾配1:{pitch}) / 開口{openings.Count}件";
        return spec;
    }
}
