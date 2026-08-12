using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 旅客ターミナル。1マス=1m で組むので、縮尺1で作ったエプロンの隣に置くと寸法が合う。
//
// 既定値の根拠（実寸）:
//   ゲート1つあたりの桁行き … 平均 33〜40m（FAA AC 150/5360-13）。ピア全長 210〜300m。
//                            エプロンのスポット幅と同じ表を使うので駐機位置と中心が揃う。
//   建物の奥行き … 動線幅 30ft（約9m）＋ラウンジ奥行き 25〜30ft（8〜9m）で片側ピア26〜30m。
//   階構成       … 出発が上階・到着が下階の2層が基本。
//   搭乗橋       … アプロンドライブ式。伸長15〜45m、ロタンダ高さ5m級、勾配10%以下。
public sealed class PassengerTerminalParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // ゲート間隔はエプロンのスポット幅と同じ値を使う。
    private readonly struct GateSize
    {
        public readonly string Label;
        public readonly int Pitch;

        public GateSize(string label, int pitch)
        {
            Label = label;
            Pitch = pitch;
        }
    }

    private static GateSize SizeOf(string key) => key switch
    {
        "s" => new GateSize("S 小型機（CRJ200級）", 27),
        "l" => new GateSize("L 大型機（B777/787級）", 81),
        "ll" => new GateSize("LL 超大型機（A380級）", 95),
        _ => new GateSize("M 中型機（A320/737級）", 45),
    };

    private static readonly (string Text, string Value)[] SizeItems =
    {
        ("S 小型機（CRJ200級・27m）", "s"),
        ("M 中型機（A320/737級・45m）", "m"),
        ("L 大型機（B777/787級・81m）", "l"),
        ("LL 超大型機（A380級・95m）", "ll"),
    };

    // 桁行きの上限。AirportExpander.TerminalMaxLen と同じ値にしてある。
    private const int MaxLen = 256;

    private static int Odd(int v) => (v % 2 == 0) ? v + 1 : v;

    public PassengerTerminalParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "エプロン側", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("ゲート")
           .Choice("size", "受け入れる機体", SizeItems, "m")
           .Note("ゲート1つあたりの桁行きは実物で平均33〜40m。エプロンのスポット幅と同じ値なので、縮尺1のエプロンと並べると中心が揃う。")
           .IntSlider("gates", "ゲート数", 1, 8, 3, "桁行き＝ゲート数×間隔。256マスを超える分は自動でゲート数が減る")
           .IntSlider("bridge", "搭乗橋の伸長", 0, 48, 15, "0でなし。実物は15〜45m、勾配は10%以下");

        _ui.Heading("建物")
           .IntSlider("depth", "奥行き", 10, 48, 30, "動線幅9m＋ラウンジ8〜9mで片側ピアは26〜30m")
           .IntSlider("levels", "階数", 1, 4, 2, "実物は出発が上階・到着が下階の2層が基本")
           .IntSlider("lh", "階高", 4, 8, 6, "搭乗橋のロタンダの高さもこれに従う。実物は5m級")
           .Choice("roof", "屋根", new[]
           {
               ("平屋根（パラペット付き）", "flat"), ("かまぼこ屋根", "vault"),
           }, "flat")
           .IntSlider("canopy", "車寄せの庇", 0, 16, 6, "0でなし");

        _ui.Heading("使用ブロック")
           .BlockPick("body", "躯体", "minecraft:white_concrete")
           .BlockPick("glass", "カーテンウォール", "minecraft:light_blue_stained_glass")
           .BlockPick("frame", "方立・腰壁", "minecraft:polished_andesite")
           .BlockPick("floor", "床・搭乗橋", "minecraft:light_gray_concrete")
           .BlockPick("roof", "屋根", "minecraft:smooth_stone")
           .BlockPick("parapet", "パラペット", "minecraft:gray_concrete")
           .BlockPick("lamp", "天井の照明", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        var size = SizeOf(_ui.GetChoice("size", "m"));
        string body = _ui.GetBlock("body", "minecraft:white_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        int pitch = Odd(size.Pitch);
        int gates = _ui.GetInt("gates");
        int asked = gates;
        while (gates > 1 && gates * pitch > MaxLen) gates--;

        int depth = _ui.GetInt("depth");
        int levels = _ui.GetInt("levels");
        int lh = _ui.GetInt("lh");
        int bridge = _ui.GetInt("bridge");
        int canopy = _ui.GetInt("canopy");
        string roof = _ui.GetChoice("roof", "flat");

        var spec = new StructureSpec
        {
            StructureType = "airport:terminal",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = gates * pitch,
            Depth = depth,
            Height = levels * lh,
            AirportGates = gates,
            AirportGateSpacing = pitch,
            AirportLevels = levels,
            AirportLevelHeight = lh,
            AirportBridge = bridge,
            AirportCanopy = canopy,
            AirportTerminalRoof = roof,
            TowerBlock = body,
            WallBlock = body,
            GlazingBlock = _ui.GetBlock("glass", "minecraft:light_blue_stained_glass"),
            AccentBlock = _ui.GetBlock("frame", "minecraft:polished_andesite"),
            FloorBlock = _ui.GetBlock("floor", "minecraft:light_gray_concrete"),
            RoofBlock = _ui.GetBlock("roof", "minecraft:smooth_stone"),
            ParapetBlock = _ui.GetBlock("parapet", "minecraft:gray_concrete"),
            SeatBlock = _ui.GetBlock("lamp", "minecraft:sea_lantern")
        };

        string roofNote = roof == "vault" ? "かまぼこ屋根" : "平屋根";
        string bridgeNote = bridge > 0 ? $"搭乗橋{bridge}" : "搭乗橋なし";
        string canopyNote = canopy > 0 ? $"庇{canopy}" : "庇なし";
        string cut = gates < asked ? $" ※桁行き{MaxLen}超のためゲート{asked}→{gates}" : "";

        summary = $"旅客ターミナル {size.Label} {gates}ゲート / " +
                  $"桁行き{gates * pitch}×奥行き{depth}×{levels}階（階高{lh}） / " +
                  $"{roofNote} / {bridgeNote} / {canopyNote}{cut}";
        return spec;
    }
}
