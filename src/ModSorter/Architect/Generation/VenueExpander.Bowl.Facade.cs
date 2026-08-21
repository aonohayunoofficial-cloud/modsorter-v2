using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // 外周の立ち上がり。step が2以上なら柱列（間引き）になる。
    private static void RaiseFacade(
        Dictionary<(int x, int y, int z), string> cells,
        IReadOnlyList<(int X, int Z)> perim, int fromY, int toY, int step, string block)
    {
        for (int i = 0; i < perim.Count; i++)
        {
            if (step > 1 && i % step != 0) continue;
            for (int y = fromY; y <= toY; y++) cells[(perim[i].X, y, perim[i].Z)] = block;
        }
    }

    // 外周にアーチ列を抜く。bay マスに1連、幅3、最上段だけ1マス狭めて迫り上がりに見せる。
    private static void CarveArcade(
        Dictionary<(int x, int y, int z), string> cells,
        IReadOnlyList<(int X, int Z)> perim, int topY, int bay, int archH, int levels)
    {
        int total = perim.Count;
        if (total < bay * 2) return;

        for (int level = 0; level < levels; level++)
        {
            int baseY = 1 + level * (archH + 2);
            if (baseY + archH > topY) break;

            for (int i = 0; i < total; i++)
            {
                int off = Math.Abs(i % bay - bay / 2);
                if (off > 1) continue;
                for (int y = baseY; y < baseY + archH; y++)
                {
                    if (off == 1 && y == baseY + archH - 1) continue;
                    cells.Remove((perim[i].X, y, perim[i].Z));
                }
            }
        }
    }

    // 入場路。長軸・短軸に沿って客席の下を貫く。上の帯は中実のまま残るので天井が付く。
    private static void CarveTunnels(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int height, int halfWidth)
    {
        int cx = (w - 1) / 2, cz = (d - 1) / 2;
        for (int y = 1; y <= height; y++)
        {
            for (int x = 0; x < w; x++)
                for (int t = -halfWidth; t <= halfWidth; t++) cells.Remove((x, y, cz + t));
            for (int z = 0; z < d; z++)
                for (int t = -halfWidth; t <= halfWidth; t++) cells.Remove((cx + t, y, z));
        }
    }

    // 客席の上だけを覆う日除け／屋根。中央（アリーナ・ピッチ）の上は開けたまま。
    private static void BuildAwning(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, double pow,
        int ring, int podiumT, int y, string block)
    {
        double a = w / 2.0, b = d / 2.0, cx = (w - 1) / 2.0, cz = (d - 1) / 2.0;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                double dx = x - cx, dz = z - cz;
                if (!Inside(dx, dz, a, b, pow)) continue;
                int m = 0;
                while (m < ring && Inside(dx, dz, a - (m + 1), b - (m + 1), pow)) m++;
                if (m < ring - podiumT) cells[(x, y, z)] = block;
            }
    }
}
