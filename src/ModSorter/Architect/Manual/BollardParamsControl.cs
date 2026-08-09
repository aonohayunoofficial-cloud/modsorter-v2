using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 係船柱の単体。岸壁・桟橋の側でも係船柱は自動配置されるが、
// こちらは金物そのものを大きく作って単体で出力する中分類。
// 座標生成は HarborExpander が受け持つので、ここは structure_type="harbor:bollard" と
// 柱径・高さ・台座を渡すだけ。
//
// 既定値の根拠（実寸）:
//   柱径 0.3〜0.6m・高さ 0.5〜1m が実物だが、1マス=1m では潰れて形が出ないため、
//   既定は径3・高さ4として金物の形（曲柱の張り出した頭部／直柱の細り）が分かる
//   大きさにしてある。実寸どおりに置くなら径1・高さ1にする。
public sealed class BollardParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public BollardParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("形式")
           .Note("曲柱は頭部が張り出したキノコ形、直柱は上へ細くなる円柱。")
           .Choice("type", "係船柱の形", new[]
           {
               ("曲柱 (bitt)", "bitt"),
               ("直柱 (bollard)", "bollard"),
           }, "bitt");

        _ui.Heading("寸法")
           .Note("実寸は径0.3〜0.6m・高さ0.5〜1m。1マス=1mでは潰れるので拡大が既定。")
           .IntSlider("dia", "柱径", 1, 9, 3)
           .IntSlider("h", "柱の高さ", 2, 16, 4)
           .IntSlider("ped", "台座の一辺", 0, 21, 7, "0で台座なし");

        _ui.Heading("使用ブロック")
           .BlockPick("fitting", "柱身", "minecraft:black_concrete")
           .BlockPick("trim", "頭部", "minecraft:polished_andesite")
           .BlockPick("pave", "台座", "minecraft:light_gray_concrete");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int dia = _ui.GetInt("dia");
        int h = _ui.GetInt("h");
        int ped = _ui.GetInt("ped");
        string type = _ui.GetChoice("type", "bitt");

        string fitting = _ui.GetBlock("fitting", "minecraft:black_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(fitting);

        int span = Math.Max(ped, dia + 2);
        var spec = new StructureSpec
        {
            StructureType = "harbor:bollard",
            Width = span,                       // 参考値
            Depth = span,                       // 参考値
            Height = h + (ped > 0 ? 1 : 0) + 2, // 参考値
            HarborBollardType = type,
            HarborBollardSize = dia,
            HarborBollardHeight = h,
            HarborPedestal = ped,
            SeatBlock = fitting,
            AccentBlock = _ui.GetBlock("trim", "minecraft:polished_andesite"),
            FloorBlock = _ui.GetBlock("pave", "minecraft:light_gray_concrete")
        };

        string name = type == "bitt" ? "曲柱" : "直柱";
        string pedNote = ped > 0 ? $"台座{ped}角" : "台座なし";
        summary = $"係船柱({name}) 径{dia}・高さ{h} / {pedNote}";
        return spec;
    }
}
