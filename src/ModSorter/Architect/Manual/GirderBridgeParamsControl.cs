using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 桁橋。1マス=1m。
//
// 既定値の根拠（実寸）:
//   支間     … 桁橋の一般的な適用支間は25〜150m。既定30mは市街地の跨道橋・跨線橋級。
//   桁高     … 支間長の1/20級。連続桁は中間支点で曲げを分担できるぶん8割へ落とす。
//   支間割   … 3径間連続の側径間:中央径間＝1:1.25（側径間80%）が最も鋼重が軽い。
//   主桁間隔 … 3m級。全幅から自動で本数を出す（2主桁〜多主桁）。
//   車線幅   … 3.25〜3.5m。
//   歩道     … 2.0m級。車道との段差（地覆）は0.15〜0.25m。
//   高欄     … 1.1m。1マスが実寸相当。
//   区画線   … 線幅0.15m。車線境界線は実線長5m・空白長5mの破線。
//   照明     … 灯具間隔30m級（道路照明施設設置基準）。
//   橋脚     … 張出式（T型）が最も一般的。幅員が広い橋では壁式。
public sealed class GirderBridgeParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    private const int MaxLength = 256;

    public GirderBridgeParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "橋が渡る向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("支間割")
           .IntSlider("spans", "支間数", 1, 10, 3, "橋脚の本数は支間数-1")
           .IntSlider("span", "支間長", 8, 80, 30, "1径間の長さ。桁橋の適用支間は25〜150m")
           .Toggle("cont", "連続桁", "単純桁（支点で桁を切る）", true)
           .BeginGroup("cont")
             .IntSlider("side", "側径間比%", 50, 100, 80, "80で側径間:中央径間＝1:1.25")
           .EndGroup()
           .IntSlider("ratio", "桁高比 1/n", 12, 30, 20, "支間長の1/nを桁高にする。20が標準")
           .Note("連続桁では桁高をさらに8割へ落とす。");

        _ui.Heading("横断構成")
           .IntSlider("lanes", "車線数", 1, 6, 2)
           .IntSlider("lanew", "車線の幅", 3, 4, 3, "1車線の幅。実物は3.25〜3.5m")
           .IntSlider("median", "分離帯幅", 0, 6, 0, "中央分離帯。0でなし。1車線のときは無効")
           .IntSlider("walk", "歩道幅", 0, 6, 2, "片側の歩道幅。0で歩道なし（地覆だけ）")
           .IntSlider("rail", "高欄の高さ", 0, 3, 1, "実物1.1m。1が実寸相当")
           .Toggle("mark", "区画線を描く", "区画線なし", true)
           .Note("区画線は専用の1マス列を占めるので、車線幅は指定どおり保たれる代わりに " +
                 "全幅が線の本数ぶん広くなる（実物の線幅は0.15m）。");

        _ui.Heading("主桁")
           .IntSlider("girders", "主桁の本数", 0, 12, 0, "0で全幅からおよそ3m間隔に自動")
           .IntSlider("cross", "横桁の間隔", 0, 24, 6, "0で横桁なし");

        _ui.Heading("下部工")
           .Choice("pier", "橋脚の形式", new[]
           {
               ("張出式（T型）", "t"), ("壁式", "wall"), ("ラーメン（2本柱）", "frame"),
           }, "t")
           .IntSlider("pierh", "橋脚の高さ", 1, 40, 8, "桁下端までの高さ")
           .Toggle("abut", "橋台と取付部を作る", "橋桁だけ", true);

        _ui.Heading("付帯設備")
           .IntSlider("light", "照明の間隔", 0, 80, 30, "0で照明なし。実物は30m級");

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "車道舗装", "minecraft:black_concrete")
           .BlockPick("mark", "区画線", "minecraft:white_concrete")
           .BlockPick("deck", "床版", "minecraft:smooth_stone")
           .BlockPick("girder", "主桁・横桁", "minecraft:gray_concrete")
           .BlockPick("pier", "橋脚・橋台", "minecraft:stone_bricks")
           .BlockPick("curb", "地覆・分離帯", "minecraft:light_gray_concrete")
           .BlockPick("walk", "歩道舗装", "minecraft:smooth_stone_slab")
           .BlockPick("rail", "高欄・照明柱", "minecraft:iron_bars")
           .BlockPick("light", "照明", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string pave = _ui.GetBlock("pave", "minecraft:black_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(pave);

        int spans = _ui.GetInt("spans");
        int span = _ui.GetInt("span");
        bool cont = _ui.GetBool("cont");
        int side = _ui.GetInt("side");
        int ratio = _ui.GetInt("ratio");

        int lanes = _ui.GetInt("lanes");
        int laneW = _ui.GetInt("lanew");
        int median = _ui.GetInt("median");
        int walk = _ui.GetInt("walk");
        int rail = _ui.GetInt("rail");
        bool marks = _ui.GetBool("mark");

        int girders = _ui.GetInt("girders");
        int cross = _ui.GetInt("cross");
        int pierH = _ui.GetInt("pierh");
        int light = _ui.GetInt("light");
        string pierType = _ui.GetChoice("pier", "t");
        bool abut = _ui.GetBool("abut");

        // 橋長・全幅・桁高は Expander と同じ式で先に出し、Width/Depth/Height と要約を合わせる。
        int sideLen = Math.Max(6, span * side / 100);
        int length = 0;
        for (int i = 0; i < spans; i++)
            length += (cont && spans >= 3 && (i == 0 || i == spans - 1)) ? sideLen : span;
        bool trimmed = length > MaxLength;
        if (trimmed) length = MaxLength;

        int markW = marks ? 1 : 0;
        int effMedian = lanes < 2 ? 0 : median;
        int lanesL = effMedian > 0 ? (lanes + 1) / 2 : lanes;
        int lanesR = effMedian > 0 ? lanes - lanesL : 0;

        int Carriage(int n) => n <= 0 ? 0 : n * laneW + (n - 1) * markW;

        int roadW = markW
                  + (effMedian > 0 ? Carriage(lanesL) + effMedian + Carriage(lanesR) : Carriage(lanes))
                  + markW;
        int edge = walk > 0 ? walk + 1 : 1;
        int deckW = roadW + edge * 2;

        int girderH = (int)Math.Round(Math.Max(span, sideLen) / (double)ratio);
        if (cont) girderH = girderH * 4 / 5;
        if (girderH < 1) girderH = 1;
        if (girderH > 8) girderH = 8;

        int height = pierH + girderH + (walk > 0 ? 3 : 2) + rail + (light > 0 ? 4 : 0);

        var spec = new StructureSpec
        {
            StructureType = "bridge:girder_bridge",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = deckW,
            Depth = length,
            Height = height,
            BridgeSpans = spans,
            BridgeSpan = span,
            BridgeContinuous = cont,
            BridgeSideRatio = side,
            BridgeDepthRatio = ratio,
            BridgeGirders = girders,
            BridgeCrossStep = cross,
            BridgeLanes = lanes,
            BridgeLaneWidth = laneW,
            BridgeMedian = median,
            BridgeSidewalk = walk,
            BridgeRailing = rail,
            BridgeLaneMark = marks,
            BridgePierType = pierType,
            BridgePierHeight = pierH,
            BridgeAbutment = abut,
            BridgeLightStep = light,
            FloorBlock = pave,
            AccentBlock = _ui.GetBlock("mark", "minecraft:white_concrete"),
            WallBlock = _ui.GetBlock("deck", "minecraft:smooth_stone"),
            RoofBlock = _ui.GetBlock("girder", "minecraft:gray_concrete"),
            BaseBlock = _ui.GetBlock("pier", "minecraft:stone_bricks"),
            TowerBlock = _ui.GetBlock("curb", "minecraft:light_gray_concrete"),
            VerandaBlock = _ui.GetBlock("walk", "minecraft:smooth_stone_slab"),
            ParapetBlock = _ui.GetBlock("rail", "minecraft:iron_bars"),
            SeatBlock = _ui.GetBlock("light", "minecraft:sea_lantern")
        };

        string pierNote = pierType switch
        {
            "wall" => "壁式",
            "frame" => "ラーメン（2本柱）",
            _ => "張出式（T型）",
        };
        string spanNote = cont && spans >= 3
            ? $"連続{spans}径間（側{sideLen}＋中央{span}）"
            : (cont ? $"連続{spans}径間" : $"単純{spans}径間");
        string laneNote = effMedian > 0
            ? $"{lanesL}＋{lanesR}車線×{laneW}（分離帯{effMedian}）"
            : $"{lanes}車線×{laneW}・分離帯なし";
        string walkNote = walk > 0 ? $"歩道{walk}×2" : "歩道なし";
        string markNote = marks ? "区画線あり（線に1マスずつ配分）" : "区画線なし";
        string lightNote = light > 0 ? $"照明{light}m間隔" : "照明なし";
        string lenNote = trimmed ? $"→上限{MaxLength}に切り詰め" : "";

        summary = $"桁橋 {spanNote} / 橋長{length}{lenNote}×全幅{deckW} / " +
                  $"桁高{girderH}（支間の1/{ratio}）/ {laneNote} / {walkNote} / {markNote} / " +
                  $"高欄{rail} / 橋脚{pierNote} 高さ{pierH} / {(abut ? "橋台あり" : "橋台なし")} / {lightNote}";
        return spec;
    }
}
