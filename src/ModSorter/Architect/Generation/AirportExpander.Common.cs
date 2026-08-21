using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // ===== 共通ヘルパー =====

    // 実寸(m) → マス数。最低 1 マス（塗る対象が消えないように）。
    private static int M(double meters, double scale)
        => Math.Max(1, (int)Math.Round(meters / scale));

    // 実寸(m) → マス数。0 を許す（間隔・空きで「無し」を表せるように）。
    private static int M0(double meters, double scale)
        => Math.Max(0, (int)Math.Round(meters / scale));

    // 中央 1 マスを確保するため偶数を奇数へ丸める。
    private static int Odd(int v) => (v % 2 == 0) ? v + 1 : v;

    // 進入端標識の本数（ICAO Annex 14 Vol.I の表）。幅は実寸(m)。
    private static int ThresholdStripes(double widthM)
    {
        if (widthM < 20.5) return 4;    // 18m 級
        if (widthM < 26.5) return 6;    // 23m 級
        if (widthM < 37.5) return 8;    // 30m 級
        if (widthM < 52.5) return 12;   // 45m 級
        return 16;                       // 60m 級
    }

    // 中心線から左右対称に、指定の横距離・幅で帯を置く。接地帯標識と着陸目標点標識に使う。
    private static void PairBand(
        Dictionary<(int x, int y, int z), string> cells,
        int cx, int w, double offM, double bandM, int z0, int z1, double scale, string block)
    {
        int off = M(offM, scale);
        int bw = M(bandM, scale);

        int ra = cx + off, rb = ra + bw - 1;
        int lb = cx - off, la = lb - bw + 1;

        if (ra < w) Fill(cells, Math.Max(0, ra), Math.Min(w - 1, rb), 0, 0, z0, z1, block);
        if (lb >= 0) Fill(cells, Math.Max(0, la), Math.Min(w - 1, lb), 0, 0, z0, z1, block);
    }

    // 舗装の両縁に沿って一定間隔で灯火を置く。間隔は実寸(m)。
    private static void EdgeLights(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int w, int len, int intervalM, double scale)
    {
        if (intervalM <= 0) return;
        int step = M0(intervalM, scale);
        if (step <= 0) return;

        for (int z = step / 2; z < len; z += step)
        {
            cells[(0, 1, z)] = p.Light;
            cells[(w - 1, 1, z)] = p.Light;
        }
    }

    private static void Fill(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string block)
    {
        if (x1 < x0 || y1 < y0 || z1 < z0) return;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    cells[(x, y, z)] = block;
    }

    private static string Face(string? f)
    {
        string v = (f ?? "south").Trim().ToLowerInvariant();
        return v == "north" || v == "east" || v == "west" ? v : "south";
    }

    // 進入端側を z の小さい側（north）として組んだものを、指定の向きへ回す。
    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> src, string face)
    {
        if (face == "north" || src.Count == 0) return src;

        int minX = src.Keys.Min(k => k.x), minZ = src.Keys.Min(k => k.z);
        int w = src.Keys.Max(k => k.x) - minX + 1;
        int d = src.Keys.Max(k => k.z) - minZ + 1;

        var dst = new Dictionary<(int x, int y, int z), string>(src.Count);
        foreach (var kv in src)
        {
            int x = kv.Key.x - minX, z = kv.Key.z - minZ;
            (int nx, int nz) = face switch
            {
                "south" => (w - 1 - x, d - 1 - z),
                "east" => (d - 1 - z, x),
                "west" => (z, w - 1 - x),
                _ => (x, z)
            };
            dst[(nx, kv.Key.y, nz)] = kv.Value;
        }
        return dst;
    }

    private static List<GeneratedBlock> Normalize(Dictionary<(int x, int y, int z), string> cells)
    {
        if (cells.Count == 0) return new List<GeneratedBlock>();

        int minX = cells.Keys.Min(k => k.x);
        int minY = cells.Keys.Min(k => k.y);
        int minZ = cells.Keys.Min(k => k.z);

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x - minX,
                Y = kv.Key.y - minY,
                Z = kv.Key.z - minZ,
                Id = kv.Value
            })
            .ToList();
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? candidate, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = allowed.FirstOrDefault(
                a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }
}
