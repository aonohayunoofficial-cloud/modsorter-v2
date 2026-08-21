using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // ===== テント広場 =====
    private static void BuildTents(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int count = Clamp(spec.VenueTentCount ?? 4, 1, 12);
        int tw = Clamp(spec.VenueTentWidth ?? 7, 3, 21) | 1;   // 奇数化して棟を中央に通す
        int td = Clamp(spec.VenueTentDepth ?? 9, 3, 31);
        int eaveH = Clamp(spec.VenueTentEave ?? 3, 2, 8);
        int gap = Clamp(spec.VenueTentGap ?? 3, 1, 12);
        int aisle = Clamp(spec.VenueTentAisle ?? 6, 2, 16);
        int rowsOfTents = (spec.VenueTentRows ?? 1) >= 2 ? 2 : 1;
        bool closed = spec.VenueTentClosed;
        bool pave = spec.VenueTentPave;

        int perRow = (count + rowsOfTents - 1) / rowsOfTents;
        int half = (tw - 1) / 2;
        int placed = 0;

        for (int r = 0; r < rowsOfTents; r++)
        {
            int z0 = r * (td + aisle);
            for (int i = 0; i < perRow && placed < count; i++, placed++)
            {
                int x0 = i * (tw + gap);

                // 地面は既定で敷かない。敷く指定のときだけテントの下に1層。
                if (pave)
                    for (int x = 0; x < tw; x++)
                        for (int z = 0; z < td; z++)
                            cells[(x0 + x, 0, z0 + z)] = p.Field;

                // 柱（開放）または壁（閉鎖）。
                for (int x = 0; x < tw; x++)
                    for (int z = 0; z < td; z++)
                    {
                        bool corner = (x == 0 || x == tw - 1) && (z == 0 || z == td - 1);
                        bool edge = x == 0 || x == tw - 1 || z == 0 || z == td - 1;
                        if (!(closed ? edge : corner)) continue;
                        for (int y = 1; y <= eaveH; y++)
                            cells[(x0 + x, y, z0 + z)] = corner ? p.Structure : p.Accent;
                    }

                // 切妻の天幕。棟は z 軸に平行。
                for (int k = 0; k <= half; k++)
                {
                    int y = eaveH + 1 + k;
                    for (int z = 0; z < td; z++)
                    {
                        cells[(x0 + k, y, z0 + z)] = p.Roof;
                        cells[(x0 + tw - 1 - k, y, z0 + z)] = p.Roof;
                    }
                }
                // 妻面（前後の三角）は天幕の一部として塞ぐ。
                foreach (int gz in new[] { 0, td - 1 })
                    for (int x = 0; x < tw; x++)
                    {
                        int k = Math.Min(x, tw - 1 - x);
                        for (int y = eaveH + 1; y < eaveH + 1 + k; y++)
                            cells[(x0 + x, y, z0 + gz)] = p.Roof;
                    }
            }
        }
    }
}
