using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // ===== ボウル（段状客席）の共通生成 =====
    // pow=2 で楕円、pow=4 で角の丸い矩形。内側から podium 帯 → 客席帯 → 外周帯。
    // 各帯は y=0 から天面まで中実に埋めるので段が浮くことは起きない。
    private static int BuildBowl(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, double pow,
        ref int rows, int run, int rise, int podiumH, int podiumT, Palette p, out int ring)
    {
        double a = w / 2.0, b = d / 2.0;
        double cx = (w - 1) / 2.0, cz = (d - 1) / 2.0;

        ring = podiumT + rows * run;
        while (rows > 1 && Math.Min(a, b) - ring < 4.0)
        {
            rows--;
            ring = podiumT + rows * run;
        }

        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                double dx = x - cx, dz = z - cz;
                if (!Inside(dx, dz, a, b, pow)) continue;

                int m = 0;
                while (m < ring && Inside(dx, dz, a - (m + 1), b - (m + 1), pow)) m++;

                if (m >= ring)
                {
                    cells[(x, 0, z)] = p.Field;
                    continue;
                }

                int top;
                string cap;
                if (m >= ring - podiumT)
                {
                    top = podiumH;                              // ポディウム壁
                    cap = p.Accent;
                }
                else
                {
                    int j = (ring - podiumT - 1) - m;           // 内側の客席帯から数えた番号
                    top = podiumH + (j / run) * rise;
                    cap = p.Seat;
                }

                for (int y = 0; y < top; y++) cells[(x, y, z)] = p.Structure;
                cells[(x, top, z)] = cap;
            }

        return podiumH + (rows - 1) * rise;
    }

    // 外形の縁1マス分を角度順に並べたもの。アーチ列の割り付けに使う。
    private static List<(int X, int Z)> Perimeter(int w, int d, double pow)
    {
        double a = w / 2.0, b = d / 2.0, cx = (w - 1) / 2.0, cz = (d - 1) / 2.0;
        var list = new List<(int X, int Z, double Ang)>();
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                double dx = x - cx, dz = z - cz;
                if (!Inside(dx, dz, a, b, pow)) continue;
                if (Inside(dx, dz, a - 1, b - 1, pow)) continue;
                list.Add((x, z, Math.Atan2(dz, dx)));
            }
        return list.OrderBy(t => t.Ang).Select(t => (t.X, t.Z)).ToList();
    }
}
