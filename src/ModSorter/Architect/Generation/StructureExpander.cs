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

        // 土台段（base course）: y=0 の外周一周を土台材に差し替える。
        // 未指定なら floor と同じ＝従来の見た目（差し替えても影響なし）。座標系は変えない。
        string baseBlock = Pick(spec.BaseBlock, allowedBlocks, floor);
        if (spec.HasBase)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    if (x == 0 || x == w - 1 || z == 0 || z == d - 1)
                        cells[(x, 0, z)] = baseBlock;
        }

        // 屋根（roof_type で分岐）
        string roofType = (spec.RoofType ?? "flat").Trim().ToLowerInvariant();
        if (roofType == "gable")
            BuildGableRoof(cells, spec, w, d, h, roof, wall);
        else
            BuildFlatRoof(cells, w, d, h, roof);

        // アクセント材（柱型リズム用）。未指定なら wall と同じ＝従来の見た目。
        string accent = Pick(spec.AccentBlock, allowedBlocks, wall);
        // 柱の間隔（spec 指定、未指定/不正なら 0＝柱なし）。
        int pilasterStep = spec.PilasterStep.HasValue && spec.PilasterStep.Value >= 2
            ? spec.PilasterStep.Value : 0;

        // 壁（中間層 y=1..h-2 の外周リングのみ）
        for (int y = 1; y <= h - 2; y++)
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    if (x == 0 || x == w - 1 || z == 0 || z == d - 1)
                    {
                        bool isCorner = (x == 0 || x == w - 1) && (z == 0 || z == d - 1);
                        bool isPilaster = pilasterStep > 0 &&
                            ((x == 0 || x == w - 1) ? (z % pilasterStep == 0)
                                                    : (x % pilasterStep == 0));
                        cells[(x, y, z)] = (isCorner || isPilaster) ? accent : wall;
                    }


        // 中間床（複数階）。指定された各 y に内部も含む全面の床を敷く。
        foreach (int fy in (spec.FloorLevels ?? new List<int>()).Distinct())
        {
            // 1階の床(0)・屋根の領域(h-1以上)とぶつかる指定は無視
            if (fy <= 0 || fy >= h - 1) continue;
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    cells[(x, fy, z)] = floor;
        }

        // 開口部の適用（中間床より後。床に窓・ドアが指定されても壁セルのみ作用するので安全）
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

    // 平屋根: 最上層 y=h-1 を全面で塞ぐ
    private static void BuildFlatRoof(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int h, string roof)
    {
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, h - 1, z)] = roof;
    }

    // 切妻屋根: 棟の向き(ridge_axis)に沿って段々に三角を作る。
    // 屋根は本体の上(y=h から上)に積む。傾斜方向の端から中央へ向け段を上げる。
    private static void BuildGableRoof(
    Dictionary<(int x, int y, int z), string> cells,
    StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        // 棟がx軸に平行 → z方向に傾斜（zの端から中央へ高くなる）
        // 棟がz軸に平行 → x方向に傾斜（xの端から中央へ高くなる）
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w; // 傾斜する方向の長さ
        // 端から中央までの段数。中央で最も高い。
        int half = (slopeLen + 1) / 2;

        for (int i = 0; i < slopeLen; i++)
        {
            // 端(0 と slopeLen-1)が低く、中央が高い。step = 端からの距離。
            int step = System.Math.Min(i, slopeLen - 1 - i);
            int yLevel = (h - 1) + step; // 壁の最上層と同じ高さ(y=h-1)から積む

            if (ridgeAlongX)
            {
                // 屋根: z=i の列。棟方向(x)は全幅に渡って同じ高さ。
                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = roof;

                // 妻壁: 妻側の面(x=0 と x=w-1)で、壁の上端(y=h-1)から
                //       この列の屋根の手前(yLevel-1)までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // 屋根: x=i の列。棟方向(z)は全奥行きに渡って同じ高さ。
                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = roof;

                // 妻壁: 妻側の面(z=0 と z=d-1)で、壁の上端からこの列の屋根の手前までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }

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
