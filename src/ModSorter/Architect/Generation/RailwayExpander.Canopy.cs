using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// ホーム上屋。プラットフォームとは別生成物で、ホーム天端の上に重ねて置く。
//
// y の基準は「ホーム天端の1マス上」＝旅客が立つ空間の最下段を y=0 とする。
// height は ホーム面から屋根下端（軒高）までのマス数。柱は y=0〜height-2、
// 梁が y=height-1、屋根が y=height から。height=4 なら頭上クリア3マス。
//
// ===== 実寸の出典 =====
//   軒高       … ホーム面上 3.5〜4.5m 級。駅区間では軌道から上屋まで 6.5m を確保する
//                例がある（ホーム高さ1.1mを引くとホーム面上 5.4m）。
//   柱間隔     … 古レール上屋で約4.5m（5ヤード）。現代は5m級。
//   柱の位置   … ホーム限界は軌道中心から1.80m（通過列車あり）。ホーム縁端が1.475mなので、
//                縁端より1マス内側に立てれば確実に外れる。点状ブロックの位置とも合う。
//   軒の出     … 軌道の上へ張り出すと建築限界（軌道中心±1.9m・高さ5.7m）に触るため、
//                軒を出すときは軒高6以上（レール面上7m）へ自動で引き上げる。
public static partial class RailwayExpander
{
    private static void BuildCanopy(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        int w = Clamp(spec.Width, 3, 24);
        int len = Clamp(spec.Depth, 4, 256);
        int h = Clamp(spec.Height, 3, 12);
        int eave = Clamp(spec.RailEave ?? 0, 0, 3);
        if (eave > 0 && h < 6) h = 6;   // 建築限界を避ける

        int step = Clamp(spec.RailColumnStep ?? 5, 3, 16);
        int rows = Clamp(spec.RailColumnRows ?? 1, 1, 2);
        int pitch = Clamp(spec.RailRoofPitch ?? 3, 1, 8);
        int lightStep = Clamp(spec.RailLightStep ?? 10, 0, 32);
        bool gutter = spec.RailGutter;
        string shape = RoofShapeOf(spec.RailCanopyRoof);

        // 柱の x。1列なら中央、2列ならホーム縁端の1マス内側。
        int[] cols = rows == 1 ? new[] { (w - 1) / 2 } : new[] { 1, w - 2 };
        int beamY = h - 1;
        int x0 = -eave, x1 = w - 1 + eave;

        // 柱と横梁
        for (int z = 0; z < len; z += step) Frame(cells, cols, w, beamY, z, p);
        if ((len - 1) % step != 0) Frame(cells, cols, w, beamY, len - 1, p);

        // 縦桁
        foreach (int cx in cols) Fill(cells, cx, cx, beamY, beamY, 0, len - 1, p.Girder);

        // 屋根
        RoofSheet(cells, shape, true, x0, x1, h, pitch, 0, len - 1, p.Girder);

        // 雨とい。軒先の1マス下に通す。片流れは低い側だけ。
        if (gutter)
        {
            int gy0 = RoofYAt(shape, x0, x0, x1, h, pitch) - 1;
            Fill(cells, x0, x0, gy0, gy0, 0, len - 1, p.Trim);
            if (shape != "shed")
            {
                int gy1 = RoofYAt(shape, x1, x0, x1, h, pitch) - 1;
                Fill(cells, x1, x1, gy1, gy1, 0, len - 1, p.Trim);
            }
        }

        // 照明。梁の1マス下に吊る。柱と重ならないよう柱間の中央から並べる。
        if (lightStep > 0)
        {
            for (int z = step / 2; z < len; z += lightStep)
                foreach (int cx in cols)
                    cells[(cx, beamY - 1, z)] = p.Trim;
        }
    }

    // 1組の柱と横梁。横梁はホーム幅いっぱいに通す。
    private static void Frame(Dictionary<(int x, int y, int z), string> cells,
        int[] cols, int w, int beamY, int z, Palette p)
    {
        foreach (int cx in cols) Fill(cells, cx, cx, 0, beamY - 1, z, z, p.Body);
        Fill(cells, 0, w - 1, beamY, beamY, z, z, p.Body);
    }
}
