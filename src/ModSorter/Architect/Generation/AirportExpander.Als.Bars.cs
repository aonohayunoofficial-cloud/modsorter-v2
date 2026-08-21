using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== クロスバー =====
    // バレットを使うときは本数が減る（CAT I は 300m のみ、CAT II/III は 150m と 300m）。
    private static void AlsCrossbars(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        string type, bool simple, bool barrette, int barHalf,
        int cx, int len, int ty, double scale)
    {
        double[] bars = simple
            ? new[] { 300.0 }
            : (barrette
                ? (type == "cat2" ? new[] { 150.0, 300.0 } : new[] { 300.0 })
                : new[] { 150.0, 300.0, 450.0, 600.0, 750.0 });

        foreach (double bm in bars)
        {
            int z = M(bm, scale);
            if (z > len) continue;

            // 300m のクロスバーは長さ 30m（簡易式は 18m か 30m のうち 30m を採る）。
            // 他は外縁を結ぶ線が進入端の 300m 先で収束するように広げる。
            double barLenM = (Math.Abs(bm - 300.0) < 0.5)
                ? AlsCrossbar300M
                : AlsCrossbar300M * (bm + AlsConvergeM) / (300.0 + AlsConvergeM);

            int half = Math.Max(1, M(barLenM, scale) / 2);
            for (int x = cx - half; x <= cx + half; x++)
            {
                if (Math.Abs(x - cx) <= barHalf) continue;   // 中心はセンターラインが占める
                Trestle(cells, x, z, ty, p);
                cells[(x, ty + 1, z)] = p.Light;
            }
        }
    }

    // ===== 側方列（CAT II/III のみ）=====
    private static void AlsSideRows(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        string type, int cx, int rw, int len, int step, int ty, double scale)
    {
        if (type != "cat2") return;

        int rowLen = Math.Min(len, M(AlsSideRowM, scale));
        int off = Math.Max(2, rw / 2);
        for (int k = 1; k * step <= rowLen; k++)
        {
            int z = k * step;
            foreach (int x in new[] { cx - off, cx + off })
            {
                Trestle(cells, x, z, ty, p);
                cells[(x, ty + 1, z)] = p.Mark;   // 側方列は赤
            }
        }
    }

    // ===== PAPI =====
    // 滑走路の左側、進入端から 300m の位置に 4 灯を横に並べる。
    private static void AlsPapi(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        bool papi, int cx, int rw, int len, double scale)
    {
        if (!papi) return;

        int z = M(PapiOffsetM, scale);
        if (z > len) return;

        int x0 = cx - Math.Max(2, rw / 2 + M(PapiSideM, scale));
        for (int i = 0; i < 4; i++)
        {
            int x = x0 - i * Math.Max(1, M(9.0, scale));
            Fill(cells, x, x, 0, 0, z, z, p.Pave);
            cells[(x, 1, z)] = p.Light;
        }
    }
}
