using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 集合住宅（アパート/マンション）のパラメータUI。
// 戸建てと同じ箱ベースだが「階数」を持ち、既存 StructureSpec.FloorLevels に
// 中間床の高さを詰めることで多層化する。各階に窓列を規則配置する。
// 展開・プレビュー・NBT出力・デバウンスは MainWindow 側（戸建てと同じ契約）。
public partial class ApartmentParamsControl : UserControl, IManualParamControl
{
    private string _wallBlock = "minecraft:stone_bricks";
    private string _floorBlock = "minecraft:smooth_stone";
    private string _roofBlock = "minecraft:stone_brick_slab";

    public event EventHandler? ParamsChanged;
    private void RaiseParamsChanged() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public ApartmentParamsControl()
    {
        InitializeComponent();
    }

    // 各スライダー → ラベル更新＋変更通知。
    private void ApSlider_Changed(object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider sl || sl.Tag is not string which) return;
        int v = (int)Math.Round(e.NewValue);
        switch (which)
        {
            case "w": if (ApWidthLabel != null) ApWidthLabel.Text = v.ToString(); break;
            case "d": if (ApDepthLabel != null) ApDepthLabel.Text = v.ToString(); break;
            case "f": if (ApFloorsLabel != null) ApFloorsLabel.Text = v.ToString(); break;
            case "fh": if (ApFloorHeightLabel != null) ApFloorHeightLabel.Text = v.ToString(); break;
            case "win": if (ApWinPerFloorLabel != null) ApWinPerFloorLabel.Text = v.ToString(); break;
        }
        RaiseParamsChanged();
    }

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

    private void ApPickWall_Click(object sender, RoutedEventArgs e)
    {
        var picked = PickSingleBlock(_wallBlock);
        if (picked != null) { _wallBlock = picked; RaiseParamsChanged(); }
    }

    private void ApPickFloor_Click(object sender, RoutedEventArgs e)
    {
        var picked = PickSingleBlock(_floorBlock);
        if (picked != null) { _floorBlock = picked; RaiseParamsChanged(); }
    }

    private void ApPickRoof_Click(object sender, RoutedEventArgs e)
    {
        var picked = PickSingleBlock(_roofBlock);
        if (picked != null) { _roofBlock = picked; RaiseParamsChanged(); }
    }

    // UI値 → StructureSpec + allowed。
    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int w = (int)Math.Round(ApWidthSlider.Value);
        int d = (int)Math.Round(ApDepthSlider.Value);
        int floors = (int)Math.Round(ApFloorsSlider.Value);
        int fh = (int)Math.Round(ApFloorHeightSlider.Value);
        int winPer = (int)Math.Round(ApWinPerFloorSlider.Value);

        int h = floors * fh; // 総高さ = 階数 × 階高

        // 中間床（2階以降の床）を各階の境目に入れる。1階床(y=0)は別管理なので除く。
        var floorLevels = new List<int>();
        for (int f = 1; f < floors; f++) floorLevels.Add(f * fh);

        // 各階の窓を前後面(north/south)に等間隔配置。窓は床から1段上(level=2)に置く。
        var openings = new List<Opening>();
        for (int f = 0; f < floors; f++)
        {
            int level = f * fh + 2; // その階の床上2段目
            if (level >= h) continue;
            for (int i = 0; i < winPer; i++)
            {
                int off = winPer <= 1 ? w / 2 : 1 + (int)Math.Round((double)i * (w - 3) / (winPer - 1));
                if (off < 1) off = 1;
                if (off > w - 2) off = w - 2;
                openings.Add(new Opening { Face = "north", Kind = "window", Offset = off, Level = level });
                openings.Add(new Opening { Face = "south", Kind = "window", Offset = off, Level = level });
            }
        }

        // 1階の south 面中央に入口ドア。
        openings.Add(new Opening { Face = "south", Kind = "door", Offset = w / 2, Level = 1 });

        var spec = new StructureSpec
        {
            Width = w,
            Depth = d,
            Height = h,
            StructureType = "building",
            RoofType = "flat",
            WallBlock = _wallBlock,
            FloorBlock = _floorBlock,
            RoofBlock = _roofBlock,
            FloorLevels = floorLevels,
            Openings = openings
        };

        // 展開に渡す許可ブロック（壁・床・屋根＋窓ガラス）。重複は除く。
        allowed = new List<string> { _wallBlock, _floorBlock, _roofBlock, "minecraft:glass" }
            .Distinct().ToList();

        summary = $"{w}×{d}×{h} / {floors}階(階高{fh}) / 開口{openings.Count}件";
        return spec;
    }
}
