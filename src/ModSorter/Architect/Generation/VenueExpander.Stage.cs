using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class VenueExpander
{
    // ===== ステージ =====
    private static void BuildStage(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 5, 63);
        int d = Clamp(spec.Depth, 5, 63);
        int deckH = Clamp(spec.VenueStage ?? 3, 1, 10);
        int backH = Clamp(spec.VenueWall ?? 8, 0, 20);
        int postH = Clamp(spec.VenueRoofHeight ?? 6, 0, 20);
        bool gable = (spec.RoofType ?? "gable").Trim().ToLowerInvariant() != "flat";

        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                bool edge = x == 0 || z == 0 || x == w - 1 || z == d - 1;
                for (int y = 0; y < deckH; y++) cells[(x, y, z)] = edge ? p.Accent : p.Structure;
                cells[(x, deckH, z)] = p.Field;
            }

        // 背面の幕（正面の反対＝z=0 側）。
        for (int x = 0; x < w && backH > 0; x++)
            for (int y = deckH + 1; y <= deckH + backH; y++)
                cells[(x, y, 0)] = p.Accent;

        if (postH <= 0) return;

        int eave = deckH + postH;
        foreach (var (px, pz) in new[] { (0, 0), (w - 1, 0), (0, d - 1), (w - 1, d - 1) })
            for (int y = deckH + 1; y <= eave; y++) cells[(px, y, pz)] = p.Structure;

        for (int x = 0; x < w; x++) { cells[(x, eave, 0)] = p.Structure; cells[(x, eave, d - 1)] = p.Structure; }
        for (int z = 0; z < d; z++) { cells[(0, eave, z)] = p.Structure; cells[(w - 1, eave, z)] = p.Structure; }

        if (!gable)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    cells[(x, eave + 1, z)] = p.Roof;
            return;
        }

        // 切妻。棟は x 軸に平行（客席から見て軒が正面に来る）。
        for (int z = 0; z < d; z++)
        {
            int k = Math.Min(z, d - 1 - z);
            for (int x = 0; x < w; x++) cells[(x, eave + 1 + k, z)] = p.Roof;
        }
        // 妻面を塞ぐ。
        foreach (int gx in new[] { 0, w - 1 })
            for (int z = 0; z < d; z++)
            {
                int k = Math.Min(z, d - 1 - z);
                for (int y = eave + 1; y < eave + 1 + k; y++) cells[(gx, y, z)] = p.Accent;
            }
    }
}
