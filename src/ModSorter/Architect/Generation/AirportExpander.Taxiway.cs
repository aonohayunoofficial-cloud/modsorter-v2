using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 誘導路 =====
    // 幅 23m 以上が基準。中心線は黄の実線 1 本、両縁に縁標識の 2 本線が走る。
    private static void BuildTaxiway(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        double scale = Math.Max(1, spec.AirportScale ?? 1);
        int w = Clamp(spec.Width, 4, 48);
        int len = Clamp(spec.Depth, 8, 64);
        int shoulder = Clamp(spec.AirportShoulder ?? M0(TaxiShoulderM, scale), 0, 16);

        if (shoulder > 0)
        {
            Fill(cells, -shoulder, -1, 0, 0, 0, len - 1, p.Shoulder);
            Fill(cells, w, w + shoulder - 1, 0, 0, 0, len - 1, p.Shoulder);
        }

        Fill(cells, 0, w - 1, 0, 0, 0, len - 1, p.Pave);

        if (spec.AirportMarking)
        {
            // 中心線標識。誘導路は実線。
            int cx = (w - 1) / 2;
            int cw = M(TaxiCenterWidthM, scale);
            Fill(cells, cx, cx + cw - 1, 0, 0, 0, len - 1, p.Mark);

            // 誘導路縁標識。舗装の縁から 1 マス内側に走る連続線。
            foreach (int x in new[] { 1, w - 2 })
                if (x > 0 && x < w - 1 && (x < cx || x > cx + cw - 1))
                    Fill(cells, x, x, 0, 0, 0, len - 1, p.Line);
        }

        EdgeLights(cells, p, w, len, spec.AirportEdgeLight ?? (int)EdgeLightM, scale);
    }
}
