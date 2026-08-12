using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// ヘリポート。すべての寸法が D 値（設計ヘリコプターの全長）から決まる。
//
// 既定値の根拠（ICAO Annex 14 Vol.II）:
//   FATO … 1D。限定用途の地上式に限り0.83Dまで縮められる。
//   TLOF … 0.83D。FATOの中に置く。
//   セーフティエリア … FATOの外へ3mか0.25Dの大きい方。
//   TLOF縁灯 … 緑。間隔5m以下。
//   TD/PM円  … 内径0.5D。
//   Hマーキング … D<16mのとき高さ3m。
public sealed class HelipadParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // 代表的なヘリコプターの D 値（全長・m）。
    private static readonly (string Text, string Value)[] TypeItems =
    {
        ("小型（R44級・D=9m）", "9"),
        ("中型（BK117/AW109級・D=13m）", "13"),
        ("中型（AW139級・D=17m）", "17"),
        ("大型（S-92/EH101級・D=23m）", "23"),
        ("超大型（CH-47級・D=30m）", "30"),
    };

    public HelipadParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "進入方向", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("設計ヘリコプター")
           .Choice("d", "D値（全長）", TypeItems, "13")
           .Note("FATOは1D、TLOFは0.83D、セーフティエリアはFATOの外へ3mか0.25Dの大きい方。すべてここから決まる。")
           .Toggle("fullfato", "FATO 1D（標準）", "FATO 0.83D（限定用途）", true);

        _ui.Heading("縮尺")
           .Choice("scale", "1マスの大きさ", new[]
           {
               ("実寸（1マス=1m）", "1"), ("1マス=2m", "2"), ("1マス=3m", "3"),
           }, "1")
           .Note("D=30mだとセーフティエリア込みで45マス角。大きすぎるときは2〜3にする。");

        _ui.Heading("形式")
           .IntSlider("lift", "高架式の高さ", 0, 24, 0, "0で地上式。屋上・洋上のヘリポートに使う")
           .Toggle("mark", "標識を描く", "舗装のみ", true)
           .Note("TLOF外周・FATO外周・TD/PM円（内径0.5D）・Hマーキング・緑の縁灯を置く。");

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "TLOF・FATOの舗装", "minecraft:gray_concrete")
           .BlockPick("mark", "TLOF外周・TD/PM円", "minecraft:yellow_concrete")
           .BlockPick("line", "FATO外周・H標識", "minecraft:white_concrete")
           .BlockPick("shoulder", "セーフティエリア・脚", "minecraft:green_concrete")
           .BlockPick("light", "縁灯", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string pave = _ui.GetBlock("pave", "minecraft:gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(pave);

        int d = int.TryParse(_ui.GetChoice("d", "13"), out int dv) && dv > 0 ? dv : 13;
        int scale = int.TryParse(_ui.GetChoice("scale", "1"), out int s) && s > 0 ? s : 1;
        bool fullFato = _ui.GetBool("fullfato");
        bool mark = _ui.GetBool("mark");
        int lift = _ui.GetInt("lift");

        double fatoM = d * (fullFato ? 1.0 : 0.83);
        double safeM = Math.Max(3.0, d * 0.25);
        int fato = Math.Max(5, (int)Math.Round(fatoM / scale));
        if (fato % 2 == 0) fato++;
        int safe = Math.Max(1, (int)Math.Round(safeM / scale));
        int total = fato + safe * 2;

        var spec = new StructureSpec
        {
            StructureType = "airport:helipad",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = total,
            Depth = total,
            Height = Math.Max(2, lift + 2),
            AirportScale = scale,
            AirportHeliD = d,
            AirportHeliFullFato = fullFato,
            AirportHeliElevated = lift,
            AirportMarking = mark,
            FloorBlock = pave,
            AccentBlock = _ui.GetBlock("mark", "minecraft:yellow_concrete"),
            WallBlock = _ui.GetBlock("line", "minecraft:white_concrete"),
            BaseBlock = _ui.GetBlock("shoulder", "minecraft:green_concrete"),
            SeatBlock = _ui.GetBlock("light", "minecraft:sea_lantern")
        };

        string fatoNote = fullFato ? "FATO 1D" : "FATO 0.83D（限定用途）";
        string liftNote = lift > 0 ? $"高架式{lift}" : "地上式";
        string scaleNote = scale == 1 ? "実寸" : $"1マス={scale}m";

        summary = $"ヘリポート D={d}m / 全体{total}×{total}マス（{scaleNote}）/ " +
                  $"{fatoNote}・TLOF 0.83D・セーフティ{safeM:0.#}m / {liftNote} / " +
                  $"{(mark ? "標識・縁灯あり" : "舗装のみ")}";
        return spec;
    }
}
