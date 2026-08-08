using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 灯台。円形テーパーの塔身の上に回廊と灯室が載る独立塔。
// 塔（TowerParamsControl）と違い、断面が円形で上へ絞れることと、
// 頂部が「回廊＋ガラス張りの灯室」で固定されることが再現度の軸なので、
// 既存の箱ベースには載せず HarborExpander が座標を作る。
//
// 既定値の根拠（実寸）:
//   塔高 20〜30m 級が一般的。塔身は下部直径 6〜8m から上へテーパーし、
//   頂部に回廊（バルコニー）が張り出して、その上にガラス張りの灯室が載る。
public sealed class LighthouseParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public LighthouseParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("塔身")
           .Note("塔高20〜30m級・下部直径6〜8mが実物の目安。")
           .IntSlider("h", "塔身の高さ", 6, 60, 24)
           .IntSlider("shaft", "下部直径", 3, 21, 7)
           .IntSlider("taper", "テーパー", 0, 20, 8, "この段数ごとに直径を1絞る。0で円筒")
           .IntSlider("base", "基礎の高さ", 0, 8, 2, "塔身より2マス外へ広い円盤");

        _ui.Heading("頂部")
           .IntSlider("gallery", "回廊の張り出し", 0, 3, 1, "0で回廊なし")
           .IntSlider("lantern", "灯室の高さ", 0, 10, 4, "0で灯室なし");

        _ui.Heading("向き")
           .Choice("sea", "窓を向ける方位", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("使用ブロック")
           .BlockPick("body", "塔身", "minecraft:white_concrete")
           .BlockPick("pave", "回廊の床", "minecraft:light_gray_concrete")
           .BlockPick("trim", "基礎の縁", "minecraft:polished_andesite")
           .BlockPick("fitting", "手すり・窓", "minecraft:glass")
           .BlockPick("armor", "灯室", "minecraft:glass")
           .BlockPick("parapet", "灯室の屋根", "minecraft:black_concrete");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int h = _ui.GetInt("h");
        int shaft = _ui.GetInt("shaft");
        int taper = _ui.GetInt("taper");
        int gallery = _ui.GetInt("gallery");
        int lantern = _ui.GetInt("lantern");
        int baseH = _ui.GetInt("base");

        string body = _ui.GetBlock("body", "minecraft:white_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        var spec = new StructureSpec
        {
            StructureType = "harbor:lighthouse",
            FacadeFace = _ui.GetChoice("sea", "south"),
            Width = shaft + 4,                          // 参考値
            Depth = shaft + 4,                          // 参考値
            Height = baseH + h + gallery * 2 + lantern, // 参考値
            HarborCrown = h,
            HarborShaft = shaft,
            HarborTaper = taper,
            HarborGallery = gallery,
            HarborLantern = lantern,
            HarborMound = baseH,
            WallBlock = body,
            FloorBlock = _ui.GetBlock("pave", "minecraft:light_gray_concrete"),
            AccentBlock = _ui.GetBlock("trim", "minecraft:polished_andesite"),
            SeatBlock = _ui.GetBlock("fitting", "minecraft:glass"),
            RoofBlock = _ui.GetBlock("armor", "minecraft:glass"),
            ParapetBlock = _ui.GetBlock("parapet", "minecraft:black_concrete")
        };

        string taperNote = taper > 0 ? $"テーパー{taper}" : "円筒";
        string galleryNote = gallery > 0 ? $"回廊{gallery}" : "回廊なし";
        string lanternNote = lantern > 0 ? $"灯室{lantern}" : "灯室なし";
        summary = $"灯台 塔高{h}・下部径{shaft} / {taperNote} / {galleryNote} / {lanternNote} / 基礎{baseH}";
        return spec;
    }
}
