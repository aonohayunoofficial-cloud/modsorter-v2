using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 跳開橋。1マス=1m。固定トラニオン式。
//
// 既定値の根拠（実寸）:
//   跳開角 … 勝鬨橋は70秒で70度まで開く設計（土木学会）。0で閉じた状態。
//   葉数   … 単葉（片持ち1枚）と双葉（中央で突き合わせる2枚）。
//   桁高   … 支間長の1/20級。
//   機械室 … 巻き上げ機械を主橋脚の上・路肩の外側に収める。
public sealed class BasculeBridgeParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    private const int MaxLength = 256;
    private const int PierLen = 6;

    public BasculeBridgeParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "橋が渡る向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("可動径間")
           .Choice("leaves", "葉数", new[] { ("双葉", "2"), ("単葉", "1") }, "2")
           .IntSlider("leaf", "1葉の長さ", 6, 60, 20, "航路の幅は葉数×この長さ")
           .IntSlider("angle", "跳開角", 0, 70, 30, "0で閉じた状態。実物の全開は70度")
           .Toggle("cw", "釣合い錘を作る", "釣合い錘なし", true)
           .Toggle("house", "機械室を作る", "機械室なし", true)
           .Note("可動桁は1マス階段状に積んで傾きを表す。");

        _ui.Heading("固定径間")
           .IntSlider("spans", "片側の径間数", 0, 6, 1, "0で可動径間だけ")
           .IntSlider("span", "1径間の長さ", 8, 80, 20)
           .IntSlider("ratio", "桁高比 1/n", 12, 30, 20, "支間長の1/nを桁高にする")
           .IntSlider("cross", "横桁の間隔", 0, 24, 6, "0で横桁なし")
           .IntSlider("clear", "桁下高", 2, 40, 8, "閉じているときの航路の桁下高")
           .Toggle("abut", "橋台と取付部を作る", "橋桁だけ", true);

        BridgeSectionUi.AddSection(_ui);

        _ui.Heading("付帯設備")
           .IntSlider("light", "照明の間隔", 0, 80, 30, "可動桁には立てない。0で照明なし");

        BridgeSectionUi.AddCommonBlocks(_ui);

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add("minecraft:black_concrete");

        int leaves = _ui.GetChoice("leaves", "2") == "1" ? 1 : 2;
        int leafLen = _ui.GetInt("leaf");
        int angle = _ui.GetInt("angle");
        bool house = _ui.GetBool("house");

        int fixedSpans = _ui.GetInt("spans");
        int fixedSpan = _ui.GetInt("span");
        int clear = _ui.GetInt("clear");
        int ratio = _ui.GetInt("ratio");
        int gh = Math.Clamp((int)Math.Round(Math.Max(fixedSpan, leafLen) / (double)ratio), 1, 6);

        int deckW = BridgeSectionUi.DeckWidth(_ui);
        int gap = leaves * leafLen;
        int length = fixedSpans * fixedSpan * 2 + PierLen * 2 + gap
                   + (_ui.GetBool("abut") ? 6 : 0);
        bool trimmed = length > MaxLength;
        if (trimmed) length = MaxLength;

        int deckY = clear + gh;
        int lift = (int)Math.Round(leafLen * Math.Sin(angle * Math.PI / 180.0));
        int height = deckY + lift + BridgeSectionUi.TopHeight(_ui) + 1;
        int width = deckW + (house ? 10 : 0);

        var spec = new StructureSpec
        {
            StructureType = "bridge:bascule_bridge",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = width,
            Depth = length,
            Height = height,
            BridgeLeaves = leaves,
            BridgeLeafSpan = leafLen,
            BridgeOpenAngle = angle,
            BridgeCounterweight = _ui.GetBool("cw"),
            BridgeMachineHouse = house,
            BridgeSpans = fixedSpans,
            BridgeSpan = fixedSpan,
            BridgeDepthRatio = ratio,
            BridgeCrossStep = _ui.GetInt("cross"),
            BridgePierHeight = clear,
            BridgeAbutment = _ui.GetBool("abut"),
            BridgeLightStep = _ui.GetInt("light"),
        };

        BridgeSectionUi.ApplySection(_ui, spec);
        BridgeSectionUi.ApplyCommonBlocks(_ui, spec);

        string leafNote = leaves == 2 ? "双葉" : "単葉";
        string angleNote = angle == 0 ? "閉じた状態" : $"跳開{angle}度（先端が{lift}上がる）";
        string fixNote = fixedSpans > 0 ? $"固定{fixedSpan}×{fixedSpans}×2" : "固定径間なし";
        string lenNote = trimmed ? $"→上限{MaxLength}に切り詰め" : "";

        summary = $"跳開橋 {leafNote} 航路{gap}（1葉{leafLen}）/ {angleNote} / " +
                  $"橋長{length}{lenNote}×全幅{deckW} / 桁高{gh}（1/{ratio}）/ 桁下{clear} / " +
                  $"{fixNote} / {(_ui.GetBool("cw") ? "釣合い錘あり" : "釣合い錘なし")} / " +
                  $"{(house ? "機械室あり" : "機械室なし")} / {BridgeSectionUi.SectionSummary(_ui)}";
        return spec;
    }
}
