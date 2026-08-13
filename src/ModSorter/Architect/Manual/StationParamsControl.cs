using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 駅舎。1マス=1m。
//
// 既定値の根拠（実寸）:
//   コンコース天井高 … みなし規定で3.0m以上。橋上駅舎の実施例は5.5〜6.8m。
//   自由通路の幅員   … 橋上化事業の実施例で4m。通路の有効幅は最低90cm、
//                      車いすのすれ違いで140cm以上。
//   自動改札機       … 本体長さ1.8m級。通路幅は標準550mm／590mm、幅広900mm。
//                      移動等円滑化基準は有効幅90cm以上。
//   エレベーター     … かご内法 幅140cm以上×奥行135cm以上（11人乗り以上）。
//   階段             … 誘導基準は幅140cm以上・蹴上げ16cm以下・踏面30cm以上。
//   桁下高さ         … 建築限界は直流電化で5700mm。橋上は6以上。
public sealed class StationParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public StationParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("形式")
           .Choice("type", "駅舎の形式", new[]
           {
               ("地平", "ground"), ("橋上", "bridge"), ("高架下", "elevated"),
           }, "ground")
           .Choice("face", "線路の向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south")
           .Note("橋上は出入口が1箇所の片側橋上駅舎。改札内からホーム連絡階段が降りる。");

        _ui.Heading("平面")
           .IntSlider("w", "奥行き（線路と直交）", 10, 48, 16, "地平・高架下で使う")
           .IntSlider("len", "桁行き（線路方向）", 10, 64, 24)
           .IntSlider("span", "跨ぐ長さ", 14, 128, 26, "橋上のみ。線路を横切る長さ")
           .IntSlider("h", "桁下高さ", 6, 24, 7, "橋上のみ。建築限界5.70mより6以上")
           .IntSlider("via", "高架の路盤高さ", 6, 32, 9, "高架下のみ。天井高＋2以上へ補正")
           .IntSlider("ch", "コンコース天井高", 3, 8, 5, "みなし規定3.0m以上。実施例5.5〜6.8m")
           .IntSlider("passage", "出入口・連絡口の有効幅", 2, 12, 4, "自由通路の実施例は幅員4m");

        _ui.Heading("改札")
           .IntSlider("gates", "改札通路の数", 1, 16, 4, "機械2マス＋通路で並ぶ")
           .Toggle("wide", "幅広通路にする", "実物900mm。基準の有効幅90cm以上を満たす", false)
           .IntSlider("ticket", "券売機の台数", 0, 16, 3, "0でなし。改札外の壁沿い");

        _ui.Heading("諸室")
           .IntSlider("waiting", "待合室の桁行き", 0, 24, 0, "3以上で改札内に付く")
           .IntSlider("office", "駅務室の桁行き", 0, 24, 5, "3以上でラッチ外側に付く")
           .Toggle("toilet", "便所を付ける", "改札外の妻側", true)
           .Toggle("ev", "エレベーターを付ける", "かご内法1400×1350mm＋壁で3マス角", true);

        _ui.Heading("屋根・階段")
           .Choice("roof", "屋根の形", new[]
           {
               ("切妻", "gable"), ("片流れ", "shed"), ("陸屋根", "flat"), ("アーチ", "arch"),
           }, "gable")
           .IntSlider("pitch", "屋根勾配", 1, 8, 4)
           .IntSlider("canopy", "車寄せの庇", 0, 8, 3, "0でなし。地平・高架下のみ")
           .Choice("stair", "地上への階段（橋上）", new[]
           {
               ("あり", "one"), ("なし", "none"),
           }, "one")
           .IntSlider("swidth", "階段の幅", 2, 12, 3, "誘導基準は140cm以上")
           .IntSlider("srun", "踏面（1段上がるごとに進むマス）", 1, 4, 2,
               "2で約26.6度＝蹴上0.16m・踏面0.30m相当")
           .IntSlider("wall", "腰壁・手すりの高さ", 1, 4, 2)
           .IntSlider("pier", "中間橋脚の間隔", 0, 64, 0, "0で橋台のみ。橋上で使う")
           .IntSlider("light", "照明の間隔", 0, 32, 8, "0で照明なし");

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "床・通路", "minecraft:smooth_stone")
           .BlockPick("wall", "壁", "minecraft:white_concrete")
           .BlockPick("body", "柱・躯体・橋台", "minecraft:stone_bricks")
           .BlockPick("roofblk", "屋根・床版", "minecraft:gray_concrete")
           .BlockPick("glass", "ガラス・仕切り", "minecraft:glass")
           .BlockPick("edge", "改札機・券売機", "minecraft:light_blue_concrete")
           .BlockPick("fence", "手すり", "minecraft:iron_bars")
           .BlockPick("trim", "照明", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string pave = _ui.GetBlock("pave", "minecraft:smooth_stone");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(pave);

        string type = _ui.GetChoice("type", "ground");
        int w = _ui.GetInt("w");
        int len = _ui.GetInt("len");
        int span = _ui.GetInt("span");
        int deck = _ui.GetInt("h");
        int ch = _ui.GetInt("ch");
        int gates = _ui.GetInt("gates");
        bool wide = _ui.GetBool("wide");
        int passage = _ui.GetInt("passage");

        var spec = new StructureSpec
        {
            StructureType = "railway:station_building",
            FacadeFace = _ui.GetChoice("face", "south"),
            RailStationType = type,
            Width = w,
            Depth = len,
            Height = deck,
            RailSpan = span,
            RailViaduct = type == "elevated" ? _ui.GetInt("via") : 0,
            RailConcourse = ch,
            RailPassage = passage,
            RailGates = gates,
            RailGateWide = wide,
            RailTicket = _ui.GetInt("ticket"),
            RailWaiting = _ui.GetInt("waiting"),
            RailOffice = _ui.GetInt("office"),
            RailToilet = _ui.GetBool("toilet"),
            RailElevator = _ui.GetBool("ev"),
            RailEntranceCanopy = _ui.GetInt("canopy"),
            RailCanopyRoof = _ui.GetChoice("roof", "gable"),
            RailRoofPitch = _ui.GetInt("pitch"),
            RailStair = _ui.GetChoice("stair", "one"),
            RailStairWidth = _ui.GetInt("swidth"),
            RailStairRun = _ui.GetInt("srun"),
            RailWallHeight = _ui.GetInt("wall"),
            RailPierStep = _ui.GetInt("pier"),
            RailLightStep = _ui.GetInt("light"),
            FloorBlock = pave,
            WallBlock = _ui.GetBlock("wall", "minecraft:white_concrete"),
            BaseBlock = _ui.GetBlock("body", "minecraft:stone_bricks"),
            RoofBlock = _ui.GetBlock("roofblk", "minecraft:gray_concrete"),
            VerandaBlock = _ui.GetBlock("glass", "minecraft:glass"),
            AccentBlock = _ui.GetBlock("edge", "minecraft:light_blue_concrete"),
            ParapetBlock = _ui.GetBlock("fence", "minecraft:iron_bars"),
            SeatBlock = _ui.GetBlock("trim", "minecraft:sea_lantern")
        };

        int lane = wide ? 2 : 1;
        int latch = gates * (lane + 1) + 1;
        string laneNote = wide ? "幅広0.9m" : "標準0.55〜0.59m";
        string typeNote = type switch
        {
            "bridge" => $"橋上（跨ぎ{span}・桁下{deck}／建築限界5.7m以上）",
            "elevated" => $"高架下（路盤{spec.RailViaduct}）",
            _ => $"地平（奥行き{w}）",
        };
        string stairNote = type == "bridge"
            ? $"地上階段{(_ui.GetChoice("stair", "one") == "none" ? "なし" : $"あり・幅{_ui.GetInt("swidth")}・踏面{_ui.GetInt("srun")}")}"
            : $"車寄せの庇{_ui.GetInt("canopy")}";

        summary = $"駅舎 {typeNote} / 桁行き{len} / コンコース天井高{ch}（規定3.0m以上）/ " +
                  $"改札{gates}通路（{laneNote}・ラッチ長{latch}）/ 券売機{spec.RailTicket}台 / " +
                  $"連絡口の有効幅{passage}（自由通路4m級）/ " +
                  $"{(spec.RailElevator ? "EVあり（かご1.4×1.35m）" : "EVなし")} / " +
                  $"{(spec.RailToilet ? "便所あり" : "便所なし")} / {stairNote}";
        return spec;
    }
}
