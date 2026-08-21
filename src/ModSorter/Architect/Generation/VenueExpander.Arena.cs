using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // ===== 円形闘技場（コロッセウム）=====
    private static void BuildArena(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 15, 63);
        int d = Clamp(spec.Depth, 15, 63);
        int rows = Clamp(spec.VenueRows ?? 5, 1, 20);
        int run = Clamp(spec.VenueRun ?? 3, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 2, 1, 3);
        int podiumH = Clamp(spec.VenuePodium ?? 5, 0, 12);
        int wallH = Clamp(spec.VenueWall ?? 4, 0, 16);
        const int podiumT = 2;   // ポディウム壁の厚み。本物の壁は厚い。

        int topSeat = BuildBowl(cells, w, d, 2.0, ref rows, run, rise,
            podiumH, podiumT, p, out int ring);

        var perim = Perimeter(w, d, 2.0);
        if (wallH > 0) RaiseFacade(cells, perim, topSeat + 1, topSeat + wallH, 1, p.Accent);

        int facadeTop = topSeat + wallH;
        // 外周アーチ。7マスに1連＝外周545mに80連（約6.8m間隔）と同じ密度。
        CarveArcade(cells, perim, facadeTop, 7, 4, Math.Max(1, (facadeTop - 1) / 6));

        // 入場路（vomitoria）。客席の下をくぐってアリーナへ抜ける。上は中実のまま。
        if (spec.VenueGates) CarveTunnels(cells, w, d, 4, 1);

        // 本物に屋根は無い。任意で日除け（velarium）だけを客席の上に張る。
        if (spec.VenueRoof) BuildAwning(cells, w, d, 2.0, ring, podiumT, facadeTop + 1, p.Roof);
    }
}
