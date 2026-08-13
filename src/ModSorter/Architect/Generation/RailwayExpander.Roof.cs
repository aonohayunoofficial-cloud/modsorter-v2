using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 鉄道の屋根形状。上屋・跨線橋・車庫で同じ勾配ロジックを使う。
// 1マス=1m なので、勾配は「何マス進んで1マス上がるか」で持つ。
// 3 ≒ 18度（4:12相当）、4 ≒ 14度。実物の上屋・庫の屋根はこのあたり。
public static partial class RailwayExpander
{
    private static string RoofShapeOf(string? v) => (v ?? "gable").Trim().ToLowerInvariant() switch
    {
        "flat" => "flat",
        "shed" => "shed",
        "arch" => "arch",
        _ => "gable",
    };

    // 勾配方向の座標 a における屋根面の y。baseY は軒先の高さ。
    private static int RoofYAt(string shape, int a, int a0, int a1, int baseY, int pitch)
    {
        if (pitch < 1) pitch = 1;
        int span = a1 - a0;
        double ac = (a0 + a1) / 2.0;

        switch (shape)
        {
            case "flat":
                return baseY;
            case "shed":
                return baseY + (a - a0) / pitch;
            case "arch":
                {
                    double r = span / 2.0;
                    double dx = a - ac;
                    double v = r * r - dx * dx;
                    if (v < 0) v = 0;
                    return baseY + (int)Math.Round(Math.Sqrt(v) / pitch);
                }
            default:
                {
                    int half = span / 2;
                    int d = (int)Math.Round(Math.Abs(a - ac));
                    return baseY + (half - d) / pitch;
                }
        }
    }

    // 屋根面を張る。slopeAlongX=true なら x に勾配を付けて z へ通す（上屋・車庫）。
    // false なら z に勾配を付けて x へ通す（跨線橋。通路が x に走るため）。
    // 段差が2以上開く形（アーチ）でも縦に埋めるので、屋根に隙間は空かない。
    private static void RoofSheet(Dictionary<(int x, int y, int z), string> cells, string shape,
        bool slopeAlongX, int a0, int a1, int baseY, int pitch, int b0, int b1, string id)
    {
        int prev = int.MinValue;
        for (int a = a0; a <= a1; a++)
        {
            int y = RoofYAt(shape, a, a0, a1, baseY, pitch);
            int lo = y, hi = y;
            if (prev != int.MinValue)
            {
                lo = Math.Min(y, prev + 1);   // 上り側の段差を埋める
                hi = Math.Max(y, prev - 1);   // 下り側の段差を埋める（アーチは2段以上落ちる）
            }
            if (slopeAlongX) Fill(cells, a, a, lo, hi, b0, b1, id);
            else Fill(cells, b0, b1, lo, hi, a, a, id);
            prev = y;
        }
    }
}
