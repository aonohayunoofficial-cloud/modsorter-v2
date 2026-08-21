using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // ===== 野外音楽堂（エピダウロス＋ハリウッドボウル）=====
    private static void BuildBandshell(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int rO = Clamp(spec.VenueOrchestra ?? 6, 3, 20);
        int rows = Clamp(spec.VenueRows ?? 12, 1, 30);
        int run = Clamp(spec.VenueRun ?? 2, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 1, 1, 3);
        int shellR = Clamp(spec.VenueShellRadius ?? 9, 4, 24);
        int shellH = Clamp(spec.VenueShellHeight ?? 12, 4, 32);
        int stageH = Clamp(spec.VenueStage ?? 2, 0, 6);

        while (rows > 1 && (rO + 1 + rows * run) * 2 + 1 > 63) rows--;

        // 帯の表。エピダウロスは55段を下34段・上21段に分ける水平通路を持つ。
        var bands = new List<(int Width, int Rise, bool Walk)>();
        int diazomaAfter = rows >= 6 ? Math.Max(2, rows * 34 / 55) : -1;
        for (int r = 0; r < rows; r++)
        {
            bands.Add((run, r == 0 ? 0 : rise, false));
            if (r + 1 == diazomaAfter) bands.Add((2, 0, true));
        }

        var table = new List<(int From, int To, int Y, bool Walk)>();
        int rad = rO + 1, yy = 1;
        foreach (var (bw, br, walk) in bands)
        {
            yy += br;
            table.Add((rad, rad + bw - 1, yy, walk));
            rad += bw;
        }
        int caveaEdge = rad - 1;

        int cx = caveaEdge;
        int cz0 = shellR + 2;

        const double half = 105.0 * Math.PI / 180.0;   // カヴェアは210°
        double axis = Math.PI / 2.0;                   // 客席は +z 側
        int stairs = 9;

        for (int x = cx - caveaEdge; x <= cx + caveaEdge; x++)
            for (int z = cz0 - caveaEdge; z <= cz0 + caveaEdge; z++)
            {
                double dx = x - cx, dz = z - cz0;
                double dist = Math.Sqrt(dx * dx + dz * dz);

                if (dist <= rO + 0.5)
                {
                    cells[(x, 0, z)] = p.Field;   // 円形のオルケストラ
                    continue;
                }
                if (dist > caveaEdge + 0.5) continue;

                double ang = Math.Atan2(dz, dx);
                if (Math.Abs(Norm(ang - axis)) > half) continue;

                int band = -1;
                for (int i = 0; i < table.Count; i++)
                    if (dist >= table[i].From - 0.5 && dist < table[i].To + 0.5) { band = i; break; }
                if (band < 0) continue;

                int top = table[band].Y;
                for (int y = 0; y < top; y++) cells[(x, y, z)] = p.Structure;

                string cap = table[band].Walk ? p.Field : p.Seat;
                // 放射状の階段（クリマケス）。
                for (int s = 0; s < stairs; s++)
                {
                    double ray = axis - half + (2 * half) * (s + 0.5) / stairs;
                    if (Math.Abs(Norm(ang - ray)) * dist < 0.7) { cap = p.Accent; break; }
                }
                cells[(x, top, z)] = cap;
            }

        // 舞台。オルケストラの奥側に半円の台を置く。
        int stageR = Math.Max(3, shellR - 2);
        for (int x = cx - stageR; x <= cx + stageR; x++)
            for (int z = cz0 - stageR; z <= cz0; z++)
            {
                double dx = x - cx, dz = z - cz0;
                if (dx * dx + dz * dz > (stageR + 0.5) * (stageR + 0.5)) continue;
                for (int y = 0; y < stageH; y++) cells[(x, y, z)] = p.Structure;
                cells[(x, stageH, z)] = p.Accent;
            }

        // シェル。半円筒の壁の上に四分の一球を載せた貝殻。開口は客席側だけ。
        int springY = stageH + Math.Max(0, shellH - shellR);
        for (int x = cx - shellR; x <= cx + shellR; x++)
            for (int z = cz0 - shellR; z <= cz0; z++)
            {
                double dx = x - cx, dz = z - cz0;
                double flat = Math.Sqrt(dx * dx + dz * dz);

                if (flat >= shellR - 0.5 && flat <= shellR + 0.5)
                    for (int y = 0; y < springY; y++)
                        cells[(x, y, z)] = Band(y) ? p.Accent : p.Roof;

                for (int y = springY; y <= springY + shellR; y++)
                {
                    double dy = y - springY;
                    double r3 = Math.Sqrt(dx * dx + dz * dz + dy * dy);
                    if (r3 >= shellR - 0.5 && r3 <= shellR + 0.5)
                        cells[(x, y, z)] = Band((int)Math.Round(dy)) ? p.Accent : p.Roof;
                }
            }
    }

    // 同心円バンド（ハリウッドボウルの縞）。3リングごとに装飾材へ替える。
    private static bool Band(int k) => (k / 3) % 2 == 1;
}
