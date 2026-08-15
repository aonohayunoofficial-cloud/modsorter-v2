using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 吊り橋。1マス=1m。
//
// 既定値の根拠（実寸）:
//   サグ比       … 1/10前後（安芸灘大橋 サグ74.0m・中央支間750m）。
//   側径間       … 中央径間の20〜60%。既定40%。
//   ハンガー間隔 … 10〜20m級（明石海峡大橋14m）。
//   主塔高       … 床版上にサグ＋余裕。0で自動。
//   補剛桁高     … 実橋は支間の1/100級。1マス=1mでは潰れるので最小2マス。
public sealed class SuspensionBridgeParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    private const int MaxLength = 256;

    public SuspensionBridgeParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "橋が渡る向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("支間割")
           .IntSlider("span", "中央径間", 40, 240, 120, "主塔の間の長さ")
           .IntSlider("side", "側径間比%", 20, 60, 40, "中央径間に対する側径間の比")
           .Toggle("anchor", "アンカレイジを作る", "ケーブル定着体なし", true)
           .IntSlider("clear", "桁下高", 4, 60, 14, "地面から補剛桁の下端まで");

        _ui.Heading("ケーブル")
           .IntSlider("sag", "サグ比 1/n", 8, 12, 10, "中央径間の1/nをケーブルの垂れ下がりにする")
           .IntSlider("hang", "ハンガー間隔", 2, 30, 10, "実物は10〜20m級");

        _ui.Heading("主塔")
           .Choice("tower", "主塔の形式", new[]
           {
               ("門型", "portal"), ("H型", "h"), ("トラス塔", "truss"),
           }, "portal")
           .IntSlider("towerh", "床版上の塔高", 0, 160, 0, "0でサグから自動");

        _ui.Heading("補剛桁")
           .IntSlider("stiff", "補剛桁高", 1, 8, 2)
           .IntSlider("cross", "横桁の間隔", 0, 24, 6, "0で横桁なし");

        BridgeSectionUi.AddSection(_ui);

        _ui.Heading("付帯設備")
           .IntSlider("light", "照明の間隔", 0, 80, 30, "0で照明なし。実物は30m級");

        BridgeSectionUi.AddCommonBlocks(_ui);
        _ui.BlockPick("cable", "主ケーブル", "minecraft:polished_andesite")
           .BlockPick("hanger", "ハンガー", "minecraft:iron_bars");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add("minecraft:black_concrete");

        int main = _ui.GetInt("span");
        int sideR = _ui.GetInt("side");
        int side = Math.Max(8, main * sideR / 100);
        bool anchor = _ui.GetBool("anchor");
        int anchorLen = anchor ? Math.Max(6, side / 5) : 0;

        int clear = _ui.GetInt("clear");
        int stiff = _ui.GetInt("stiff");
        int sagR = _ui.GetInt("sag");
        int sag = Math.Max(3, main / sagR);

        int towerAbove = _ui.GetInt("towerh");
        if (towerAbove <= 0) towerAbove = sag + Math.Max(3, sag / 4);
        if (towerAbove < sag + 2) towerAbove = sag + 2;

        int deckW = BridgeSectionUi.DeckWidth(_ui);
        int length = main + side * 2 + anchorLen * 2;
        bool trimmed = length > MaxLength;
        if (trimmed) length = MaxLength;

        int width = deckW + (anchor ? 6 : 2);        // 主塔・アンカレイジが床版の外へ出る
        int height = clear + stiff + towerAbove + 1;

        var spec = new StructureSpec
        {
            StructureType = "bridge:suspension_bridge",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = width,
            Depth = length,
            Height = height,
            BridgeSpan = main,
            BridgeSideRatio = sideR,
            BridgeSagRatio = sagR,
            BridgeHangerStep = _ui.GetInt("hang"),
            BridgeTowerType = _ui.GetChoice("tower", "portal"),
            BridgeTowerHeight = _ui.GetInt("towerh"),
            BridgeStiffenDepth = stiff,
            BridgeCrossStep = _ui.GetInt("cross"),
            BridgeAnchorage = anchor,
            BridgePierHeight = clear,
            BridgeLightStep = _ui.GetInt("light"),
        };

        BridgeSectionUi.ApplySection(_ui, spec);
        BridgeSectionUi.ApplyCommonBlocks(_ui, spec);
        spec.TowerRoofBlock = _ui.GetBlock("cable", "minecraft:polished_andesite");
        spec.GlazingBlock = _ui.GetBlock("hanger", "minecraft:iron_bars");

        string towerNote = _ui.GetChoice("tower", "portal") switch
        {
            "h" => "H型",
            "truss" => "トラス塔",
            _ => "門型",
        };
        string lenNote = trimmed ? $"→上限{MaxLength}に切り詰め" : "";

        summary = $"吊り橋 中央{main}＋側{side}×2{(anchor ? $"＋アンカレイジ{anchorLen}×2" : "")} / " +
                  $"橋長{length}{lenNote}×全幅{deckW} / サグ{sag}（1/{sagR}）/ " +
                  $"主塔{towerNote} 床版上{towerAbove} / ハンガー{_ui.GetInt("hang")}間隔 / " +
                  $"補剛桁高{stiff} / 桁下{clear} / {BridgeSectionUi.SectionSummary(_ui)}";
        return spec;
    }
}
