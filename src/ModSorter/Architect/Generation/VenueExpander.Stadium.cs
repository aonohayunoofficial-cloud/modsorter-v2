using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // ===== 競技場 =====
    private static void BuildStadium(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        if ((spec.VenueSides ?? "bowl").Trim().ToLowerInvariant() == "single")
        {
            BuildSingleStand(cells, spec, p);
            return;
        }

        int w = Clamp(spec.Width, 21, 63);
        int d = Clamp(spec.Depth, 21, 63);
        int rows = Clamp(spec.VenueRows ?? 7, 1, 20);
        int run = Clamp(spec.VenueRun ?? 2, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 1, 1, 3);
        int podiumH = Clamp(spec.VenuePodium ?? 2, 0, 8);
        int wallH = Clamp(spec.VenueWall ?? 3, 0, 16);
        const int podiumT = 1;

        // pow=4 の超楕円＝角の丸い矩形。ピッチは矩形、外周は連続したボウルになる。
        int topSeat = BuildBowl(cells, w, d, 4.0, ref rows, run, rise,
            podiumH, podiumT, p, out int ring);

        var perim = Perimeter(w, d, 4.0);
        if (wallH > 0) RaiseFacade(cells, perim, topSeat + 1, topSeat + wallH, 1, p.Accent);

        int facadeTop = topSeat + wallH;
        // 外装のコンコース開口。5マスに1つ、高さ3。
        CarveArcade(cells, perim, facadeTop, 5, 3, Math.Max(1, (facadeTop - 1) / 5));
        if (spec.VenueGates) CarveTunnels(cells, w, d, 4, 1);

        // 屋根はスタンドの上だけ。ピッチの上は開ける。外周に柱を立てて支える。
        if (spec.VenueRoof)
        {
            int lift = Clamp(spec.VenueRoofHeight ?? 4, 1, 12);
            int roofY = facadeTop + lift;
            RaiseFacade(cells, perim, facadeTop + 1, roofY - 1, 4, p.Structure);
            BuildAwning(cells, w, d, 4.0, ring, podiumT, roofY, p.Roof);
        }
    }
}
