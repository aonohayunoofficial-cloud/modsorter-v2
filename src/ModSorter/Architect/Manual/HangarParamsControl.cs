using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 格納庫。1マス=1m で組むので、縮尺1のエプロン・ターミナルと寸法が合う。
//
// 既定値の根拠（実寸）:
//   扉の高さ … NFPA 409 は 28ft（8.5m）超を Group I とし消火設備の要求が上がる。
//              2026 年の改訂でこの境が 35ft（10.7m）へ上がった。
//   尾翼高さ … CRJ200 6.2m / A320 11.8m / B777 18.5m / A380 24.1m。
//              扉の高さは尾翼高さ＋1〜1.5m のクリアランスを取る。
//   扉の幅   … 翼幅＋両側のクリアランス。エプロンのスポット幅と同じ値を使う。
//   実例     … A380 対応で 幅45m×奥行62m×有効高さ18m。
public sealed class HangarParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // 機体サイズごとの寸法。扉幅はエプロンのスポット幅と同じ値。
    private readonly struct HangarSize
    {
        public readonly string Label;
        public readonly int Span;      // 扉の開口幅
        public readonly int Depth;     // 奥行き
        public readonly int TailH;     // 尾翼高さ
        public readonly int Clear;     // 庫内の有効高さ

        public HangarSize(string label, int span, int depth, int tailH, int clear)
        {
            Label = label;
            Span = span;
            Depth = depth;
            TailH = tailH;
            Clear = clear;
        }
    }

    private static HangarSize SizeOf(string key) => key switch
    {
        "s" => new HangarSize("S 小型機（CRJ200級・尾翼6.2m）", 27, 33, 6, 10),
        "l" => new HangarSize("L 大型機（B777/787級・尾翼18.5m）", 81, 83, 19, 24),
        "ll" => new HangarSize("LL 超大型機（A380級・尾翼24.1m）", 95, 83, 25, 30),
        _ => new HangarSize("M 中型機（A320/737級・尾翼11.8m）", 45, 47, 12, 16),
    };

    private static readonly (string Text, string Value)[] SizeItems =
    {
        ("S 小型機（CRJ200級・扉幅27m）", "s"),
        ("M 中型機（A320/737級・扉幅45m）", "m"),
        ("L 大型機（B777/787級・扉幅81m）", "l"),
        ("LL 超大型機（A380級・扉幅95m）", "ll"),
    };

    // NFPA 409 の区分点。28ft と 2026 年改訂後の 35ft。
    private const int GroupIOld = 9;   // 28ft ≒ 8.5m。これを超えると旧基準では Group I
    private const int GroupINew = 11;  // 35ft ≒ 10.7m。2026 年改訂後の境

    public HangarParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "扉の向き（エプロン側）", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("収める機体")
           .Choice("size", "機体サイズ", SizeItems, "m")
           .Note("扉幅は翼幅＋両側クリアランスで、エプロンのスポット幅と同じ値。尾翼高さはCRJ200 6.2m / A320 11.8m / B777 18.5m / A380 24.1m。")
           .IntSlider("bays", "収める機数", 1, 4, 1, "扉の開口が機数ぶん横に伸びる");

        _ui.Heading("寸法")
           .Toggle("custom", "手動で寸法を指定", "機体サイズから自動", false)
           .BeginGroup("custom")
               .IntSlider("span", "扉の開口幅", 11, 128, 45)
               .IntSlider("depth", "奥行き", 12, 96, 47)
               .IntSlider("clear", "庫内の有効高さ", 6, 32, 16)
               .IntSlider("doorh", "扉の高さ", 4, 32, 13)
           .EndGroup()
           .Note("自動のときは扉高さ＝尾翼高さ＋1、有効高さは機体を持ち上げても収まる高さになる。");

        _ui.Heading("構造")
           .Choice("roof", "屋根", new[]
           {
               ("アーチトラス", "arch"), ("陸屋根", "flat"), ("片流れ", "shed"),
           }, "arch")
           .Choice("door", "扉の形式", new[]
           {
               ("引き分け戸", "slide"), ("折り戸", "fold"), ("扉なし（開口のみ）", "open"),
           }, "slide")
           .IntSlider("annex", "附属棟の奥行き", 0, 24, 0, "0でなし。側面に付く2層の別棟。工場・部品庫・事務所");

        _ui.Heading("使用ブロック")
           .BlockPick("body", "躯体・トラス", "minecraft:light_gray_concrete")
           .BlockPick("glass", "扉の窓・高窓", "minecraft:glass")
           .BlockPick("frame", "まぐさ・柱", "minecraft:gray_concrete")
           .BlockPick("floor", "床", "minecraft:smooth_stone")
           .BlockPick("roof", "屋根", "minecraft:light_blue_terracotta")
           .BlockPick("door", "扉", "minecraft:iron_block")
           .BlockPick("lamp", "庫内の照明", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        var size = SizeOf(_ui.GetChoice("size", "m"));
        string body = _ui.GetBlock("body", "minecraft:light_gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(body);

        bool custom = _ui.GetBool("custom");
        int bays = _ui.GetInt("bays");
        int span = custom ? _ui.GetInt("span") : size.Span;
        int depth = custom ? _ui.GetInt("depth") : size.Depth;
        int clear = custom ? _ui.GetInt("clear") : size.Clear;
        int doorH = custom ? _ui.GetInt("doorh") : size.TailH + 1;
        if (doorH > clear) doorH = clear;

        string roof = _ui.GetChoice("roof", "arch");
        string door = _ui.GetChoice("door", "slide");
        int annex = _ui.GetInt("annex");

        var spec = new StructureSpec
        {
            StructureType = "airport:hangar",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = span,
            Depth = depth,
            Height = clear,
            AirportBays = bays,
            AirportDoorHeight = doorH,
            AirportHangarRoof = roof,
            AirportDoorType = door,
            AirportAnnex = annex,
            TowerBlock = body,
            WallBlock = body,
            GlazingBlock = _ui.GetBlock("glass", "minecraft:glass"),
            AccentBlock = _ui.GetBlock("frame", "minecraft:gray_concrete"),
            FloorBlock = _ui.GetBlock("floor", "minecraft:smooth_stone"),
            RoofBlock = _ui.GetBlock("roof", "minecraft:light_blue_terracotta"),
            ParapetBlock = _ui.GetBlock("door", "minecraft:iron_block"),
            SeatBlock = _ui.GetBlock("lamp", "minecraft:sea_lantern")
        };

        int total = (span % 2 == 0 ? span + 1 : span) * bays;
        string group = doorH > GroupINew
            ? "NFPA409 GroupI（35ft超）"
            : (doorH > GroupIOld ? "35ft以下（2026年改訂でGroupII）" : "28ft以下（GroupII）");
        string roofNote = roof switch { "flat" => "陸屋根", "shed" => "片流れ", _ => "アーチトラス" };
        string doorNote = door switch { "fold" => "折り戸", "open" => "扉なし", _ => "引き分け戸" };
        string annexNote = annex >= 4 ? $"附属棟{annex}" : "附属棟なし";
        string baysNote = bays > 1 ? $"{bays}機 " : "";

        summary = $"格納庫 {size.Label} {baysNote}/ 開口{total}×奥行き{depth}・有効高さ{clear} / " +
                  $"扉高さ{doorH}（{group}）/ {roofNote} / {doorNote} / {annexNote}";
        return spec;
    }
}
