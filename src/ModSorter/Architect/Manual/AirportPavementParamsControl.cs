using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 空港の平面土木 3 種（滑走路・誘導路・エプロン）。どれも y=0 の舗装 1 層＋標識なので、
// コンストラクタで種類を受けて UI と既定値を切り替える 1 クラスにまとめる。
// 座標生成は AirportExpander が受け持つので、ここは structure_type="airport:<種類>" と
// 寸法・標識の指定・素材を渡すだけ。
//
// 既定値の根拠（実寸・ICAO Annex 14 / EASA CS-ADR-DSN）:
//   滑走路 … コード E の幅は 45m。ショルダーを含めた全幅は 60m 以上なので片側 7.5m。
//     中心線標識は長 30m・間隔 20m の破線。進入端の縦縞は幅で本数が決まり、
//     18m:4 / 23m:6 / 30m:8 / 45m:12 / 60m:16 本。
//   誘導路 … コード E の幅は 23m。ショルダーを含めた全幅はコード C:25m / D:38m /
//     E:44m / F:60m なので、コード E なら片側 10.5m。中心線は黄の実線。
//   エプロン … スポット 1 つの幅は「翼幅＋両側のクリアランス」で決まる。
//     クリアランスはコード A/B:3.0m、C:4.5m、D/E/F:7.5m。
//     幅は入力させず機体サイズから決め、全幅はスポット数ぶん横に伸ばす。
public sealed class AirportPavementParamsControl : UserControl, IManualParamControl
{
    private readonly string _kind;   // "runway" / "taxiway" / "apron"
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // 機体サイズ 1 区分ぶんの実寸(m)。幅＝翼幅＋両側クリアランス、
    // 奥行き＝全長＋前後の余裕、走行路＝その区分の誘導路幅。
    private readonly struct ApronSize
    {
        public readonly int WidthM, DepthM, LaneM;
        public readonly string Label;
        public ApronSize(int w, int d, int lane, string label)
        { WidthM = w; DepthM = d; LaneM = lane; Label = label; }
    }

    private static ApronSize SizeOf(string key) => key switch
    {
        // CRJ200 翼幅 21m・全長 27m、クリアランス 3.0m、誘導路幅 10.5m
        "s" => new ApronSize(27, 33, 11, "S 小型機（CRJ200級）"),
        // B777-300ER 翼幅 65m・全長 74m、クリアランス 7.5m、誘導路幅 23m
        "l" => new ApronSize(81, 83, 23, "L 大型機（B777/787級）"),
        // A380 翼幅 80m・全長 73m、クリアランス 7.5m、誘導路幅 25m
        "ll" => new ApronSize(95, 83, 25, "LL 超大型機（A380級）"),
        // A320 / 737-800 翼幅 36m・全長 38m、クリアランス 4.5m、誘導路幅 15m
        _ => new ApronSize(45, 47, 15, "M 中型機（A320/737級）"),
    };

    private static int CeilDiv(int v, int d) => (v + d - 1) / d;
    private static int Odd(int v) => v % 2 == 1 ? v : v + 1;

    public AirportPavementParamsControl(string kind)
    {
        _kind = (kind ?? "runway").Trim().ToLowerInvariant();
        if (_kind != "taxiway" && _kind != "apron") _kind = "runway";
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", _kind == "apron" ? "走行路側" : "進入端側", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        if (_kind == "runway")
        {
            _ui.Heading("寸法")
               .Note("コードEの幅は45m。延長は64マス上限のため実物の一部を切り出す。")
               .IntSlider("w", "幅", 12, 64, 45)
               .IntSlider("len", "延長", 16, 64, 64)
               .IntSlider("sh", "ショルダー", 0, 12, 8, "片側ぶん。幅45mなら7.5mで全幅60m");

            _ui.Heading("標識")
               .Note("縦縞の本数は幅で決まる。18m:4 / 23m:6 / 30m:8 / 45m:12 / 60m:16。")
               .Toggle("mark", "標識を描く", "舗装のみ", true)
               .IntSlider("cstep", "中心線の周期", 0, 12, 5, "0で実線")
               .IntSlider("thr", "進入端の縦縞", 0, 16, 12, "中心線を挟んで対称に並ぶ本数")
               .IntSlider("tdz", "接地帯標識の対", 0, 6, 3);

            _ui.Heading("灯火")
               .IntSlider("light", "縁灯の間隔", 0, 16, 6, "0で灯火なし");
        }
        else if (_kind == "taxiway")
        {
            _ui.Heading("寸法")
               .Note("コードEの幅は23m。ショルダー込みの全幅44mなので片側10.5m。")
               .IntSlider("w", "幅", 8, 48, 23)
               .IntSlider("len", "延長", 8, 64, 48)
               .IntSlider("sh", "ショルダー", 0, 20, 11);

            _ui.Heading("標識")
               .Note("中心線は黄の実線1本。両縁に誘導路縁標識の線が走る。")
               .Toggle("mark", "標識を描く", "舗装のみ", true);

            _ui.Heading("灯火")
               .IntSlider("light", "縁灯の間隔", 0, 16, 8, "0で灯火なし");
        }
        else
        {
            _ui.Heading("機体サイズ")
               .Choice("size", "想定する機体", new[]
               {
                   ("S 小型機（CRJ200級）", "s"),
                   ("M 中型機（A320/737級）", "m"),
                   ("L 大型機（B777/787級）", "l"),
                   ("LL 超大型機（A380級）", "ll"),
               }, "m")
               .Note("スポット1つの大きさが決まる。全幅はスポット数ぶん横に伸びる。")
               .IntSlider("spots", "スポット数", 1, 8, 3)
               .Toggle("lane", "走行路あり", "駐機区画のみ", true);

            _ui.Heading("縮尺")
               .Choice("scale", "1マスの大きさ", new[]
               {
                   ("実寸（1マス=1m）", "1"),
                   ("1マス=2m", "2"),
                   ("1マス=3m", "3"),
                   ("1マス=5m", "5"),
               }, "1")
               .Note("LLは実寸だと1スポット95マス。大きすぎるときは2〜5にする。");

            _ui.Heading("標識")
               .Note("各スポットに区画線・リードインライン・ストップマークを引く。")
               .Toggle("mark", "標識を描く", "舗装のみ", true);
        }

        // エプロンの標識は実物では黄色。滑走路は白なので既定を分ける。
        string markDefault = _kind == "apron"
            ? "minecraft:yellow_concrete"
            : "minecraft:white_concrete";

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "舗装", "minecraft:gray_concrete")
           .BlockPick("mark", "標識", markDefault)
           .BlockPick("line", "区画線・縁標識", "minecraft:yellow_concrete")
           .BlockPick("shoulder", "ショルダー", "minecraft:light_gray_concrete")
           .BlockPick("light", "灯火", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string pave = _ui.GetBlock("pave", "minecraft:gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(pave);

        bool mark = _ui.GetBool("mark");

        var spec = new StructureSpec
        {
            StructureType = "airport:" + _kind,
            FacadeFace = _ui.GetChoice("face", "south"),
            AirportMarking = mark,
            Height = 2,                            // 参考値。舗装は y=0 の 1 層
            FloorBlock = pave,
            AccentBlock = _ui.GetBlock("mark", "minecraft:white_concrete"),
            WallBlock = _ui.GetBlock("line", "minecraft:yellow_concrete"),
            BaseBlock = _ui.GetBlock("shoulder", "minecraft:light_gray_concrete"),
            SeatBlock = _ui.GetBlock("light", "minecraft:sea_lantern")
        };

        if (_kind == "runway")
        {
            int w = _ui.GetInt("w");
            int len = _ui.GetInt("len");
            spec.Width = w;
            spec.Depth = len;
            spec.AirportShoulder = _ui.GetInt("sh");
            spec.AirportCenterStep = _ui.GetInt("cstep");
            spec.AirportThreshold = _ui.GetInt("thr");
            spec.AirportTouchdown = _ui.GetInt("tdz");
            spec.AirportEdgeLight = _ui.GetInt("light");

            string markNote = mark
                ? $"中心線周期{_ui.GetInt("cstep")}・縦縞{_ui.GetInt("thr")}本・接地帯{_ui.GetInt("tdz")}対"
                : "舗装のみ";
            summary = $"滑走路 幅{w}×延長{len} / ショルダー{_ui.GetInt("sh")} / {markNote}";
        }
        else if (_kind == "taxiway")
        {
            int w = _ui.GetInt("w");
            int len = _ui.GetInt("len");
            spec.Width = w;
            spec.Depth = len;
            spec.AirportShoulder = _ui.GetInt("sh");
            spec.AirportEdgeLight = _ui.GetInt("light");

            summary = $"誘導路 幅{w}×延長{len} / ショルダー{_ui.GetInt("sh")} / "
                    + (mark ? "中心線・縁標識あり" : "舗装のみ");
        }
        else
        {
            string sizeKey = _ui.GetChoice("size", "m");
            var size = SizeOf(sizeKey);
            int scale = int.TryParse(_ui.GetChoice("scale", "1"), out int s) && s > 0 ? s : 1;

            int spots = _ui.GetInt("spots");
            int sw = Odd(Math.Max(5, CeilDiv(size.WidthM, scale)));   // 中央を出すため奇数
            int stand = Math.Max(5, CeilDiv(size.DepthM, scale));
            int lane = _ui.GetBool("lane") ? Math.Max(2, CeilDiv(size.LaneM, scale)) : 0;

            spec.Width = spots * sw;               // 全幅はスポット数から決まる従属値
            spec.Depth = stand + lane;
            spec.AirportSpots = spots;
            spec.AirportSpotWidth = sw;
            spec.AirportShoulder = lane;

            string scaleNote = scale == 1 ? "実寸" : $"1マス={scale}m";
            summary = $"エプロン {size.Label} {spots}スポット / "
                    + $"全幅{spec.Width}×奥行き{spec.Depth}（{scaleNote}） / "
                    + (mark ? "区画線・誘導線あり" : "舗装のみ");
        }

        return spec;
    }
}
