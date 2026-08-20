using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 船(structure_type="ship")を確定的に座標へ展開する専用ビルダー。
// StructureExpander.ExpandCore の ship 分岐から呼ばれる。
// 床/壁/屋根/開口部・入口保証は一切通さず、ここで船体・甲板・上部構造物・出入口を作る。
//
// 座標系（共通骨格）:
//   x = 船の幅（左舷 0 .. 右舷 w-1）
//   z = 船の長さ（船首・船尾方向）。bow_face="north" なら z=0 が船首、"south" なら z=d-1 が船首。
//   y = 高さ（0 が船底、上へ）。
//
// ファイル分割（partial・1ファイル9KB以下を目安）:
//   ShipExpander.cs             … 入口(Build) / 船種決定(ResolveShipClass)
//   ShipExpander.Hull.cs        … 共通骨格(BuildHull)
//   ShipExpander.Parts.cs       … 部品ヘルパーと座標/小ヘルパー
//   ShipExpander.Civil.cs       … 民間船 6種
//   ShipExpander.Naval.cs       … 護衛艦系（destroyer / frigate）
//   ShipExpander.Naval.Capital.cs … 主力艦・特殊（cruiser / battleship / carrier / submarine）
public static partial class ShipExpander
{
    // 全船種の候補（自動選択用）。
    private static readonly string[] AllClasses =
    {
        "motorboat", "trawler", "caravel", "galleon",
        "liner", "cargo", "destroyer", "battleship", "carrier", "submarine",
        "cruiser", "frigate"
    };

    public static List<GeneratedBlock> Build(
        StructureSpec spec, int w, int d, int h,
        IReadOnlyList<string> allowedBlocks, string fallback)
    {
        // 素材決定（未指定は wall_block → fallback の順で流用）。
        string wall = Pick(spec.WallBlock, allowedBlocks, fallback);
        string hull = Pick(spec.HullBlock ?? spec.WallBlock, allowedBlocks, wall);
        string deck = Pick(spec.DeckBlock ?? spec.FloorBlock ?? spec.WallBlock, allowedBlocks, wall);
        string sup = Pick(spec.SuperstructureBlock ?? spec.WallBlock, allowedBlocks, wall);
        string glass = Pick("minecraft:glass", allowedBlocks, "minecraft:glass");

        // 船首の向き。既定は north（z=0 側が船首）。
        bool bowNorth = (spec.BowFace ?? "north").Trim().ToLowerInvariant() != "south";

        // 船種の決定。指定があればそれ、なければサイズ帯から確定的乱数で選ぶ。
        string shipClass = ResolveShipClass(spec, w, d, h);

        // 座標 -> ブロックID。後勝ち（上部構造物が甲板を上書きする）。
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 船底・船体・甲板の共通骨格を作る。甲板の高さ(deckY)を返す。
        int deckY = BuildHull(cells, w, d, h, hull, deck, bowNorth, shipClass);

        // 船種ごとの上部構造物・マスト・出入口。
        switch (shipClass)
        {
            case "motorboat": BuildMotorboat(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "trawler": BuildTrawler(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "caravel": BuildCaravel(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "galleon": BuildGalleon(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "liner": BuildLiner(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "cargo": BuildCargo(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "destroyer": BuildDestroyer(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "frigate": BuildFrigate(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "cruiser": BuildCruiser(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "battleship": BuildBattleship(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "carrier": BuildCarrier(cells, w, d, h, deckY, sup, glass, deck, bowNorth); break;
            case "submarine": BuildSubmarine(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            default: BuildMotorboat(cells, w, d, h, deckY, sup, glass, bowNorth); break;
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x,
                Y = kv.Key.y,
                Z = kv.Key.z,
                Id = kv.Value
            })
            .ToList();
    }

    // ===== 船種の自動決定 =====
    // ship_class 指定があればそれを尊重。無ければサイズ帯で候補を絞り、シードで1つ選ぶ。
    private static string ResolveShipClass(StructureSpec spec, int w, int d, int h)
    {
        string? given = spec.ShipClass?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(given) && AllClasses.Contains(given))
            return given;

        // サイズ帯で候補を絞る。d(船の長さ)を主基準に、w(幅)・h(高さ)で補正。
        List<string> pool;
        if (d <= 6)
            pool = new() { "motorboat" };
        else if (d <= 12)
            pool = new() { "motorboat", "trawler", "caravel", "destroyer", "frigate" };
        else if (d <= 20)
            pool = new() { "trawler", "caravel", "galleon", "destroyer", "frigate", "cruiser" };
        else if (d <= 32)
            pool = new() { "galleon", "liner", "cargo", "destroyer", "cruiser", "battleship" };
        else
            pool = new() { "liner", "cargo", "battleship", "carrier" };

        // 潜水艦は「細長く低い」ときだけ候補に足す（幅が狭く高さが低い）。
        if (d >= 10 && w <= Math.Max(3, d / 4) && h <= Math.Max(3, d / 4))
            pool.Add("submarine");

        // 実在しない保険値を除去（cruiser_fallback は destroyer に寄せる）。
        pool = pool.Select(c => c == "cruiser_fallback" ? "destroyer" : c)
                   .Where(AllClasses.Contains).Distinct().ToList();
        if (pool.Count == 0) pool.Add("motorboat");

        // シード（0なら寸法から確定的に導く）で候補から1つ選ぶ。
        int seed = spec.ShipSeed != 0 ? spec.ShipSeed : (w * 73856093) ^ (d * 19349663) ^ (h * 83492791);
        int idx = Math.Abs(seed) % pool.Count;
        return pool[idx];
    }
}
