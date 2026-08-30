using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 貨物艙口（ハッチ）と荷役デリック。
//
// 実物の根拠（リバティ船 EC2型 1941）:
//   全長134.57m・型幅17.34m・深さ11.38mで貨物艙は5つ。艙口は甲板の中央線上に開き、
//   幅は型幅の1/3級・前後長は艙の長さの半分級。周りに1ft半＝0.5mのハッチコーミング
//   （縁の立ち上がり）が付き、その上へ板とキャンバスで蓋をする。
//   艙口の前後にデリックポスト（荷役柱）が立ち、腕木（ブーム）が艙口の上へ倒れる。
//   リバティ船の荷役装置は5t級のデリックが計10基級。
//   艙口の前後長を艙の全部にせず半分に留めるのは、残りが甲板として歩けるようにする
//   実船の作りに合わせるため。
public static partial class HullExpander
{
    private static void BuildHolds(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t)
    {
        if (top.Holds <= 0) return;

        // 艙は船体を等分した位置へ置く。船体中央のデッキハウスの範囲は避ける。
        int hlen = Math.Max(3, f.L * top.HouseLen / 100);
        int hz0 = Math.Max(1, (f.L - hlen) / 2 + top.HouseShift);
        int hz1 = hz0 + hlen - 1;

        int step = f.L / (top.Holds + 1);
        if (step < 3) return;

        for (int i = 1; i <= top.Holds; i++)
        {
            int zc = step * i;
            int half = Math.Max(1, step / 3);
            int za = zc - half, zb = zc + half;
            if (top.HouseDecks > 0 && zb >= hz0 - 1 && za <= hz1 + 1) continue;
            if (za < 1 || zb > f.L - 2) continue;

            DeckSpanAt(f, zc, out int dx0, out int dx1);
            int w = dx1 - dx0 + 1;
            if (w < 7) continue;   // 艙口＋コーミング＋両舷の通路が取れない

            // 艙口の幅は型幅の1/3級。両舷に通路が2マス以上残る幅へ抑える。
            int hw = Math.Max(1, Math.Min(f.B / 3, w - 5) / 2);
            int cx = (dx0 + dx1) / 2;

            for (int z = za; z <= zb; z++)
            {
                DeckSpanAt(f, z, out int sx0, out int sx1);
                int dk = f.DeckY(z);
                for (int x = cx - hw; x <= cx + hw; x++)
                {
                    if (x <= sx0 || x >= sx1) continue;
                    // 甲板を抜いて艙内を見せ、周りへコーミングを立てる。
                    cells.Remove((x, dk, z));
                }
            }

            // コーミング。艙口の外周を1マス立ち上げる。実船の0.5mを1マスで表す。
            for (int z = za - 1; z <= zb + 1; z++)
            {
                DeckSpanAt(f, z, out int sx0, out int sx1);
                int dk = f.DeckY(z);
                for (int x = cx - hw - 1; x <= cx + hw + 1; x++)
                {
                    if (x <= sx0 || x >= sx1) continue;
                    bool edge = z == za - 1 || z == zb + 1
                             || x == cx - hw - 1 || x == cx + hw + 1;
                    if (edge) cells[(x, dk + 1, z)] = t.Fitting;
                }
            }

            if (top.Derrick) BuildDerrick(cells, props, f, t, cx, za - 2, zb + 2, zc);
        }
    }

    // デリック。艙口の前後に柱を立て、腕木を艙口の上へ斜めに倒す。
    // 柱の高さは艙口の幅の2倍級（実船の5tデリックは腕木が艙口の外まで届く長さ）。
    private static void BuildDerrick(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, TopPalette t, int cx, int za, int zb, int zc)
    {
        foreach (int z in new[] { za, zb })
        {
            if (z < 1 || z > f.L - 2) continue;
            DeckSpanAt(f, z, out int sx0, out int sx1);
            if (cx <= sx0 || cx >= sx1) continue;

            int dk = f.DeckY(z);
            int h = 8;
            for (int y = dk + 1; y <= dk + h; y++) cells[(cx, y, z)] = t.Mast;

            // 腕木。柱の頂の1つ下から艙口の中央へ向けて1マスごとに1段下げる。
            int dir = z < zc ? 1 : -1;
            int by = dk + h - 1;
            for (int k = 1; k <= 4; k++)
            {
                int bz = z + dir * k;
                int y = by - k;
                if (bz < 1 || bz > f.L - 2 || y <= dk + 1) break;
                PutSpar(cells, props, (cx, y, bz), t.Mast, "z");
            }
        }
    }
}
