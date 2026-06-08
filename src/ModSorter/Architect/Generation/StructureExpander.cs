using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// StructureSpec を確定的に座標へ展開する。
// 壁の外周リングは必ずここで生成するため、塊化や壁抜けは原理的に起きない。
public static class StructureExpander
{
    public static List<GeneratedBlock> Expand(StructureSpec spec, IReadOnlyList<string> allowedBlocks)
    {
        // 寸法の健全化（最低 2x2x2、極端な値は抑える）
        int w = Clamp(spec.Width, 2, 64);
        int d = Clamp(spec.Depth, 2, 64);
        int h = Clamp(spec.Height, 2, 64);

        // 素材決定（許可リスト外なら先頭ブロックにフォールバック）
        string fallback = allowedBlocks.Count > 0 ? allowedBlocks[0] : "minecraft:oak_planks";
        string wall = Pick(spec.WallBlock, allowedBlocks, fallback);
        string floor = Pick(spec.FloorBlock ?? spec.WallBlock, allowedBlocks, wall);
        string roof = Pick(spec.RoofBlock ?? spec.WallBlock, allowedBlocks, wall);

        // 座標 -> ブロックID。後勝ち（開口部で上書きするため）。
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 床（y=0 全面）
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, 0, z)] = floor;

        // 屋根（y=h-1 全面）
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, h - 1, z)] = roof;

        // 壁（中間層 y=1..h-2 の外周リングのみ）
        for (int y = 1; y <= h - 2; y++)
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    if (x == 0 || x == w - 1 || z == 0 || z == d - 1)
                        cells[(x, y, z)] = wall;

        // 開口部の適用
        foreach (var op in spec.Openings ?? new List<Opening>())
            ApplyOpening(cells, op, w, d, h, allowedBlocks);

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

    private static void ApplyOpening(
        Dictionary<(int x, int y, int z), string> cells,
        Opening op, int w, int d, int h, IReadOnlyList<string> allowedBlocks)
    {
        int y = Clamp(op.Level, 1, Math.Max(1, h - 2)); // 中間層に収める
        string face = (op.Face ?? "").Trim().ToLowerInvariant();

        // 面ごとに、面に沿った座標(offset)から壁上の1セルを特定する
        (int x, int z)? target = face switch
        {
            "north" => (Clamp(op.Offset, 0, w - 1), 0),       // z=0 の面
            "south" => (Clamp(op.Offset, 0, w - 1), d - 1),   // z=d-1 の面
            "west" => (0, Clamp(op.Offset, 0, d - 1)),       // x=0 の面
            "east" => (w - 1, Clamp(op.Offset, 0, d - 1)),   // x=w-1 の面
            _ => null
        };
        if (target == null) return;

        var key = (target.Value.x, y, target.Value.z);
        // 壁セルでなければ無視（角や非外周を壊さない）
        if (!cells.ContainsKey(key)) return;

        bool isDoor = (op.Kind ?? "").Trim().ToLowerInvariant() == "door";
        if (isDoor)
        {
            cells.Remove(key); // ドア=開口（ブロック除去）
        }
        else
        {
            string glass = Pick(op.Block ?? "minecraft:glass", allowedBlocks, "minecraft:glass");
            cells[key] = glass; // 窓=ガラス置換
        }
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? candidate, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = allowed.FirstOrDefault(
                a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }
}
