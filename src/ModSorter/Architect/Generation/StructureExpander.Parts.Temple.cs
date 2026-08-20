using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 神殿のファサード（temple 様式）。柱の部品は StructureExpander.Parts.Column.cs。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // ファサード型（temple）: 指定された面(facadeFace)に柱廊、その奥に壁の部屋。
    // 柱は建物範囲内に収める（張り出さない）。柱廊と部屋の間に gap マスの空きを設け、
    // 柱廊側の壁の中央に縦2マスの入口を空ける。
    private static void BuildTemple(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, string wall, string col, string facadeFace)
    {
        int rByHeight = h < 10 ? 1 : (h < 18 ? 2 : 3);
        int r = Math.Max(1, rByHeight);

        int yTop = h - 2;
        if (yTop < 1) yTop = 1;
        int step = Math.Max(4, r * 2 + 3);

        int gap = r * 2 + 1;

        string face = (facadeFace ?? "south").Trim().ToLowerInvariant();
        bool frontAlongX = (face == "south" || face == "north");

        if (frontAlongX)
        {
            r = Math.Min(r, Math.Max(1, w / 5));
            int lo = r, hi = w - 1 - r;
            if (hi < lo) hi = lo;

            int rzLo, rzHi, doorZ;
            if (face == "south")
            {
                rzLo = 0;
                rzHi = Math.Max(rzLo + 1, d - 1 - gap - 1);
                doorZ = rzHi; // 柱廊に面した壁
            }
            else
            {
                rzHi = d - 1;
                rzLo = Math.Min(rzHi - 1, gap + 1);
                doorZ = rzLo; // 柱廊に面した壁
            }

            for (int y = 1; y <= h - 2; y++)
                for (int x = 0; x < w; x++)
                    for (int z = rzLo; z <= rzHi; z++)
                        if (x == 0 || x == w - 1 || z == rzLo || z == rzHi)
                            cells[(x, y, z)] = wall;

            // 入口: 柱廊側の壁(z=doorZ)の中央に縦2マスの開口。
            int doorX = w / 2;
            cells.Remove((doorX, 1, doorZ));
            if (h - 2 >= 2) cells.Remove((doorX, 2, doorZ));

            int frontZ = (face == "south") ? d - 1 - r : r;
            if (frontZ < 0) frontZ = 0;
            if (frontZ > d - 1) frontZ = d - 1;

            foreach (int cxp in AxisPositions(lo, hi, step))
                BuildColumn(cells, cxp, frontZ, r, 1, yTop, col, w, d);
        }
        else
        {
            r = Math.Min(r, Math.Max(1, d / 5));
            int lo = r, hi = d - 1 - r;
            if (hi < lo) hi = lo;

            int rxLo, rxHi, doorX2;
            if (face == "east")
            {
                rxLo = 0;
                rxHi = Math.Max(rxLo + 1, w - 1 - gap - 1);
                doorX2 = rxHi;
            }
            else
            {
                rxHi = w - 1;
                rxLo = Math.Min(rxHi - 1, gap + 1);
                doorX2 = rxLo;
            }

            for (int y = 1; y <= h - 2; y++)
                for (int x = rxLo; x <= rxHi; x++)
                    for (int z = 0; z < d; z++)
                        if (z == 0 || z == d - 1 || x == rxLo || x == rxHi)
                            cells[(x, y, z)] = wall;

            // 入口: 柱廊側の壁(x=doorX2)の中央に縦2マスの開口。
            int doorZ2 = d / 2;
            cells.Remove((doorX2, 1, doorZ2));
            if (h - 2 >= 2) cells.Remove((doorX2, 2, doorZ2));

            int frontX = (face == "east") ? w - 1 - r : r;
            if (frontX < 0) frontX = 0;
            if (frontX > w - 1) frontX = w - 1;

            foreach (int czp in AxisPositions(lo, hi, step))
                BuildColumn(cells, frontX, czp, r, 1, yTop, col, w, d);
        }
    }
}
