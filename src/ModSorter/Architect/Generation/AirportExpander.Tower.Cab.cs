using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // 管制室とその上まわり。floorY が管制室の床。
    private static void TowerCab(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        string shape, int cabR, int cabH, int floorY, int tilt, int walk, int mast, bool light)
    {
        // ===== 管制室 =====
        PlanFill(cells, shape, cabR, floorY, p.Pave, false);

        // キャットウォークと手すり。
        if (walk > 0)
        {
            int rw = cabR + walk;
            for (int dx = -rw; dx <= rw; dx++)
                for (int dz = -rw; dz <= rw; dz++)
                    if (InPlan(shape, dx, dz, rw) && !InPlan(shape, dx, dz, cabR))
                        cells[(dx, floorY, dz)] = p.Pave;
            PlanFill(cells, shape, rw, floorY + 1, p.Rail, true);
        }

        // 窓。tilt 段ごとに 1 マス外へ出して 15 度の外傾を近似する。
        // 最下段はコンソールが並ぶ腰壁なので窓にしない。
        int rTop = cabR;
        for (int j = 0; j < cabH; j++)
        {
            int y = floorY + 1 + j;
            rTop = cabR + (tilt > 0 ? j / tilt : 0);
            PlanFill(cells, shape, rTop, y, j == 0 ? p.Mark : p.Glass, true);

            // 方立。平面の角に立てる。
            if (j > 0)
                for (int dx = -rTop; dx <= rTop; dx++)
                    for (int dz = -rTop; dz <= rTop; dz++)
                        if (IsCorner(shape, dx, dz, rTop)) cells[(dx, y, dz)] = p.Mark;
        }

        // ===== 屋根・アンテナ柱・航空障害灯 =====
        int roofY = floorY + 1 + cabH;
        PlanFill(cells, shape, rTop + 1, roofY, p.Roof, false);

        for (int k = 1; k <= mast; k++) cells[(0, roofY + k, 0)] = p.Mark;

        if (light)
        {
            cells[(0, roofY + mast + 1, 0)] = p.Light;
            cells[(rTop, roofY + 1, 0)] = p.Light;
            cells[(-rTop, roofY + 1, 0)] = p.Light;
            cells[(0, roofY + 1, rTop)] = p.Light;
            cells[(0, roofY + 1, -rTop)] = p.Light;
        }
    }
}
