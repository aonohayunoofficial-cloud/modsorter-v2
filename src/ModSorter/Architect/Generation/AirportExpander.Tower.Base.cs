using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // 管制塔の庁舎。シャフトの通る中央を屋根から抜き、正面に出入口を空ける。
    private static void TowerBase(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        string shape, int shaftR, int baseW, int baseD, int baseH)
    {
        int bx = (baseW - 1) / 2;
        int bz = (baseD - 1) / 2;

        Fill(cells, -bx, bx, 0, 0, -bz, bz, p.Pave);

        for (int y = 1; y <= baseH; y++)
        {
            Fill(cells, -bx, bx, y, y, -bz, -bz, p.Shoulder);
            Fill(cells, -bx, bx, y, y, bz, bz, p.Shoulder);
            Fill(cells, -bx, -bx, y, y, -bz, bz, p.Shoulder);
            Fill(cells, bx, bx, y, y, -bz, bz, p.Shoulder);
        }

        // 窓。1マスおきに抜く。
        int wy = Math.Max(2, baseH - 2);
        for (int x = -bx + 1; x <= bx - 1; x++)
            if (((x + bx) & 1) == 0)
            {
                cells[(x, wy, -bz)] = p.Glass;
                cells[(x, wy, bz)] = p.Glass;
            }
        for (int z = -bz + 1; z <= bz - 1; z++)
            if (((z + bz) & 1) == 0)
            {
                cells[(-bx, wy, z)] = p.Glass;
                cells[(bx, wy, z)] = p.Glass;
            }

        // 屋根。シャフトの通る中央は空ける。
        for (int x = -bx; x <= bx; x++)
            for (int z = -bz; z <= bz; z++)
                if (!InPlan(shape, x, z, shaftR)) cells[(x, baseH + 1, z)] = p.Roof;

        // 正面の出入口。
        int doorH = Math.Min(3, baseH);
        for (int x = -1; x <= 1; x++)
            for (int y = 1; y <= doorH; y++)
                cells.Remove((x, y, -bz));
    }
}
