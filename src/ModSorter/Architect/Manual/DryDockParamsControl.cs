using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// ドライドック（乾ドック）。地面を掘り込んだ船の修繕設備。
// 座標生成は HarborExpander が受け持つので、ここは structure_type="harbor:drydock" と
// 掘り込みの寸法・素材を渡すだけ。
//
// 既定値の根拠（実寸）:
//   中型ドックは全長 200m 級・幅 30〜40m・深さ 10m 級。側壁は作業段（アルター）が
//   段状に下り、盤木（キールブロック）が中心線上に 1.2〜2m 間隔で並ぶ。
//   海側の入口はケーソンゲート（浮きドア）で閉じる。
//   全長は 64 マス上限のため、実物の中央部を切り出す粒度にしてある。
public sealed class DryDockParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public DryDockParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("sea", "海側(ゲート側)", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("規模")
           .Note("中型ドックは全長200m級・幅30〜40m・深さ10m級。ここは中央部を切り出す。")
           .IntSlider("len", "全長", 16, 64, 56)
           .IntSlider("wide", "内幅", 10, 40, 34)
           .IntSlider("deep", "深さ", 4, 20, 10);

        _ui.Heading("側壁")
           .Note("作業段（アルター）。側壁を段状に下げ、盤木の据付と足場を兼ねる。")
           .IntSlider("steps", "作業段の段数", 0, 6, 3, "0で垂直の側壁");

        _ui.Heading("盤木")
           .Note("キールブロック。中心線上に並ぶ船底の受け台。実物は1.2〜2m間隔。")
           .Toggle("keel", "盤木あり", "盤木なし", true)
           .BeginGroup("keel")
           .IntSlider("keelStep", "盤木の間隔", 2, 8, 2)
           .EndGroup();

        _ui.Heading("ゲート")
           .Note("ケーソンゲート。海側の入口を塞ぐ扉体。")
           .Toggle("gate", "ゲートあり", "開口のまま", true)
           .BeginGroup("gate")
           .IntSlider("gateT", "ゲートの厚み", 1, 6, 3)
           .EndGroup();

        _ui.Heading("付帯設備")
           .Toggle("bollard", "係船柱あり", "係船柱なし", true)
           .BeginGroup("bollard")
           .IntSlider("bstep", "係船柱の間隔", 10, 45, 20)
           .EndGroup();

        _ui.Heading("使用ブロック")
           .BlockPick("body", "本体コンクリート", "minecraft:gray_concrete")
           .BlockPick("pave", "縁の舗装", "minecraft:light_gray_concrete")
           .BlockPick("rubble", "盤木の台", "minecraft:cobblestone")
           .BlockPick("trim", "縁石・ゲート・盤木", "minecraft:polished_andesite")
           .BlockPick("fitting", "係船柱", "minecraft:black_concrete");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int len = _ui.GetInt("len");
        int wide = _ui.GetInt("wide");
        int deep = _ui.GetInt("deep");
        int steps = _ui.GetInt("steps");
        bool keel = _ui.GetBool("keel");
        bool gate = _ui.GetBool("gate");
        bool bollard = _ui.GetBool("bollard");

        string body = _ui.GetBlock("body", "minecraft:gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        var spec = new StructureSpec
        {
            StructureType = "harbor:drydock",
            FacadeFace = _ui.GetChoice("sea", "south"),
            Width = len,
            Depth = wide + 14,          // 参考値。断面は harbor_* から組む
            Height = deep + 1,          // 参考値
            HarborDepth = deep,
            HarborBody = wide,
            HarborAltarSteps = steps,
            HarborKeelStep = keel ? _ui.GetInt("keelStep") : 0,
            HarborGate = gate ? _ui.GetInt("gateT") : 0,
            HarborBollardStep = bollard ? _ui.GetInt("bstep") : 0,
            WallBlock = body,
            FloorBlock = _ui.GetBlock("pave", "minecraft:light_gray_concrete"),
            BaseBlock = _ui.GetBlock("rubble", "minecraft:cobblestone"),
            AccentBlock = _ui.GetBlock("trim", "minecraft:polished_andesite"),
            SeatBlock = _ui.GetBlock("fitting", "minecraft:black_concrete")
        };

        string keelNote = keel ? $"盤木{spec.HarborKeelStep}間隔" : "盤木なし";
        string gateNote = gate ? $"ゲート厚{spec.HarborGate}" : "開口のまま";
        summary = $"ドライドック {len}×{wide}×深さ{deep} / 作業段{steps} / {keelNote} / {gateNote}";
        return spec;
    }
}
