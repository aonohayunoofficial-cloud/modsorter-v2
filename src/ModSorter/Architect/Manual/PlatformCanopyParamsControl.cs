using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// ホーム上屋。プラットフォームとは別生成物で、ホーム天端の上に重ねて置く。
//
// 既定値の根拠（実寸）:
//   軒高     … ホーム面上 3.5〜4.5m 級。駅区間で軌道から上屋まで 6.5m を確保する例あり
//              （ホーム高さ1.1mを引くとホーム面上5.4m）。
//   柱間隔   … 古レール上屋で約4.5m（5ヤード）。現代は5m級。
//   柱の位置 … ホーム限界は軌道中心から1.80m（通過列車あり）。縁端が1.475mなので
//              縁端より1マス内側に立てれば外れる。点状ブロックの位置と同じ。
//   軒の出   … 軌道上へ張り出すと建築限界（軌道中心±1.9m・高さ5.7m）に触るため、
//              1マス以上出すときは軒高を6以上へ自動で引き上げる。
public sealed class PlatformCanopyParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    private const int MaxLen = 256;

    public PlatformCanopyParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "線路の向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south")
           .Note("プラットフォームと同じ向きにするとホームの上にそのまま載る。");

        _ui.Heading("大きさ")
           .IntSlider("width", "上屋の幅", 3, 24, 8, "ホーム幅と同じ値にする")
           .IntSlider("cars", "覆う両数", 1, 16, 6, "上屋はホーム全長より短いのが普通")
           .IntSlider("carlen", "1両の長さ(m)", 15, 26, 20)
           .IntSlider("height", "軒高（ホーム面から）", 3, 12, 4, "実物はホーム面上3.5〜4.5m級")
           .IntSlider("eave", "軒の出", 0, 3, 0, "1以上にすると軒高を6へ引き上げる（建築限界）");

        _ui.Heading("骨組み")
           .Choice("rows", "柱の列", new[]
           {
               ("中央1列（Y型）", "1"),
               ("両側2列", "2"),
           }, "1")
           .IntSlider("step", "柱の間隔", 3, 16, 5, "古レール上屋は約4.5m、現代は5m級")
           .Note("柱はホーム縁端の1マス内側に立つ。ホーム限界（軌道中心1.80m）を侵さない。");

        _ui.Heading("屋根")
           .Choice("roof", "屋根の形", new[]
           {
               ("切妻", "gable"), ("片流れ", "shed"), ("陸屋根", "flat"), ("アーチ", "arch"),
           }, "gable")
           .IntSlider("pitch", "屋根勾配（何マスで1マス上がるか）", 1, 8, 3, "3≒18度・4≒14度")
           .Toggle("gutter", "雨といを付ける", "軒先の1マス下に通す", true)
           .IntSlider("light", "照明の間隔", 0, 32, 10, "0で照明なし");

        _ui.Heading("使用ブロック")
           .BlockPick("roofblk", "屋根・桁", "minecraft:light_gray_concrete")
           .BlockPick("body", "柱・梁", "minecraft:polished_andesite")
           .BlockPick("trim", "雨とい・照明", "minecraft:sea_lantern")
           .BlockPick("pave", "予備（床）", "minecraft:smooth_stone");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string roofBlk = _ui.GetBlock("roofblk", "minecraft:light_gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(roofBlk);

        int width = _ui.GetInt("width");
        int cars = _ui.GetInt("cars");
        int carLen = _ui.GetInt("carlen");
        int len = cars * carLen;
        bool trimmed = len > MaxLen;
        if (trimmed) len = MaxLen;

        int height = _ui.GetInt("height");
        int eave = _ui.GetInt("eave");
        if (eave > 0 && height < 6) height = 6;   // 建築限界を避ける

        int rows = _ui.GetChoice("rows", "1") == "2" ? 2 : 1;
        int step = _ui.GetInt("step");
        int pitch = _ui.GetInt("pitch");
        int light = _ui.GetInt("light");
        string roof = _ui.GetChoice("roof", "gable");

        var spec = new StructureSpec
        {
            StructureType = "railway:platform_canopy",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = width,
            Depth = len,
            Height = height,
            RailCanopyRoof = roof,
            RailEave = eave,
            RailColumnRows = rows,
            RailColumnStep = step,
            RailRoofPitch = pitch,
            RailGutter = _ui.GetBool("gutter"),
            RailLightStep = light,
            RoofBlock = roofBlk,
            BaseBlock = _ui.GetBlock("body", "minecraft:polished_andesite"),
            SeatBlock = _ui.GetBlock("trim", "minecraft:sea_lantern"),
            FloorBlock = _ui.GetBlock("pave", "minecraft:smooth_stone")
        };

        string roofNote = roof switch
        {
            "shed" => "片流れ",
            "flat" => "陸屋根",
            "arch" => "アーチ",
            _ => "切妻",
        };
        string eaveNote = eave > 0 ? $"軒の出{eave}（軒高6以上へ補正）" : "軒の出なし";
        string lenNote = trimmed ? $"→上限{MaxLen}に切り詰め" : "";

        summary = $"ホーム上屋 {roofNote} / 幅{width}×長さ{len}（{cars}両分）{lenNote} / " +
                  $"軒高{height}・頭上{height - 1} / 柱{rows}列・間隔{step} / {eaveNote} / " +
                  $"{(_ui.GetBool("gutter") ? "雨といあり" : "雨といなし")} / " +
                  $"{(light > 0 ? $"照明{light}間隔" : "照明なし")}";
        return spec;
    }
}
