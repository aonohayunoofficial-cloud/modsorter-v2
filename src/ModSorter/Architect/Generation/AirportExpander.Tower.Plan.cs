using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // 管制室・シャフトの平面。"square" | "octagon"（既定） | "round"。
    private static string ShapeOf(string? s)
    {
        string v = (s ?? "octagon").Trim().ToLowerInvariant();
        return (v == "square" || v == "round") ? v : "octagon";
    }

    // 中心 (0,0) から見て (dx,dz) が半径 r の平面の内側か。
    // 八角形は正八角形の一辺 ＝ 対辺幅/(1+√2) に合わせて ax+az の上限を r×1.45 とする。
    private static bool InPlan(string shape, int dx, int dz, int r)
    {
        int ax = Math.Abs(dx), az = Math.Abs(dz);
        if (shape == "round") return dx * dx + dz * dz <= (r + 0.35) * (r + 0.35);
        if (ax > r || az > r) return false;
        if (shape == "octagon") return ax + az <= (int)Math.Round(r * 1.45);
        return true;
    }

    // 平面を 1 段ぶん塗る。ringOnly なら外周 1 マスだけ（4近傍が全部内側なら塗らない）。
    private static void PlanFill(
        Dictionary<(int x, int y, int z), string> cells,
        string shape, int r, int y, string block, bool ringOnly)
    {
        if (r < 0) return;
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                if (!InPlan(shape, dx, dz, r)) continue;
                if (ringOnly
                    && InPlan(shape, dx + 1, dz, r) && InPlan(shape, dx - 1, dz, r)
                    && InPlan(shape, dx, dz + 1, r) && InPlan(shape, dx, dz - 1, r)) continue;
                cells[(dx, y, dz)] = block;
            }
    }

    // 平面の角。方立を立てる位置。
    private static bool IsCorner(string shape, int dx, int dz, int r)
        => InPlan(shape, dx, dz, r)
           && (!InPlan(shape, dx + 1, dz, r) || !InPlan(shape, dx - 1, dz, r))
           && (!InPlan(shape, dx, dz + 1, r) || !InPlan(shape, dx, dz - 1, r));
}
