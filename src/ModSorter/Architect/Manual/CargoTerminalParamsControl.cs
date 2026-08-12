using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 貨物ターミナル。1マス=1m で組むので、縮尺1のエプロンや旅客ターミナルと寸法が合う。
//
// 既定値の根拠（実寸）:
//   トラックドック … 建物床面積 1,000 sq ft あたり 0.6 台（以前は 0.3 台）＝約155㎡に1台。
//                    扉は幅9ft・高さ10ft、ドック高さは48インチ（1.2m）。
//   庫内有効高さ   … 22ft（約7m）が従来標準だが今は不足。自動段積みを入れる棟は40ft（約12m）。
//   トラック回転   … 建物の面から取付道路まで150ft（約46m）。
//   事務所         … 倉庫面積の10%。10万sq ft以上の棟では独立した事務所が好まれる。
//   エプロン       … 建物床面積の4.5倍。敷地は建物15%・ランドサイド25%・エアサイド60%。
public sealed class CargoTerminalParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // 桁行きの上限。AirportExpander.CargoMaxLen と同じ値にしてある。
    private const int MaxLen = 256;

    // ドック1台あたりの建物床面積(㎡)。1,000 sq ft あたり 0.6 台から出した値。
    private const double AreaPerDock = 154.8;

    // エプロンの所要面積は建物床面積の 4.5 倍。
    private const double ApronRatio = 4.5;

    private static int Odd(int v) => (v % 2 == 0) ? v + 1 : v;

    public CargoTerminalParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "エアサイド", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("トラックドック")
           .Note("実物の計画値は建物床面積1,000sq ftあたり0.6台＝約155㎡に1台。桁行き＝ドック数×間隔で、256マスを超える分は自動でドック数が減る。")
           .IntSlider("docks", "ドック数", 2, 48, 12)
           .IntSlider("pitch", "ドック間隔", 3, 12, 4, "扉は幅9ft（約2.7m）。ドック高さは48インチ＝1マス")
           .IntSlider("canopy", "ドック上屋", 0, 16, 5, "0でなし");

        _ui.Heading("倉庫")
           .IntSlider("depth", "奥行き", 16, 96, 48)
           .IntSlider("clear", "庫内の有効高さ", 5, 20, 8, "22ft（約7m）が従来標準。自動段積みを入れる棟は40ft（約12m）")
           .IntSlider("doors", "エアサイドの大型扉", 0, 8, 2, "0でなし")
           .IntSlider("doorw", "大型扉の幅", 3, 31, 7, "偶数は奇数へ丸める");

        _ui.Heading("事務所")
           .IntSlider("office", "事務所棟の桁行き", 0, 64, 24, "0でなし。倉庫の妻側に付く2層の別棟。実物は倉庫面積の10%");

        _ui.Heading("使用ブロック")
           .BlockPick("body", "躯体", "minecraft:light_gray_concrete")
           .BlockPick("glass", "高窓・トップライト", "minecraft:glass")
           .BlockPick("frame", "まぐさ・帯", "minecraft:gray_concrete")
           .BlockPick("floor", "床・エプロン取付け", "minecraft:smooth_stone")
           .BlockPick("roof", "屋根・上屋", "minecraft:cyan_terracotta")
           .BlockPick("shutter", "シャッター・パラペット", "minecraft:iron_block")
           .BlockPick("lamp", "庫内の照明", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string body = _ui.GetBlock("body", "minecraft:light_gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        int pitch = _ui.GetInt("pitch");
        int docks = _ui.GetInt("docks");
        int asked = docks;
        while (docks > 2 && docks * pitch > MaxLen) docks--;

        int len = docks * pitch;
        int depth = _ui.GetInt("depth");
        int clear = _ui.GetInt("clear");
        int doors = _ui.GetInt("doors");
        int doorW = Odd(_ui.GetInt("doorw"));
        int canopy = _ui.GetInt("canopy");
        int office = _ui.GetInt("office");

        var spec = new StructureSpec
        {
            StructureType = "airport:cargo_terminal",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = len,
            Depth = depth,
            Height = clear,
            AirportDocks = docks,
            AirportDockPitch = pitch,
            AirportAirsideDoors = doors,
            AirportDoorWidth = doorW,
            AirportOffice = office,
            AirportCanopy = canopy,
            TowerBlock = body,
            WallBlock = body,
            GlazingBlock = _ui.GetBlock("glass", "minecraft:glass"),
            AccentBlock = _ui.GetBlock("frame", "minecraft:gray_concrete"),
            FloorBlock = _ui.GetBlock("floor", "minecraft:smooth_stone"),
            RoofBlock = _ui.GetBlock("roof", "minecraft:cyan_terracotta"),
            ParapetBlock = _ui.GetBlock("shutter", "minecraft:iron_block"),
            SeatBlock = _ui.GetBlock("lamp", "minecraft:sea_lantern")
        };

        int area = len * depth;
        int rec = Math.Max(1, (int)Math.Round(area / AreaPerDock));
        int apron = (int)Math.Round(area * ApronRatio);
        string officeNote = office >= 6 ? $"事務所{office}×2層" : "事務所なし";
        string doorNote = doors > 0 ? $"大型扉{doors}×幅{doorW}" : "大型扉なし";
        string cut = docks < asked ? $" ※桁行き{MaxLen}超のためドック{asked}→{docks}" : "";

        summary = $"貨物ターミナル 桁行き{len}×奥行き{depth}（{area}㎡）/ " +
                  $"ドック{docks}（この床面積の目安は{rec}）/ 有効高さ{clear} / " +
                  $"{doorNote} / {officeNote} / エプロン所要{apron}㎡{cut}";
        return spec;
    }
}
