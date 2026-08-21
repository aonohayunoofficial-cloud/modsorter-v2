using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

public static partial class AirportExpander
{
    // 各ゲートの道路側出入口とアプロンドライブ式の搭乗橋。
    // 搭乗橋の床はロタンダの高さ。実物は 5m 級なので出発階の床に合わせる。
    private static void TerminalGates(
        Dictionary<(int x, int y, int z), string> cells, Palette p,
        int gates, int pitch, int depth, int levels, int lh, int bridge)
    {
        int fy = levels >= 2 ? lh : 4;

        for (int i = 0; i < gates; i++)
        {
            int cx = i * pitch + pitch / 2;   // エプロンのスポット中心と同じ式

            // 道路側の出入口。
            for (int x = cx - 2; x <= cx + 2; x++)
                for (int y = 1; y <= 3; y++)
                    cells.Remove((x, y, depth - 1));

            if (bridge <= 0) continue;

            // ロタンダの開口。
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = fy; y <= fy + 2; y++)
                    cells.Remove((x, y, 0));

            // トンネル。勾配は 10% が上限なので 10 マスにつき 1 マス下げる。
            for (int k = 1; k <= bridge; k++)
            {
                int y = Math.Max(2, fy - k / 10);
                Fill(cells, cx - 1, cx + 1, y, y, -k, -k, p.Pave);
                Fill(cells, cx - 1, cx - 1, y + 1, y + 2, -k, -k, p.Glass);
                Fill(cells, cx + 1, cx + 1, y + 1, y + 2, -k, -k, p.Glass);
                Fill(cells, cx - 1, cx + 1, y + 3, y + 3, -k, -k, p.Roof);
            }

            // 走行装置の支柱。ロタンダから 2/3 の位置に立てる。
            int sk = Math.Max(2, bridge * 2 / 3);
            int sy = Math.Max(2, fy - sk / 10);
            Fill(cells, cx, cx, 0, sy - 1, -sk, -sk, p.Body);

            // 先端のキャブ。幅を広げて機体側の口にする。
            if (bridge >= 4)
            {
                int ey = Math.Max(2, fy - bridge / 10);
                Fill(cells, cx - 2, cx + 2, ey, ey, -bridge, -bridge + 1, p.Pave);
                Fill(cells, cx - 2, cx - 2, ey + 1, ey + 2, -bridge, -bridge + 1, p.Glass);
                Fill(cells, cx + 2, cx + 2, ey + 1, ey + 2, -bridge, -bridge + 1, p.Glass);
                Fill(cells, cx - 1, cx + 1, ey + 1, ey + 2, -bridge, -bridge, p.Glass);
                Fill(cells, cx - 2, cx + 2, ey + 3, ey + 3, -bridge, -bridge + 1, p.Roof);
            }
        }
    }
}
