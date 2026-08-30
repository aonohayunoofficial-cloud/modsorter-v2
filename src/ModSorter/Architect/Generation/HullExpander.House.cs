using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 船体中央のデッキハウス（船橋楼）と煙突。船首・船尾の端に載る船楼は
// HullExpander.Castle.cs が受け持つので、中央に載る箱はこちらで作る。
//
// 実物の根拠:
//   リバティ船（EC2型 1941）は全長134.57m・型幅17.34m・深さ11.38mで、船体中央に
//   3層の船橋楼が載る。下から機関室囲壁・居住区、その上が船橋甲板で、
//   さらに上に操舵室が乗る。階高は7ft6in＝2.3m級なので1層3マス。
//   箱の前後長は全長の15%級で、両舷に1マスずつ通路（サイドデッキ）を残す。
//   煙突は船橋楼の後ろ半分から立ち、頂は船橋の屋根より高い。リバティ船の
//   煙突は直径3.7m級・高さは水面上17m級。
//   SS グレート・ブリテン（1845）は全長98m・型幅15.4mで煙突1本とマスト6本を持ち、
//   煙突は船体中央より少し前に立つ。
public static partial class HullExpander
{
    // 1層の階高。実船の7ft6in＝2.3mを、床1マス＋内法2マスの3マスで表す。
    private const int HouseFloorH = 3;

    private static void BuildDeckHouse(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.HouseDecks <= 0) return;

        int len = Math.Max(3, f.L * top.HouseLen / 100);
        int z0 = Math.Max(1, (f.L - len) / 2 + top.HouseShift);
        int z1 = Math.Min(f.L - 2, z0 + len - 1);
        if (z1 - z0 < 2) return;

        // 箱の平面形は範囲内でいちばん狭い甲板から両舷1マスの通路を引いた幅。
        // いちばん広いところを取ると船体の細るほうで舷から張り出すため、
        // 実船のサイドデッキ（人が前後に通る通路）が消える。
        int hx0 = int.MinValue, hx1 = int.MaxValue;
        for (int z = z0; z <= z1; z++)
        {
            DeckSpanAt(f, z, out int dx0, out int dx1);
            if (dx0 + 1 > hx0) hx0 = dx0 + 1;
            if (dx1 - 1 < hx1) hx1 = dx1 - 1;
        }
        if (hx1 - hx0 < 2) return;

        // 箱の下端も全長で揃える。station ごとの甲板へ合わせると裾が段々になる。
        int baseY = f.DeckY(z0) + 1;
        for (int z = z0; z <= z1; z++) baseY = Math.Max(baseY, f.DeckY(z) + 1);

        for (int d = 0; d < top.HouseDecks; d++)
        {
            // 上の層は前後を1マスずつ詰める。実船の船橋楼も上へ行くほど短くなり、
            // 下の層の屋根が前後で歩ける甲板になる。
            int a = Math.Min(z0 + d, z1 - 1);
            int b = Math.Max(z1 - d, a + 1);
            int lo = baseY + d * HouseFloorH;
            int hi = lo + HouseFloorH - 1;

            for (int z = a; z <= b; z++)
                for (int x = hx0; x <= hx1; x++)
                    for (int y = lo; y <= hi; y++)
                    {
                        bool wall = x == hx0 || x == hx1 || z == a || z == b;
                        bool floor = y == lo;
                        bool roof = y == hi && d == top.HouseDecks - 1;
                        if (wall || floor || roof) cells[(x, y, z)] = t.Castle;
                        else cells.Remove((x, y, z));
                    }

            // 窓。舷側と前面に内法の下段を開ける。実船の居住区も舷窓が並び、
            // 最上層の操舵室は前面が一面の窓になる。
            int wy = lo + 1;
            for (int z = a + 1; z < b; z++)
            {
                cells[(hx0, wy, z)] = t.Glass;
                cells[(hx1, wy, z)] = t.Glass;
            }
            for (int x = hx0 + 1; x < hx1; x++) cells[(x, wy, b)] = t.Glass;

            // 戸口。後面の中央に幅1・高さ2で開ける。上の層へは外の階段で上がる
            // 形にせず、実船と同じく各層の後面から出入りする。
            int door = (hx0 + hx1) / 2;
            cells.Remove((door, lo + 1, a));
            cells.Remove((door, lo + 2, a));
        }

        // 外の階段。後面の戸口の脇へ層ごとに1段ずつ。実船の船橋楼にも外舷梯子が付く。
        int sx = hx0;
        for (int d = 1; d < top.HouseDecks; d++)
        {
            int y = baseY + d * HouseFloorH;
            for (int k = 0; k < HouseFloorH; k++)
                cells[(sx, y - HouseFloorH + k, Math.Max(1, z0 - 1))] = t.Fitting;
        }

        BuildFunnel(cells, f, top, t, (hx0 + hx1) / 2, z0, z1, baseY);
    }

    // 煙突。船橋楼の後ろ半分から立ち、頂は屋根より上へ出す。
    // 直径3.7m級なので3マス角、高さは実船の水面上17m級に合わせて屋根＋4。
    private static void BuildFunnel(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t,
        int cx, int z0, int z1, int baseY)
    {
        if (top.Funnel <= 0) return;

        int roofY = baseY + top.HouseDecks * HouseFloorH - 1;
        int topY = roofY + top.Funnel;
        int fz = z0 + (z1 - z0) / 3;   // 後ろ寄り（船首が +z なので z の小さい側）

        for (int y = baseY; y <= topY; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int z = fz - 1; z <= fz + 1; z++)
                {
                    bool inner = x == cx && z == fz;
                    if (inner)
                    {
                        // 煙路。頂は開けたままにして煙の抜ける口にする。
                        if (y >= roofY) cells.Remove((x, y, z));
                        continue;
                    }
                    cells[(x, y, z)] = t.Funnel;
                }
    }
}
