using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 跨線橋。線路を横切る向きに通路が走る。y=0 はレール面。
//
// 既定値の根拠（実寸）:
//   桁下高さ … 建築限界は直流電化で高さ5700mm。跨線橋はこれを侵せないので6以上。
//   通路幅   … 3m級。幅が3mを超える階段は中間手すりが要る（建築基準法）。
//   階段     … 一般用は幅75cm以上・蹴上げ22cm以下・踏面21cm以上。バリアフリー
//              誘導基準は幅140cm以上・蹴上げ16cm以下・踏面30cm以上。
//              1マス=1m では蹴上げ1マス固定なので踏面で勾配を合わせる。
//              踏面2で約26.6度＝実物の約28度に最も近い。
public sealed class OverpassParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public OverpassParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "通路の向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("橋")
           .IntSlider("span", "跨ぐ長さ", 6, 128, 24, "線路とホームを横切る全長")
           .IntSlider("width", "通路幅", 2, 12, 3, "3m級。3を超える階段は中間手すりが要る")
           .IntSlider("height", "桁下高さ（レール面から）", 6, 24, 6,
               "建築限界は直流電化で5700mm。6が下限")
           .IntSlider("wall", "腰壁・手すりの高さ", 1, 4, 2)
           .IntSlider("pier", "中間橋脚の間隔", 0, 64, 0, "0で中間橋脚なし。線路上に落ちないよう注意")
           .Note("ホーム上に降ろす場合はホーム高さ1マス分を差し引いて考える。");

        _ui.Heading("階段")
           .Choice("stair", "階段の付き方", new[]
           {
               ("両端", "both"), ("片側だけ", "one"), ("階段なし", "none"),
           }, "both")
           .IntSlider("swidth", "階段の幅", 2, 12, 3, "誘導基準は140cm以上")
           .IntSlider("run", "踏面（1段上がるごとに進むマス）", 1, 4, 2, "2で約26.6度＝実物に最も近い")
           .Note("階段の全長は 桁下高さ×踏面＋踊り場2。桁下6・踏面2なら14マス伸びる。");

        _ui.Heading("屋根")
           .Toggle("covered", "屋根を付ける", "腰壁の上に柱を立て頭上2マス空けて架ける", true)
           .Choice("roof", "屋根の形", new[]
           {
               ("切妻", "gable"), ("片流れ", "shed"), ("陸屋根", "flat"), ("アーチ", "arch"),
           }, "gable")
           .IntSlider("pitch", "屋根勾配", 1, 8, 3)
           .IntSlider("light", "照明の間隔", 0, 32, 8, "0で照明なし");

        _ui.Heading("使用ブロック")
           .BlockPick("girder", "桁・床版・屋根", "minecraft:gray_concrete")
           .BlockPick("pave", "踏面・通路", "minecraft:smooth_stone")
           .BlockPick("body", "柱・橋台・階段躯体", "minecraft:stone_bricks")
           .BlockPick("fence", "腰壁・手すり", "minecraft:iron_bars")
           .BlockPick("trim", "照明", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string girder = _ui.GetBlock("girder", "minecraft:gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(girder);

        int span = _ui.GetInt("span");
        int width = _ui.GetInt("width");
        int height = _ui.GetInt("height");
        int wall = _ui.GetInt("wall");
        int pier = _ui.GetInt("pier");
        string stair = _ui.GetChoice("stair", "both");
        int sWidth = Math.Min(_ui.GetInt("swidth"), width);
        int run = _ui.GetInt("run");
        bool covered = _ui.GetBool("covered");
        int light = _ui.GetInt("light");
        string roof = _ui.GetChoice("roof", "gable");

        var spec = new StructureSpec
        {
            StructureType = "railway:overpass",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = width,
            Depth = span,
            Height = height,
            RailSpan = span,
            RailStair = stair,
            RailStairWidth = sWidth,
            RailStairRun = run,
            RailWallHeight = wall,
            RailCovered = covered,
            RailPierStep = pier,
            RailCanopyRoof = roof,
            RailRoofPitch = _ui.GetInt("pitch"),
            RailLightStep = light,
            RoofBlock = girder,
            FloorBlock = _ui.GetBlock("pave", "minecraft:smooth_stone"),
            BaseBlock = _ui.GetBlock("body", "minecraft:stone_bricks"),
            ParapetBlock = _ui.GetBlock("fence", "minecraft:iron_bars"),
            SeatBlock = _ui.GetBlock("trim", "minecraft:sea_lantern")
        };

        int stairLen = height * run + 2;
        int ends = stair == "both" ? 2 : (stair == "one" ? 1 : 0);
        int total = span + stairLen * ends;
        string stairNote = stair switch
        {
            "one" => $"階段は片側だけ（全長{stairLen}）",
            "none" => "階段なし（両端に橋台）",
            _ => $"階段は両端（各全長{stairLen}）",
        };
        string roofNote = roof switch
        {
            "shed" => "片流れ",
            "flat" => "陸屋根",
            "arch" => "アーチ",
            _ => "切妻",
        };

        summary = $"跨線橋 スパン{span}×通路幅{width} / 桁下{height}（建築限界5.7m以上）/ " +
                  $"腰壁{wall} / {stairNote}・幅{sWidth}・踏面{run} / " +
                  $"{(covered ? $"屋根あり（{roofNote}）" : "屋根なし")} / " +
                  $"{(pier > 0 ? $"中間橋脚{pier}間隔" : "中間橋脚なし")} / 占有長{total}";
        return spec;
    }
}
