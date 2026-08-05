using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 屋根（頂冠形）: ドーム・四角錐・尖塔。いずれも足元（y=h-1）を平面マスク一杯で塞いでから
// 上へ絞りながら積むので、屋根の付け根に穴が空かない。
// 絞りはマスクの4近傍侵食で行う。矩形一杯のマスクでは侵食が「各辺から1マス内側の矩形」と
// 完全に一致するため、従来の等間隔インセットと同じ結果になる（回帰なし）。
// 円形平面では侵食が半径を1ずつ縮めるので、四角錐・尖塔がそのまま円錐になる。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // ドーム屋根: 建物の上面(w×d)を底面とする半楕円体の殻を、y=h-1 から上に積む。
    // 半径 rx=w/2, rz=d/2。ドーム高さ ry は spec.DomeHeight（未指定なら控えめな既定）。
    // 殻だけ残す（中空）ことで屋根らしくする。底面 y=h-1 はマスク一杯に塞いで天井とする。
    // マスク外のセルには置かないので、円形平面では円形のドームになる。
    private static void BuildDomeRoof(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot,
        StructureSpec spec, int w, int d, int h, string roof)
    {
        double rx = w / 2.0;
        double rz = d / 2.0;
        // 中心（底面）。整数格子の中央。
        double cx = (w - 1) / 2.0;
        double cz = (d - 1) / 2.0;

        // ドームの高さ。未指定なら水平半径の小さい方に合わせる（半球に近い）。
        int ry = spec.DomeHeight.HasValue && spec.DomeHeight.Value >= 1
            ? spec.DomeHeight.Value
            : Math.Max(2, (int)Math.Round(Math.Min(rx, rz)));

        int baseY = h - 1; // ドームの底面（壁の最上層の上）

        // まず底面を天井としてマスク一杯に塞ぐ（ドームの足元の穴を防ぐ）
        foreach (var (x, z) in foot)
            cells[(x, baseY, z)] = roof;

        // 半楕円体の殻。yLayer=0..ry の各層で、その高さの輪郭リングを置く。
        for (int yi = 0; yi <= ry; yi++)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                {
                    // 平面マスクの外へは出さない（宙に浮くセルを作らない）。
                    if (!foot.Contains((x, z))) continue;

                    double nx = (x - cx) / (rx <= 0 ? 1 : rx);
                    double nz = (z - cz) / (rz <= 0 ? 1 : rz);
                    double ny = (double)yi / (ry <= 0 ? 1 : ry);
                    double v = nx * nx + nz * nz + ny * ny;
                    if (v > 1.0) continue; // 楕円体の外

                    // 殻判定: 隣接が外側になるセルだけ残す（表面）
                    bool surface =
                        Outside(x + 1, cx, rx, z, cz, rz, yi, ry) ||
                        Outside(x - 1, cx, rx, z, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z + 1, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z - 1, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z, cz, rz, yi + 1, ry);
                    if (!surface) continue;

                    cells[(x, baseY + yi, z)] = roof;
                }
        }
    }

    // 指定セルが半楕円体の外側か（殻判定用）
    private static bool Outside(int x, double cx, double rx, int z, double cz, double rz, int yi, int ry)
    {
        double nx = (x - cx) / (rx <= 0 ? 1 : rx);
        double nz = (z - cz) / (rz <= 0 ? 1 : rz);
        double ny = (double)yi / (ry <= 0 ? 1 : ry);
        return (nx * nx + nz * nz + ny * ny) > 1.0;
    }

    // ピラミッド屋根（四角錐）: 底面を y=h-1 にマスク一杯で敷き、そこから上へ
    // 1段ごとにマスクを1マスずつ内側へ削りながら積む。頂点で1〜数マスに収束する。
    // 矩形なら従来どおり全周1マスずつのインセット、円形なら円錐になる。
    private static void BuildPyramidRoof(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, int h, string roof)
    {
        // 底面（壁の最上層の上）を天井としてマスク一杯に塞ぐ。錐の足元の穴を防ぐ。
        int baseY = h - 1;
        foreach (var (x, z) in foot)
            cells[(x, baseY, z)] = roof;

        // 段ごとに1マス侵食する。中身も塗るので隙間は原理的に空かない。
        var layer = foot;
        for (int step = 1; ; step++)
        {
            layer = ErodeMask(layer);
            if (layer.Count == 0) break; // 絞り切った（頂点に到達）
            int y = baseY + step;
            foreach (var (x, z) in layer)
                cells[(x, y, z)] = roof;
        }
    }

    // 尖塔（spire）の頂部: 壁の上端をマスク一杯で塞いだうえで、周期的に1マスずつ
    // 内側へ削りながら上へ積む。四角錐(pyramid)より鋭く高い輪郭になる。
    // 絞る周期は roof_pitch を流用する。1=1段ごと（四角錐と同じ45°）、
    // 大きいほど段数が増えて細く鋭く伸びる（4で最も鋭い）。
    // 中実なので隙間は原理的に空かない。
    private static void BuildSpireRoof(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, StructureSpec spec, int h, string roof)
    {
        foreach (var (x, z) in foot)
            cells[(x, h - 1, z)] = roof;

        int per = Clamp(spec.RoofPitch ?? 2, 1, 4);

        // 侵食回数ごとのマスクを使い回す（同じ inset の層が per 段続くため）。
        var masks = new List<HashSet<(int x, int z)>> { foot };
        for (int k = 1; ; k++)
        {
            int inset = k / per;
            while (masks.Count <= inset)
                masks.Add(ErodeMask(masks[masks.Count - 1]));

            var layer = masks[inset];
            if (layer.Count == 0) break;
            foreach (var (x, z) in layer)
                cells[(x, h - 1 + k, z)] = roof;
        }
    }

    // 平面マスクを1マスぶん内側へ削る（4近傍侵食）。
    // 矩形一杯のマスクでは「各辺から1マス内側の矩形」と完全に一致するので、
    // 従来の等間隔インセットと同じ絞り方になる。円形なら半径が1縮む。
    private static HashSet<(int x, int z)> ErodeMask(HashSet<(int x, int z)> mask)
    {
        var next = new HashSet<(int x, int z)>();
        foreach (var (x, z) in mask)
            if (mask.Contains((x + 1, z)) && mask.Contains((x - 1, z)) &&
                mask.Contains((x, z + 1)) && mask.Contains((x, z - 1)))
                next.Add((x, z));
        return next;
    }
}
