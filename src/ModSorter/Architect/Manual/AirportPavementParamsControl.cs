using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 空港の平面土木 3 種（滑走路・誘導路・エプロン）。どれも y=0 の舗装 1 層＋標識なので、
// コンストラクタで種類を受けて UI と既定値を切り替える 1 クラスにまとめる。
// ManualCatalog からは new AirportPavementParamsControl("runway") のように種類を渡す。
// 座標生成は AirportExpander が受け持つので、ここは structure_type="airport:<種類>" と
// 寸法・標識の指定・素材を渡すだけ。
//
// 既定値の根拠（実寸）:
//   滑走路 … 国内主要空港の幅は 45m（いわて花巻空港などが延長 2500m×幅 45m）。
//     中心線標識は長 30m・間隔 20m の破線、進入端標識は縦縞 8 本を中心線対称に配置、
//     接地帯標識は進入端から一定間隔の対、着陸目標点標識は進入端から 400m（延長 2500m 時）。
//     縦縞の寸法は幅 30m 以上の滑走路とそれ未満とで別に定められている。
//   誘導路 … 幅は 23m 以上（成田では 23 / 25 / 30m が使い分けられている）。
//     ショルダー幅は 9.5m 級、固定障害物との間隔は 39m 以上。中心線は黄の実線。
//   エプロン … スポット単位で区画し、リードインラインとストップマークを引く。
//     奥側にタキシレーンが回る。
public sealed class AirportPavementParamsControl : UserControl, IManualParamControl
{
    private readonly string _kind;   // "runway" / "taxiway" / "apron"
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

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
               .Note("国内主要空港の幅は45m。延長は64マス上限のため実物の一部を切り出す。")
               .IntSlider("w", "幅", 12, 64, 45)
               .IntSlider("len", "延長", 16, 64, 64)
               .IntSlider("sh", "ショルダー", 0, 12, 7, "舗装の外側の路肩。片側ぶん");

            _ui.Heading("標識")
               .Note("中心線は長30m・間隔20mの破線。進入端の縦縞は幅45mで8本。")
               .Toggle("mark", "標識を描く", "舗装のみ", true)
               .IntSlider("cstep", "中心線の周期", 0, 12, 5, "0で実線")
               .IntSlider("thr", "進入端の縦縞", 0, 16, 8, "中心線を挟んで対称に並ぶ本数")
               .IntSlider("tdz", "接地帯標識の対", 0, 6, 3);

            _ui.Heading("灯火")
               .IntSlider("light", "縁灯の間隔", 0, 16, 6, "0で灯火なし");
        }
        else if (_kind == "taxiway")
        {
            _ui.Heading("寸法")
               .Note("誘導路の幅は23m以上。ショルダーは9.5m級が実物の目安。")
               .IntSlider("w", "幅", 8, 48, 23)
               .IntSlider("len", "延長", 8, 64, 48)
               .IntSlider("sh", "ショルダー", 0, 16, 9);

            _ui.Heading("標識")
               .Note("中心線は黄の実線1本。両縁に誘導路縁標識の線が走る。")
               .Toggle("mark", "標識を描く", "舗装のみ", true);

            _ui.Heading("灯火")
               .IntSlider("light", "縁灯の間隔", 0, 16, 8, "0で灯火なし");
        }
        else
        {
            _ui.Heading("寸法")
               .Note("スポット単位で区画する。奥側に走行路（タキシレーン）が回る。")
               .IntSlider("spots", "スポット数", 1, 8, 3)
               .IntSlider("sw", "スポットの幅", 6, 40, 18)
               .IntSlider("len", "奥行き", 12, 64, 40, "駐機区画＋走行路")
               .IntSlider("sh", "走行路の幅", 0, 24, 10);

            _ui.Heading("標識")
               .Note("各スポットにリードインラインとストップマークを引く。")
               .Toggle("mark", "標識を描く", "舗装のみ", true);
        }

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "舗装", "minecraft:gray_concrete")
           .BlockPick("mark", "標識", "minecraft:white_concrete")
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
        int len = _ui.GetInt("len");

        var spec = new StructureSpec
        {
            StructureType = "airport:" + _kind,
            FacadeFace = _ui.GetChoice("face", "south"),
            Depth = len,
            AirportMarking = mark,
            FloorBlock = pave,
            AccentBlock = _ui.GetBlock("mark", "minecraft:white_concrete"),
            WallBlock = _ui.GetBlock("line", "minecraft:yellow_concrete"),
            BaseBlock = _ui.GetBlock("shoulder", "minecraft:light_gray_concrete"),
            SeatBlock = _ui.GetBlock("light", "minecraft:sea_lantern")
        };

        if (_kind == "runway")
        {
            int w = _ui.GetInt("w");
            spec.Width = w;
            spec.Height = 2;                       // 参考値。舗装は y=0 の 1 層
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
            spec.Width = w;
            spec.Height = 2;
            spec.AirportShoulder = _ui.GetInt("sh");
            spec.AirportEdgeLight = _ui.GetInt("light");

            summary = $"誘導路 幅{w}×延長{len} / ショルダー{_ui.GetInt("sh")} / "
                    + (mark ? "中心線・縁標識あり" : "舗装のみ");
        }
        else
        {
            int spots = _ui.GetInt("spots");
            int sw = _ui.GetInt("sw");
            spec.Width = Math.Min(64, spots * sw);
            spec.Height = 2;
            spec.AirportSpots = spots;
            spec.AirportSpotWidth = sw;
            spec.AirportShoulder = _ui.GetInt("sh");

            summary = $"エプロン {spots}スポット×幅{sw} / 奥行き{len} / "
                    + (mark ? "誘導線・停止位置あり" : "舗装のみ");
        }

        return spec;
    }
}
