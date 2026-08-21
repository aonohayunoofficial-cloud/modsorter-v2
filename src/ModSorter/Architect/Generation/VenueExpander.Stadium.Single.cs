using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // 片面スタンド単体。背面のコンコース棟・妻壁・持ち出し屋根まで作って単独で完結させる。
    // 参考: 競馬場／陸上競技場のメインスタンド。
    private static void BuildSingleStand(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 7, 63);
        int rows = Clamp(spec.VenueRows ?? 8, 1, 20);
        int run = Clamp(spec.VenueRun ?? 2, 1, 4);
        int rise = Clamp(spec.VenueRise ?? 1, 1, 3);
        int podiumH = Clamp(spec.VenuePodium ?? 2, 0, 8);
        int wallH = Clamp(spec.VenueWall ?? 4, 1, 16);
        const int concourse = 3;

        int seatD = rows * run;
        int d = seatD + concourse;
        int topSeat = podiumH + (rows - 1) * rise;
        int roofY = topSeat + wallH + 1;

        int LocalTop(int z)
        {
            if (z < concourse) return topSeat;
            int k = d - 1 - z;
            return podiumH + (k / run) * rise;
        }

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < d; z++)
            {
                int top = LocalTop(z);
                for (int y = 0; y < top; y++) cells[(x, y, z)] = p.Structure;
                cells[(x, top, z)] = z < concourse ? p.Field : p.Seat;
            }
            // フィールド側の立ち上がり（ポディウム）。
            for (int y = 0; y < podiumH; y++) cells[(x, y, d - 1)] = p.Accent;
            // 背面の壁。
            for (int y = topSeat + 1; y < roofY; y++) cells[(x, y, 0)] = p.Accent;
        }

        // 妻壁。段の輪郭に沿わせて屋根まで立ち上げる。
        foreach (int gx in new[] { 0, w - 1 })
            for (int z = 0; z < d; z++)
                for (int y = LocalTop(z) + 1; y < roofY; y++)
                    cells[(gx, y, z)] = p.Accent;

        // 屋根。背面の壁と妻壁で支えるので浮かない。
        if (spec.VenueRoof)
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    cells[(x, roofY, z)] = p.Roof;
    }
}
