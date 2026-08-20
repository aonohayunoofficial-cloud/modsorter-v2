using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 塔の配置決定（tower_align → 左手前角）と頂部。StructureExpander の partial。
// 本体は StructureExpander.Parts.Tower.cs。
public static partial class StructureExpander
{
    // tower_align から塔の左手前角(x0,z0)の候補を作る。正面は facade_face で決まる。
    // 範囲外へ出る値もそのまま返す。呼び出し側で 0..xMax / 0..zMax にクランプする。
    private static List<(int x0, int z0)> TowerSpots(string align, string face, int w, int d, int s)
    {
        int cx = (w - s) / 2, cz = (d - s) / 2;
        int xMax = Math.Max(0, w - s), zMax = Math.Max(0, d - s);

        var spots = new List<(int x0, int z0)>();
        switch (align)
        {
            case "center":
                spots.Add((cx, cz));
                break;

            case "rear":
                if (face == "south") spots.Add((cx, 0));
                else if (face == "north") spots.Add((cx, zMax));
                else if (face == "east") spots.Add((0, cz));
                else spots.Add((xMax, cz));
                break;

            case "front_corners":
                if (face == "south" || face == "north")
                {
                    int fz = face == "south" ? zMax : 0;
                    spots.Add((0, fz));
                    spots.Add((xMax, fz));
                }
                else
                {
                    int fx = face == "east" ? xMax : 0;
                    spots.Add((fx, 0));
                    spots.Add((fx, zMax));
                }
                break;

            case "four_corners":
                spots.Add((0, 0));
                spots.Add((xMax, 0));
                spots.Add((0, zMax));
                spots.Add((xMax, zMax));
                break;

            default: // "front"
                if (face == "south") spots.Add((cx, zMax));
                else if (face == "north") spots.Add((cx, 0));
                else if (face == "east") spots.Add((xMax, cz));
                else spots.Add((0, cz));
                break;
        }
        return spots;
    }

    // 塔の頂部。spire=尖塔（2段ごとに1マス絞る）、dome=丸屋根、flat=陸屋根。
    // どの形でも最初に s×s を全面へ敷き、塔の吹き抜けを確実に塞ぐ。
    private static void BuildTowerCap(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int z0, int s, int topY, string cap, string roof)
    {
        int x1 = x0 + s - 1, z1 = z0 + s - 1;

        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                cells[(x, topY + 1, z)] = roof;

        if (cap == "flat") return;

        if (cap == "dome")
        {
            // 半球。段ごとの水平半径を球の式で求め、円板を敷く（中実なので穴が空かない）。
            double r = (s - 1) / 2.0;
            double ccx = x0 + r, ccz = z0 + r;
            int hr = Math.Max(2, (int)Math.Round(r));
            for (int k = 1; k <= hr; k++)
            {
                double t = (double)k / hr;
                double rr = r * Math.Sqrt(Math.Max(0.0, 1.0 - t * t));
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        double dx = x - ccx, dz = z - ccz;
                        if (dx * dx + dz * dz <= (rr + 0.5) * (rr + 0.5))
                            cells[(x, topY + 1 + k, z)] = roof;
                    }
            }
            return;
        }

        // 尖塔: 2段ごとに全周を1マスずつ内側へ絞る。1段ごとに絞る四角錐より鋭く伸びる。
        for (int k = 1; ; k++)
        {
            int inset = k / 2;
            int ax0 = x0 + inset, ax1 = x1 - inset;
            int az0 = z0 + inset, az1 = z1 - inset;
            if (ax0 > ax1 || az0 > az1) break;
            for (int x = ax0; x <= ax1; x++)
                for (int z = az0; z <= az1; z++)
                    cells[(x, topY + 1 + k, z)] = roof;
        }
    }
}
