using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// アーチ橋。1マス=1m。
//
// 既定値の根拠（実寸）:
//   ライズ比 … 支間の1/5〜1/10（日本大百科全書）。既定1/5。
//   適用支間 … タイドアーチで50〜170m級。世界最大は支間550m（上海盧浦大橋）。
//   形式     … 上路式はアーチが床版の下、下路式は上、中路式はその中間。
//   タイ材   … 下路式のタイドアーチは水平反力を橋自身で受ける（ランガー/ローゼ等）。
public sealed class ArchBridgeParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    private const int MaxLength = 256;

    public ArchBridgeParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "橋が渡る向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("アーチ")
           .Choice("type", "形式", new[]
           {
               ("上路式（アーチが下）", "deck"),
               ("下路式（アーチが上）", "through"),
               ("中路式", "half"),
           }, "deck")
           .IntSlider("span", "アーチ支間", 16, 200, 60, "起拱点どうしの距離")
           .IntSlider("rise", "ライズ比 1/n", 4, 10, 5, "支間の1/nをアーチの高さにする")
           .IntSlider("vstep", "鉛直材の間隔", 2, 20, 5, "上路式は支柱、下路式は吊材の間隔")
           .IntSlider("spring", "起拱点の高さ", 0, 40, 4, "地面からアーチの付け根まで")
           .BeginChoiceGroup("type", "deck")
             .IntSlider("ribs", "アーチリブの本数", 2, 4, 2, "上路式のみ。床版の下に等間隔")
           .EndGroup()
           .BeginChoiceGroup("type", "through", "half")
             .Toggle("tie", "タイ材を入れる（タイドアーチ）", "タイ材なし", true)
           .EndGroup()
           .Toggle("brace", "リブ間の横構を入れる", "横構なし", true)
           .Toggle("abut", "取付部を作る", "アーチだけ", true)
           .Note("下路式・中路式のリブは床版の外側に立つので、車道の幅は変わらない。");

        BridgeSectionUi.AddSection(_ui);

        _ui.Heading("付帯設備")
           .IntSlider("light", "照明の間隔", 0, 80, 30, "0で照明なし。実物は30m級");

        BridgeSectionUi.AddCommonBlocks(_ui);
        _ui.BlockPick("cable", "アーチリブ・横構", "minecraft:polished_andesite")
           .BlockPick("hanger", "支柱・吊材", "minecraft:iron_bars");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add("minecraft:black_concrete");

        string type = _ui.GetChoice("type", "deck");
        int span = _ui.GetInt("span");
        int riseR = _ui.GetInt("rise");
        int rise = Math.Max(3, span / riseR);
        int springH = _ui.GetInt("spring");
        bool abut = _ui.GetBool("abut");
        int approach = abut ? Math.Max(4, span / 8) : 0;

        int crownY = springH + rise;
        int deckY = type switch
        {
            "deck" => crownY + 2,
            "half" => springH + rise / 2,
            _ => springH,
        };

        int deckW = BridgeSectionUi.DeckWidth(_ui);
        int length = span + approach * 2;
        bool trimmed = length > MaxLength;
        if (trimmed) length = MaxLength;

        int railTop = deckY + BridgeSectionUi.TopHeight(_ui);
        int lightH = _ui.GetInt("light") > 0 ? 4 : 0;
        int height = Math.Max(crownY, railTop + lightH) + 1;
        int width = deckW + 2;

        var spec = new StructureSpec
        {
            StructureType = "bridge:arch_bridge",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = width,
            Depth = length,
            Height = height,
            BridgeSpan = span,
            BridgeArchType = type,
            BridgeRiseRatio = riseR,
            BridgeArchRibs = _ui.GetInt("ribs"),
            BridgeVerticalStep = _ui.GetInt("vstep"),
            BridgeTie = _ui.GetBool("tie"),
            BridgeBracing = _ui.GetBool("brace"),
            BridgePierHeight = springH,
            BridgeAbutment = abut,
            BridgeLightStep = _ui.GetInt("light"),
        };

        BridgeSectionUi.ApplySection(_ui, spec);
        BridgeSectionUi.ApplyCommonBlocks(_ui, spec);
        spec.TowerRoofBlock = _ui.GetBlock("cable", "minecraft:polished_andesite");
        spec.GlazingBlock = _ui.GetBlock("hanger", "minecraft:iron_bars");

        string typeNote = type switch
        {
            "through" => "下路式",
            "half" => "中路式",
            _ => "上路式",
        };
        string tieNote = type != "deck" && _ui.GetBool("tie") ? " / タイドアーチ" : "";
        string lenNote = trimmed ? $"→上限{MaxLength}に切り詰め" : "";

        summary = $"アーチ橋 {typeNote} 支間{span} / 橋長{length}{lenNote}×全幅{deckW} / " +
                  $"ライズ{rise}（1/{riseR}）/ 鉛直材{_ui.GetInt("vstep")}間隔 / " +
                  $"起拱点の高さ{springH}{tieNote} / {BridgeSectionUi.SectionSummary(_ui)}";
        return spec;
    }
}
