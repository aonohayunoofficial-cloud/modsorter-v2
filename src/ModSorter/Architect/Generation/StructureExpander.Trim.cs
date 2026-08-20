using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 外周に付く仕上げ（軒・縁側・塔）と、最後の座標正規化。
// 軒と縁側は一時的に負座標を作るので、Normalize より前に必ず呼ぶ。
public static partial class StructureExpander
{
    private static void BuildExteriorTrim(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot,
        StructureSpec spec, int w, int d, int h,
        IReadOnlyList<string> allowedBlocks, string wall, string roof,
        string roofType, bool rectangular)
    {
        // 軒の出（eaves）。flat/gable/shed のときだけ、屋根の軒先を外側へ水平に伸ばす。
        // ここでは負座標(x=-1 等)も一時的に許し、直後の一括シフトで 0 以上へ寄せる。
        int eave = Math.Clamp(spec.EaveOverhang ?? 0, 0, 8);
        if (eave > 0 && rectangular &&
            (roofType == "flat" || roofType == "gable" || roofType == "shed"))
        {
            BuildEaves(cells, foot, spec, w, d, h, roof, roofType, eave);
        }

        // 縁側／基壇の縁（veranda）。平面の外側へ y=0 の床を敷き足す。
        // 深い軒の下に回り縁ができ、寺社の「軒下に縁がある」輪郭になる。
        // 軒と同じく負座標を一時的に許し、直後の一括シフトで 0 以上へ寄せる。
        int veranda = Math.Clamp(spec.VerandaWidth ?? 0, 0, 4);
        if (veranda > 0)
        {
            string verandaBlock = Pick(
                spec.VerandaBlock ?? spec.BaseBlock ?? spec.FloorBlock, allowedBlocks, wall);
            BuildVeranda(cells, foot, w, d, veranda, verandaBlock);
        }

        // 塔（鐘塔・尖塔・ミナレット）。平面内に正方形の塔を立て、屋根より上へ突き出す。
        // 屋根形状を問わないので、切妻の教会・ドームのモスク・陸屋根のどれにも載る。
        // 必ず軒の後に呼ぶ。軒は「その列の屋根の実際の最高y」を走査して高さを決めるため、
        // 先に塔を立てると塔の頂部の高さで軒が張り出して破綻する。
        if ((spec.TowerWidth ?? 0) >= 3 && (spec.TowerHeight ?? 0) >= 1)
        {
            string towerBlock = Pick(spec.TowerBlock ?? spec.WallBlock, allowedBlocks, wall);
            string towerRoofBlock = Pick(spec.TowerRoofBlock ?? spec.RoofBlock, allowedBlocks, roof);
            BuildTower(cells, foot, spec, w, d, h, towerBlock, towerRoofBlock);
        }
    }

    // 全ブロックの最小座標を求め、負のぶんだけ全体をシフトして 0 起点に正規化する。
    // 軒で x=-1/z=-1 が出ても、ここで +eave 相当のシフトがかかり負座標は消える。
    // StructureNbtWriter は負座標を書けないため、この正規化は必須。
    private static List<GeneratedBlock> Normalize(Dictionary<(int x, int y, int z), string> cells)
    {
        int minX = 0, minZ = 0;
        foreach (var k in cells.Keys)
        {
            if (k.x < minX) minX = k.x;
            if (k.z < minZ) minZ = k.z;
        }
        int shiftX = -minX, shiftZ = -minZ;

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x + shiftX,
                Y = kv.Key.y,
                Z = kv.Key.z + shiftZ,
                Id = kv.Value
            })
            .ToList();
    }
}
