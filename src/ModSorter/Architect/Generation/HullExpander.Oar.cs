using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 櫂（オール）。舷縁の外へ出し、先へ行くほど1段ずつ下げて水面へ向ける。
//
// 実物の根拠（レパント 1571 のヴェネツィア・ガレア・ソッティレ）:
//   全長42m・型幅5.1mで長さ/幅は8:1。漕ぎ座は片舷24。3人が1つの座に並んで
//   各自1挺を漕ぐ alla sensile 式なので、漕ぎ手は計144人・櫂も144挺。
//   座の間隔は約1.2mなので、1マス=1mでは1 station に1挺として並べるのが
//   実物にいちばん近い見え方になる。
//   櫂そのものは10m級で舷から大きく張り出すが、外寸と生成物を食い違わせない
//   範囲に収めるため3マスで切り、水面の手前で止める。
public static partial class HullExpander
{
    // 舷から外へ出すマス数。1マスごとに1段下げるので、先端は舷縁より3段下。
    private const int OarReach = 3;

    private static void BuildOars(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t)
    {
        if (top.OarPerSide <= 0) return;

        int z0 = Math.Max(1, f.L / 2 - top.OarPerSide / 2);
        for (int i = 0; i < top.OarPerSide; i++)
        {
            int z = z0 + i;
            if (z >= f.L - 1) break;

            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);
            if (x1 - x0 < 2) continue;   // 舷が寄るところには櫂を出さない

            // 舷縁（舷墻があればその天端）から出す。ここが櫂の支点になる。
            int y = dk + f.Bulwark;

            for (int k = 1; k <= OarReach; k++)
            {
                int oy = y - (k - 1);
                if (oy <= f.WL) break;   // 水面より下へは下ろさない
                PutSpar(cells, props, (x0 - k, oy, z), t.Mast, "x");
                PutSpar(cells, props, (x1 + k, oy, z), t.Mast, "x");
            }
        }
    }
}
