using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 屋根（頂冠形）: ドーム・四角錐・尖塔。いずれも y=h-1 を全面で塞いでから
// 上へ絞りながら積むので、足元に穴が空かない。矩形平面のときだけ呼ばれる。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // ドーム屋根: 建物の上面(w×d)を底面とする半楕円体の殻を、y=h-1 から上に積む。
    // 半径 rx=w/2, rz=d/2。ドーム高さ ry は spec.DomeHeight（未指定なら控えめな既定）。
    // 殻だけ残す（中空）ことで屋根らしくする。底面 y=h-1 は全面塞いで天井とする。
    private static void BuildDomeRoof(
        Dictionary<(int x, int y, int z), string> cells,
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

        // まず底面を天井として全面塞ぐ（ドームの足元の穴を防ぐ）
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, baseY, z)] = roof;

        // 半楕円体の殻。yLayer=0..ry の各層で、その高さの輪郭リングを置く。
        for (int yi = 0; yi <= ry; yi++)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                {
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

    // ピラミッド屋根（四角錐）: 底面(w×d)を y=h-1 に全面で敷き、そこから上へ
    // 1段ごとに全周を1マスずつ内側へ絞りながら積む。頂点で1〜2マスに収束する。
    // pyramids（建物全体を四角錐にしたいとき）や塔・東洋風の屋根に使える。
    private static void BuildPyramidRoof(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int h, string roof)
    {
        // 底面（壁の最上層の上）を天井として全面塞ぐ。錐の足元の穴を防ぐ。
        int baseY = h - 1;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, baseY, z)] = roof;

        // 段ごとに内側へ絞る。step マスだけ各辺から内側に入った矩形リング（中身も塗る）。
        // 頂点まで積めるよう、絞り切るまで層を重ねる。
        int maxStep = (Math.Min(w, d) + 1) / 2; // これ以上絞ると矩形が消える
        for (int step = 1; step <= maxStep; step++)
        {
            int x0 = step, x1 = w - 1 - step;
            int z0 = step, z1 = d - 1 - step;
            if (x1 < x0 || z1 < z0) break; // 絞り切った（頂点に到達）

            int y = baseY + step;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    cells[(x, y, z)] = roof;
        }
    }

    // 尖塔（spire）の頂部: 壁の上端を全面で塞いだうえで、全周を周期的に1マスずつ
    // 内側へ絞りながら上へ積む。四角錐(pyramid)より鋭く高い輪郭になる。
    // 絞る周期は roof_pitch を流用する。1=1段ごと（四角錐と同じ45°）、
    // 大きいほど段数が増えて細く鋭く伸びる（4で最も鋭い）。
    // 中実なので隙間は原理的に空かない。矩形平面のときだけ呼ばれる。
    private static void BuildSpireRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string roof)
    {
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, h - 1, z)] = roof;

        int per = Clamp(spec.RoofPitch ?? 2, 1, 4);

        for (int k = 1; ; k++)
        {
            int inset = k / per;
            int x0 = inset, x1 = w - 1 - inset;
            int z0 = inset, z1 = d - 1 - inset;
            if (x0 > x1 || z0 > z1) break;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    cells[(x, h - 1 + k, z)] = roof;
        }
    }
}
