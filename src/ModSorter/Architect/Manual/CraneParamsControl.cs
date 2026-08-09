using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// クレーン2種（ガントリークレーン・橋形クレーン）。骨格は門形の脚＋横行桁で共通なので、
// コンストラクタで種類を受けて既定値と表示を切り替える。
// ManualCatalog からは new CraneParamsControl("gantry") のように種類を渡す。
// 座標生成は HarborExpander が受け持つので、ここは structure_type="harbor:<種類>" と
// 骨格の寸法・素材を渡すだけ。
//
// 既定値の根拠（実寸）:
//   ガントリークレーン … 軌間 30.48m（100ft）が標準。アウトリーチ 38〜60m、
//     バックリーチ 8〜28m、全揚程 45m 級。海側のブームは起伏式で不使用時に跳ね上がる。
//     脚は 2〜3m 角の箱断面で、走行方向の脚間隔は 16m 前後。
//   橋形クレーン … 荷役ヤードで使う門形。スパン 23〜26m・揚程 15〜18m、
//     両端の張り出し（カンチレバー）は 5〜15m。ブームの起伏は持たない。
public sealed class CraneParamsControl : UserControl, IManualParamControl
{
    private readonly bool _tall;   // true=ガントリークレーン / false=橋形クレーン
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public CraneParamsControl(string kind)
    {
        _tall = !string.Equals(kind, "bridgecrane", StringComparison.OrdinalIgnoreCase);
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("sea", "海側", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        if (_tall)
        {
            _ui.Heading("骨格")
               .Note("軌間30mが100ft相当の標準。脚高32m・アウトリーチ38mが実物の目安。")
               .IntSlider("gauge", "軌間", 6, 40, 30, "海側レールと陸側レールの間隔")
               .IntSlider("legH", "脚の高さ", 6, 56, 32, "レール面から横行桁の下端まで")
               .IntSlider("legS", "脚の太さ", 1, 5, 2, "箱断面の一辺。実物は2〜3m")
               .IntSlider("legB", "走行方向の脚間隔", 4, 40, 16);

            _ui.Heading("桁の張り出し")
               .Note("アウトリーチは船幅を跨ぐ長さ。バックリーチは陸側の荷置き場ぶん。")
               .IntSlider("outr", "アウトリーチ", 0, 60, 38)
               .IntSlider("back", "バックリーチ", 0, 30, 14)
               .IntSlider("raise", "ブームの起伏", 0, 4, 0, "0で水平。1で急に跳ね上げる");

            _ui.Heading("付属")
               .Toggle("mach", "機械室・運転室あり", "骨格のみ", true);
        }
        else
        {
            _ui.Heading("骨格")
               .Note("荷役ヤードの門形。スパン23〜26m・揚程15〜18mが実物の目安。")
               .IntSlider("gauge", "スパン", 6, 40, 24, "脚と脚の間隔")
               .IntSlider("legH", "脚の高さ", 6, 40, 16, "レール面から横行桁の下端まで")
               .IntSlider("legS", "脚の太さ", 1, 5, 2)
               .IntSlider("legB", "走行方向の脚間隔", 4, 40, 10);

            _ui.Heading("桁の張り出し")
               .Note("両端のカンチレバー。実物は5〜15m。")
               .IntSlider("outr", "海側の張り出し", 0, 30, 8)
               .IntSlider("back", "陸側の張り出し", 0, 30, 8);

            _ui.Heading("付属")
               .Toggle("mach", "機械室・運転室あり", "骨格のみ", true);
        }

        _ui.Heading("使用ブロック")
           .BlockPick("body", "脚・主構", "minecraft:light_blue_concrete")
           .BlockPick("trim", "桁・梁・走行装置", "minecraft:white_concrete")
           .BlockPick("pave", "機械室", "minecraft:gray_concrete")
           .BlockPick("fitting", "運転室", "minecraft:glass");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        int gauge = _ui.GetInt("gauge");
        int legH = _ui.GetInt("legH");
        int legS = _ui.GetInt("legS");
        int legB = _ui.GetInt("legB");
        int outr = _ui.GetInt("outr");
        int back = _ui.GetInt("back");
        int raise = _tall ? _ui.GetInt("raise") : 0;
        bool mach = _ui.GetBool("mach");

        string body = _ui.GetBlock("body", "minecraft:light_blue_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        var spec = new StructureSpec
        {
            StructureType = _tall ? "harbor:crane" : "harbor:bridgecrane",
            FacadeFace = _ui.GetChoice("sea", "south"),
            Width = legB + legS,                 // 参考値。骨格は harbor_* から組む
            Depth = outr + gauge + back,         // 参考値
            Height = legH + 8,                   // 参考値
            HarborGauge = gauge,
            HarborLegHeight = legH,
            HarborLegSize = legS,
            HarborLegBase = legB,
            HarborOutreach = outr,
            HarborBackreach = back,
            HarborBoomRaise = raise,
            HarborMachinery = mach,
            WallBlock = body,
            AccentBlock = _ui.GetBlock("trim", "minecraft:white_concrete"),
            FloorBlock = _ui.GetBlock("pave", "minecraft:gray_concrete"),
            SeatBlock = _ui.GetBlock("fitting", "minecraft:glass")
        };

        string name = _tall ? "ガントリークレーン" : "橋形クレーン";
        string boomNote = _tall ? (raise > 0 ? $"ブーム起伏{raise}" : "ブーム水平") : "カンチレバー";
        string machNote = mach ? "機械室・運転室あり" : "骨格のみ";
        summary = $"{name} 軌間{gauge}・脚高{legH} / 海側{outr}＋陸側{back} / {boomNote} / {machNote}";
        return spec;
    }
}
