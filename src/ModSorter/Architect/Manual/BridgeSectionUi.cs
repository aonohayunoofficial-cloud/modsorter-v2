using System;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 橋梁の小分類で共通の「横断構成」と「使用ブロック」。
// 全幅の式は BridgeExpander.MakeSection と必ず一致させること。
internal static class BridgeSectionUi
{
    public static void AddSection(ParamPanelBuilder ui, int lanes = 2, int walk = 2, int rail = 1)
    {
        ui.Heading("横断構成")
          .IntSlider("lanes", "車線数", 1, 6, lanes)
          .IntSlider("lanew", "車線の幅", 3, 4, 3, "1車線の幅。実物は3.25〜3.5m")
          .IntSlider("median", "分離帯幅", 0, 6, 0, "中央分離帯。0でなし。1車線のときは無効")
          .IntSlider("walk", "歩道幅", 0, 6, walk, "片側の歩道幅。0で歩道なし（地覆だけ）")
          .IntSlider("rail", "高欄の高さ", 0, 3, rail, "実物1.1m。1が実寸相当")
          .Toggle("mark", "区画線を描く", "区画線なし", true)
          .Note("区画線は専用の1マス列を占める。車線幅は指定どおり保たれ、" +
                "全幅が線の本数ぶん広がる（実物の線幅は0.15m）。");
    }

    public static int DeckWidth(ParamPanelBuilder ui)
    {
        int lanes = ui.GetInt("lanes");
        int laneW = ui.GetInt("lanew");
        int median = lanes < 2 ? 0 : ui.GetInt("median");
        int walk = ui.GetInt("walk");
        int markW = ui.GetBool("mark") ? 1 : 0;

        int lanesL = median > 0 ? (lanes + 1) / 2 : lanes;
        int lanesR = median > 0 ? lanes - lanesL : 0;
        int Carriage(int n) => n <= 0 ? 0 : n * laneW + (n - 1) * markW;

        int roadW = markW
                  + (median > 0 ? Carriage(lanesL) + median + Carriage(lanesR) : Carriage(lanes))
                  + markW;
        int edge = walk > 0 ? walk + 1 : 1;
        return roadW + edge * 2;
    }

    // 床版から高欄天端までの高さ。
    public static int TopHeight(ParamPanelBuilder ui)
        => (ui.GetInt("walk") > 0 ? 2 : 1) + ui.GetInt("rail");

    public static void ApplySection(ParamPanelBuilder ui, StructureSpec spec)
    {
        spec.BridgeLanes = ui.GetInt("lanes");
        spec.BridgeLaneWidth = ui.GetInt("lanew");
        spec.BridgeMedian = ui.GetInt("median");
        spec.BridgeSidewalk = ui.GetInt("walk");
        spec.BridgeRailing = ui.GetInt("rail");
        spec.BridgeLaneMark = ui.GetBool("mark");
    }

    public static string SectionSummary(ParamPanelBuilder ui)
    {
        int lanes = ui.GetInt("lanes");
        int laneW = ui.GetInt("lanew");
        int median = lanes < 2 ? 0 : ui.GetInt("median");
        int walk = ui.GetInt("walk");

        int lanesL = median > 0 ? (lanes + 1) / 2 : lanes;
        string laneNote = median > 0
            ? $"{lanesL}＋{lanes - lanesL}車線×{laneW}（分離帯{median}）"
            : $"{lanes}車線×{laneW}・分離帯なし";
        string walkNote = walk > 0 ? $"歩道{walk}×2" : "歩道なし";
        string markNote = ui.GetBool("mark") ? "区画線あり" : "区画線なし";
        return $"{laneNote} / {walkNote} / {markNote} / 高欄{ui.GetInt("rail")}";
    }

    // 4形式で共通のブロック。形式固有のもの（ケーブル等）は各コントロールで足す。
    public static void AddCommonBlocks(ParamPanelBuilder ui)
    {
        ui.Heading("使用ブロック")
          .BlockPick("pave", "車道舗装", "minecraft:black_concrete")
          .BlockPick("mark", "区画線", "minecraft:white_concrete")
          .BlockPick("deck", "床版", "minecraft:smooth_stone")
          .BlockPick("girder", "主桁・横桁", "minecraft:gray_concrete")
          .BlockPick("pier", "橋脚・橋台", "minecraft:stone_bricks")
          .BlockPick("curb", "地覆・分離帯", "minecraft:light_gray_concrete")
          .BlockPick("walk", "歩道舗装", "minecraft:smooth_stone_slab")
          .BlockPick("rail", "高欄・照明柱", "minecraft:iron_bars")
          .BlockPick("light", "照明", "minecraft:sea_lantern");
    }

    public static void ApplyCommonBlocks(ParamPanelBuilder ui, StructureSpec spec)
    {
        spec.FloorBlock = ui.GetBlock("pave", "minecraft:black_concrete");
        spec.AccentBlock = ui.GetBlock("mark", "minecraft:white_concrete");
        spec.WallBlock = ui.GetBlock("deck", "minecraft:smooth_stone");
        spec.RoofBlock = ui.GetBlock("girder", "minecraft:gray_concrete");
        spec.BaseBlock = ui.GetBlock("pier", "minecraft:stone_bricks");
        spec.TowerBlock = ui.GetBlock("curb", "minecraft:light_gray_concrete");
        spec.VerandaBlock = ui.GetBlock("walk", "minecraft:smooth_stone_slab");
        spec.ParapetBlock = ui.GetBlock("rail", "minecraft:iron_bars");
        spec.SeatBlock = ui.GetBlock("light", "minecraft:sea_lantern");
    }
}
