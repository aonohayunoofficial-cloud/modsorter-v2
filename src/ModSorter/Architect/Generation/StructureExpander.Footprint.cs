using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 平面形状マスク（フットプリント）と、全体で使う共通小物(Clamp / Pick)。
// 壁・土台・パラペット・平屋根はすべてこのマスクの縁に沿って作るので、
// L字・コの字・十字でも内側角まで外皮が正しく回る。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // ===== フットプリント（平面形状マスク）=====
    // spec の footprint 指定から、建てる平面(X-Z)のマスクを確定的に作る。
    // 手順: プリセット形状 → footprint_add をすべて OR → footprint_sub をすべて減算。
    // add をすべて足してから sub をすべて引くので、add 同士・sub 同士の順序は結果に影響しない。
    // 未指定（shape=null かつ add/sub 空）なら全面 true＝従来の矩形と完全一致。
    private static HashSet<(int x, int z)> BuildFootprint(StructureSpec spec, int w, int d)
    {
        var mask = new HashSet<(int x, int z)>();

        string shape = (spec.FootprintShape ?? "rect").Trim().ToLowerInvariant();
        int cutW = spec.FootprintParams?.CutW ?? 0;
        int cutD = spec.FootprintParams?.CutD ?? 0;
        // 未指定(0以下)なら幅・奥行のおよそ半分を既定の切り欠き量にする。
        if (cutW <= 0) cutW = Math.Max(1, w / 2);
        if (cutD <= 0) cutD = Math.Max(1, d / 2);
        // 切り欠きが全体を食い尽くさないよう上限を掛ける。
        cutW = Clamp(cutW, 1, Math.Max(1, w - 1));
        cutD = Clamp(cutD, 1, Math.Max(1, d - 1));

        // 1) プリセットで大枠を作る。
        switch (shape)
        {
            case "l":
                // L字: 右奥(x大・z大)の cutW×cutD の一角を削る。
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        if (!(x >= w - cutW && z >= d - cutD))
                            mask.Add((x, z));
                break;

            case "u":
                // コの字: 手前(z大側)の中央を幅 cutW・深さ cutD で削り込む。
                {
                    int lo = (w - cutW) / 2;
                    int hi = lo + cutW - 1;
                    for (int x = 0; x < w; x++)
                        for (int z = 0; z < d; z++)
                            if (!(x >= lo && x <= hi && z >= d - cutD))
                                mask.Add((x, z));
                }
                break;

            case "t":
                // T字: z 小側に横棒（全幅・厚み cutD）、中央に縦棒（幅 cutW・全奥行）。
                {
                    int lo = (w - cutW) / 2;
                    int hi = lo + cutW - 1;
                    for (int x = 0; x < w; x++)
                        for (int z = 0; z < d; z++)
                        {
                            bool bar = z < cutD;                 // 横棒
                            bool stem = (x >= lo && x <= hi);    // 縦棒
                            if (bar || stem) mask.Add((x, z));
                        }
                }
                break;

            case "plus":
                // 十字: 中央縦帯（幅 cutW）＋中央横帯（厚み cutD）。
                {
                    int xlo = (w - cutW) / 2, xhi = xlo + cutW - 1;
                    int zlo = (d - cutD) / 2, zhi = zlo + cutD - 1;
                    for (int x = 0; x < w; x++)
                        for (int z = 0; z < d; z++)
                        {
                            bool vBand = (x >= xlo && x <= xhi);
                            bool hBand = (z >= zlo && z <= zhi);
                            if (vBand || hBand) mask.Add((x, z));
                        }
                }
                break;

            case "circle":
                // 円形: w×d を直径とする楕円。中心は整数格子の中央、半径は w/2・d/2。
                // 端の列も内側に入るので、幅・奥行いっぱいの円になる。
                // 壁は IsEdge によって円周1マス厚のリングになる（中は吹き抜け）。
                {
                    double ccx = (w - 1) / 2.0, ccz = (d - 1) / 2.0;
                    double crx = w / 2.0, crz = d / 2.0;
                    for (int x = 0; x < w; x++)
                        for (int z = 0; z < d; z++)
                        {
                            double nx = (x - ccx) / (crx <= 0 ? 1 : crx);
                            double nz = (z - ccz) / (crz <= 0 ? 1 : crz);
                            if (nx * nx + nz * nz <= 1.0) mask.Add((x, z));
                        }
                }
                break;

            default: // "rect" ほか未知の値は矩形一杯（従来互換）。
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        mask.Add((x, z));
                break;
        }

        // 2) footprint_add をすべて OR で足す（順序非依存）。
        foreach (var r in spec.FootprintAdd ?? new List<Rect>())
            AddRect(mask, r, w, d, add: true);

        // 3) footprint_sub をすべて減算する（add 完了後に一括、順序非依存）。
        foreach (var r in spec.FootprintSub ?? new List<Rect>())
            AddRect(mask, r, w, d, add: false);

        // 空マスク（全部削られた等）になったら安全側で矩形一杯へ戻す。宙抜け生成を防ぐ。
        if (mask.Count == 0)
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    mask.Add((x, z));

        return mask;
    }

    // 矩形 r を建物範囲(0..w-1, 0..d-1)にクランプして、マスクへ加算/減算する。
    private static void AddRect(HashSet<(int x, int z)> mask, Rect r, int w, int d, bool add)
    {
        int x0 = Clamp(r.X, 0, w - 1);
        int z0 = Clamp(r.Z, 0, d - 1);
        int x1 = Clamp(r.X + Math.Max(0, r.W) - 1, 0, w - 1);
        int z1 = Clamp(r.Z + Math.Max(0, r.D) - 1, 0, d - 1);
        if (r.W <= 0 || r.D <= 0) return;
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                if (add) mask.Add((x, z));
                else mask.Remove((x, z));
    }

    // マスクが矩形一杯（全 w*d セルが埋まっている）か。true なら従来の矩形と同一。
    private static bool IsRectangular(HashSet<(int x, int z)> mask, int w, int d)
        => mask.Count == w * d;

    // 指定セルがマスクの「縁」か。4近傍(±x, ±z)のいずれかがマスク外なら縁とみなす。
    // マスク外セルに対しては false。壁・土台をここで判定するので、L字の内側角も正しく回る。
    private static bool IsEdge(HashSet<(int x, int z)> mask, int x, int z)
    {
        if (!mask.Contains((x, z))) return false;
        return !mask.Contains((x + 1, z))
            || !mask.Contains((x - 1, z))
            || !mask.Contains((x, z + 1))
            || !mask.Contains((x, z - 1));
    }

    // 狭間（クレネル）の位置か。縁がどちら向きに走っているかを隣接セルの有無で判定し、
    // x 方向に走る縁は x、z 方向に走る縁は z を周期で見る。向かい合う壁で狭間が揃う。
    // 両方向の隣がある／どちらも無い位置（角・出隅入隅・孤立点）は矢壁を残して false。
    private static bool IsCrenelGap(HashSet<(int x, int z)> foot, int x, int z, int step)
    {
        bool alongX = foot.Contains((x - 1, z)) && foot.Contains((x + 1, z));
        bool alongZ = foot.Contains((x, z - 1)) && foot.Contains((x, z + 1));
        if (alongX == alongZ) return false;
        int i = alongX ? x : z;
        return ((i % step) + step) % step == step - 1;
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
