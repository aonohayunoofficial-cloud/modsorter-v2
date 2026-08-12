using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 進入灯。滑走路と同じ縮尺の仕組みに載る平面土木。
//
// 既定値の根拠（ICAO Annex 14 Vol.I 第5章）:
//   CAT I      … センターライン900m（間隔30m）＋クロスバー150/300/450/600/750m。
//                300mのクロスバーは長さ30m、他は外縁を結ぶ線が進入端の300m先で収束。
//                0〜300mは1灯、300〜600mは2灯、600〜900mは3灯。
//   CAT II/III … CAT Iに加えて270mまで伸びる赤の側方列（間隔30m）。
//   簡易式     … 420m以上（間隔60m・30mまで詰めてよい）＋300mに長さ18mか30mのクロスバー1本。
//   バレット   … 簡易式で3m以上、他で4m以上。使うときクロスバーはCAT Iで300mの1本、
//                CAT II/IIIで150mと300mの2本だけになる。
public sealed class ApproachLightParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public ApproachLightParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "進入端側", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("方式")
           .Choice("type", "灯火システム", new[]
           {
               ("精密進入 CAT I（900m）", "cat1"),
               ("精密進入 CAT II/III（900m＋側方列）", "cat2"),
               ("簡易式 SALS（420m）", "simple"),
           }, "cat1")
           .Note("CAT Iは灯数で距離が読める（0〜300m:1灯 / 300〜600m:2灯 / 600〜900m:3灯）。CAT II/IIIは270mまで赤の側方列が加わる。")
           .Toggle("barrette", "バレットを使う", "単独の灯火", false)
           .Note("バレットは簡易式で3m以上、他で4m以上。使うとクロスバーはCAT Iで300mの1本、CAT II/IIIで150mと300mの2本だけになる。");

        _ui.Heading("縮尺")
           .Choice("scale", "1マスの大きさ", new[]
           {
               ("1マス=5m", "5"), ("1マス=10m", "10"),
               ("1マス=15m", "15"), ("1マス=30m", "30"),
           }, "15")
           .Note("CAT Iの全長900mは実寸だと900マス。15mなら60マスに収まる。");

        _ui.Heading("寸法")
           .IntSlider("len", "描く長さ", 8, 64, 60, "マス数。実寸は縮尺を掛けた値")
           .IntSlider("rw", "滑走路の幅", 5, 63, 45, "進入端の帯と側方列の位置に使う。偶数は奇数へ丸める")
           .IntSlider("trestle", "進入灯橋の高さ", 0, 8, 0, "0で地面置き。海上・傾斜地の進入灯に使う");

        _ui.Heading("附属")
           .Toggle("papi", "PAPIあり", "PAPIなし", true)
           .Note("滑走路の左側、進入端から300mの位置に4灯を横に並べる。");

        _ui.Heading("使用ブロック")
           .BlockPick("light", "白灯", "minecraft:sea_lantern")
           .BlockPick("mark", "赤灯（側方列）", "minecraft:redstone_lamp")
           .BlockPick("pave", "基礎・進入端の帯", "minecraft:gray_concrete")
           .BlockPick("shoulder", "進入灯橋", "minecraft:iron_block")
           .BlockPick("line", "区画線", "minecraft:white_concrete");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string light = _ui.GetBlock("light", "minecraft:sea_lantern");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(light);

        string type = _ui.GetChoice("type", "cat1");
        bool barrette = _ui.GetBool("barrette");
        bool papi = _ui.GetBool("papi");
        int scale = int.TryParse(_ui.GetChoice("scale", "15"), out int s) && s > 0 ? s : 15;
        int len = _ui.GetInt("len");
        int rw = _ui.GetInt("rw");
        int trestle = _ui.GetInt("trestle");

        var spec = new StructureSpec
        {
            StructureType = "airport:approach_light",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = rw,
            Depth = len,
            Height = 2,
            AirportScale = scale,
            AirportAlsType = type,
            AirportAlsBarrette = barrette,
            AirportAlsTrestle = trestle,
            AirportPapi = papi,
            AirportMarking = true,
            SeatBlock = light,
            AccentBlock = _ui.GetBlock("mark", "minecraft:redstone_lamp"),
            FloorBlock = _ui.GetBlock("pave", "minecraft:gray_concrete"),
            BaseBlock = _ui.GetBlock("shoulder", "minecraft:iron_block"),
            WallBlock = _ui.GetBlock("line", "minecraft:white_concrete")
        };

        int fullM = type == "simple" ? 420 : 900;
        int shownM = len * scale;
        string typeNote = type switch
        {
            "cat2" => "精密進入 CAT II/III",
            "simple" => "簡易式 SALS",
            _ => "精密進入 CAT I"
        };
        string cover = shownM >= fullM
            ? $"全長{fullM}m を収容"
            : $"進入端から{shownM}m まで（全長{fullM}m のうち）";
        string barNote = barrette ? "バレット" : "単独の灯火";
        string trestleNote = trestle > 0 ? $"進入灯橋{trestle}" : "地面置き";

        summary = $"進入灯 {typeNote} / {cover}・1マス={scale}m / {barNote} / " +
                  $"{trestleNote} / {(papi ? "PAPIあり" : "PAPIなし")}";
        return spec;
    }
}
