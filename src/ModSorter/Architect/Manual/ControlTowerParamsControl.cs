using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 管制塔。庁舎＋シャフト＋管制室（キャブ）の3段構成。
// 1マス=1m で組むので、滑走路・誘導路・エプロンを実寸（縮尺1）で作ったものと
// 並べたときに縮尺が合う。
//
// 既定値の根拠（実寸）:
//   管制室の床面積 … FAA Order 6480.7D の標準型で 234 / 350 / 625 / 850 sq ft
//                    ＝ 22 / 33 / 58 / 79 ㎡。羽田の新管制塔は約130㎡・塔高113m級。
//   平面形         … 正方形・五角形・六角形・八角形・円形。八角形が最多。
//   窓             … 鉛直から外へ 15 度傾ける。4段で1マス＝14.0度が最も近い。
//   キャットウォーク … 窓の清掃用に外周へ回す。幅1m級。
public sealed class ControlTowerParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // 管制室の規模。床面積から対辺幅を決め、シャフトはその内側に収まる寸法にする。
    private readonly struct CabSize
    {
        public readonly string Label;
        public readonly int CabWidth;
        public readonly int ShaftWidth;

        public CabSize(string label, int cabWidth, int shaftWidth)
        {
            Label = label;
            CabWidth = cabWidth;
            ShaftWidth = shaftWidth;
        }
    }

    private static CabSize SizeOf(string key) => key switch
    {
        "s" => new CabSize("S 小規模（管制室22㎡級）", 7, 5),
        "m" => new CabSize("M 中規模（管制室33㎡級）", 9, 7),
        "ll" => new CabSize("LL 大規模拠点（管制室130㎡級）", 15, 11),
        _ => new CabSize("L 拠点空港（管制室58㎡級）", 11, 9),
    };

    private static readonly (string Text, string Value)[] SizeItems =
    {
        ("S 小規模（22㎡・FAA 234sqft級）", "s"),
        ("M 中規模（33㎡・FAA 350sqft級）", "m"),
        ("L 拠点空港（58㎡・FAA 625sqft級）", "l"),
        ("LL 大規模拠点（130㎡・羽田級）", "ll"),
    };

    public ControlTowerParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "正面（見通す側）", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("管制室")
           .Choice("size", "規模", SizeItems, "l")
           .Choice("shape", "平面形", new[]
           {
               ("八角形", "octagon"), ("正方形", "square"), ("円形", "round"),
           }, "octagon")
           .Note("床面積はFAA標準型で22/33/58/79㎡、羽田の新管制塔は約130㎡。平面形は八角形が最多。")
           .IntSlider("cabh", "窓の高さ", 2, 8, 4)
           .IntSlider("tilt", "窓の傾き", 0, 8, 4, "この段数で1マス外へ出す。4段＝14度で実物の15度に最も近い。0で垂直")
           .IntSlider("walk", "外周通路", 0, 3, 1, "窓清掃用のキャットウォーク。0でなし");

        _ui.Heading("塔身")
           .IntSlider("h", "管制室の床の高さ", 12, 60, 30, "マス数。1マス=1m")
           .IntSlider("step", "中間床の間隔", 0, 10, 5, "0で中間床なし")
           .Note("実物の塔高は地方空港で30〜40m、拠点空港で50〜80m、羽田は113m級。");

        _ui.Heading("庁舎")
           .Toggle("office", "庁舎あり", "塔だけ", true)
           .BeginGroup("office")
               .IntSlider("bw", "幅", 7, 48, 21)
               .IntSlider("bd", "奥行き", 7, 48, 15)
               .IntSlider("bh", "高さ", 3, 12, 5)
           .EndGroup();

        _ui.Heading("頂部")
           .IntSlider("mast", "アンテナ柱", 0, 16, 6, "0でなし")
           .Toggle("beacon", "航空障害灯あり", "灯火なし", true);

        _ui.Heading("使用ブロック")
           .BlockPick("body", "塔身", "minecraft:white_concrete")
           .BlockPick("glass", "窓", "minecraft:gray_stained_glass")
           .BlockPick("frame", "窓枠・腰壁", "minecraft:polished_andesite")
           .BlockPick("floor", "床・外周通路", "minecraft:light_gray_concrete")
           .BlockPick("roof", "屋根", "minecraft:black_concrete")
           .BlockPick("rail", "手すり", "minecraft:iron_bars")
           .BlockPick("base", "庁舎", "minecraft:gray_concrete")
           .BlockPick("lamp", "灯火", "minecraft:shroomlight");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        var size = SizeOf(_ui.GetChoice("size", "l"));
        string body = _ui.GetBlock("body", "minecraft:white_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        bool office = _ui.GetBool("office");
        bool beacon = _ui.GetBool("beacon");
        int floorY = _ui.GetInt("h");
        int cabH = _ui.GetInt("cabh");
        int mast = _ui.GetInt("mast");
        int walk = _ui.GetInt("walk");
        int baseH = office ? _ui.GetInt("bh") : 0;

        var spec = new StructureSpec
        {
            StructureType = "airport:control_tower",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = office ? _ui.GetInt("bw") : 0,
            Depth = office ? _ui.GetInt("bd") : 0,
            Height = floorY,
            AirportCabWidth = size.CabWidth,
            AirportShaftWidth = size.ShaftWidth,
            AirportCabHeight = cabH,
            AirportCabShape = _ui.GetChoice("shape", "octagon"),
            AirportCabTilt = _ui.GetInt("tilt"),
            AirportCatwalk = walk,
            AirportFloorStep = _ui.GetInt("step"),
            AirportBaseHeight = baseH,
            AirportMast = mast,
            AirportEdgeLight = beacon ? 1 : 0,
            TowerBlock = body,
            WallBlock = body,
            GlazingBlock = _ui.GetBlock("glass", "minecraft:gray_stained_glass"),
            AccentBlock = _ui.GetBlock("frame", "minecraft:polished_andesite"),
            FloorBlock = _ui.GetBlock("floor", "minecraft:light_gray_concrete"),
            RoofBlock = _ui.GetBlock("roof", "minecraft:black_concrete"),
            ParapetBlock = _ui.GetBlock("rail", "minecraft:iron_bars"),
            BaseBlock = _ui.GetBlock("base", "minecraft:gray_concrete"),
            SeatBlock = _ui.GetBlock("lamp", "minecraft:shroomlight")
        };

        int total = floorY + 1 + cabH + 1 + mast + (beacon ? 1 : 0);
        string shapeNote = _ui.GetChoice("shape", "octagon") switch
        {
            "square" => "正方形",
            "round" => "円形",
            _ => "八角形"
        };
        string officeNote = office
            ? $"庁舎{_ui.GetInt("bw")}×{_ui.GetInt("bd")}×{baseH}"
            : "庁舎なし";
        string walkNote = walk > 0 ? $"外周通路{walk}" : "外周通路なし";

        summary = $"管制塔 {size.Label} {shapeNote} 幅{size.CabWidth} / " +
                  $"管制室の床{floorY}m・全高{total}m / {walkNote} / {officeNote} / " +
                  $"{(beacon ? "航空障害灯あり" : "灯火なし")}";
        return spec;
    }
}
