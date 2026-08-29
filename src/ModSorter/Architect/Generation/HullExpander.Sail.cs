using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 帆と帆桁。横帆（square）と縦帆（fore-and-aft）の2通り。
// マストの柱そのものは HullExpander.Rig.cs が最後に上書きで立てる。
// Rig.cs が8.9KBになり足す余地がなくなったので帆だけを分けた。
//
// 実物の根拠:
//   横帆は帆桁を舷と直角に吊り、帆はその下へ広がる。帆桁は船体幅より長く、
//   カティサークの主檣下桁は23.8mで型幅10.97mの倍以上ある。
//   縦帆（ガフ帆）は帆をマストの後ろへ張り、下辺をブーム・上辺をガフが持つ。
//   どちらも前後方向に寝る。ブルーノーズの主ブームは24.7m・前ブームは9.9mで、
//   前檣の帆は後ろの主檣に当たらない長さで切る。ブームの下は人が通るので、
//   甲板から2マス以上の空きを取る。
public static partial class HullExpander
{
    private static void BuildSail(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t, int index, int z, int baseY, int mastTop)
    {
        if (top.Sail == "none") return;
        if (top.Sail == "fore") BuildForeAftSail(cells, props, f, top, t, index, z, mastTop);
        else BuildSquareSail(cells, props, f, top, t, baseY, z, mastTop);
    }

    // 横帆。帆桁をマストの上端の1マス下へ横に渡し、帆をその下へ吊る。
    // 帆の下端は舷墻の天端より下へ入れない。帆桁の位置（マスト高で決まる）に対して
    // 帆の丈が長いと下端が甲板へ食い込むので、帆桁を上げるのではなく入りきらない
    // ぶんだけ丈を切り詰める。実船でも帆の丈はマスト高と舷墻の高さで決まる。
    private static void BuildSquareSail(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t, int baseY, int z, int mastTop)
    {
        int cx0 = (f.B - 1) / 2;
        int yardY = Math.Max(baseY, mastTop - 1);
        int xa = cx0 - (top.SailW - 1) / 2;
        int xb = xa + top.SailW - 1;

        for (int x = xa; x <= xb; x++) PutSpar(cells, props, (x, yardY, z), t.Mast, "x");

        int sailTop = yardY - 1;
        int sailFloor = f.DeckY(z) + f.Bulwark + 1;
        int room = sailTop - sailFloor + 1;
        int rows = top.Sail == "furled" ? 1 : Math.Min(top.SailH, room);

        for (int k = 0; k < rows; k++)
        {
            int y = sailTop - k;
            if (y < baseY) break;
            for (int x = xa; x <= xb; x++) cells[(x, y, z)] = t.Sail;
        }
    }

    // 縦帆。帆はマストの後ろ（船尾側）へ張る。板は中心線の列に立つので
    // 舷の外へは出ない。ブームの後端は船尾材で止め、2本目以降のマストでは
    // 後ろのマストの手前で止める。
    private static void BuildForeAftSail(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t, int index, int z, int mastTop)
    {
        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;

        int aft = index > 0 ? top.MastZs[index - 1] + 1 : 0;
        int za = Math.Max(aft, z - top.SailW);
        if (za > z - 1) return;                    // 張る余地がない

        // ブームの高さ。舷墻の天端より2マス上へ置き、下を人が通れるようにする。
        int boomY = f.DeckY(z) + f.Bulwark + 2;
        int gaffY = Math.Min(boomY + top.SailH, mastTop - 1);
        if (gaffY - boomY < 2) return;             // 帆を張る高さが取れない

        for (int zz = za; zz <= z - 1; zz++)
            for (int x = cx0; x <= cx1; x++)
            {
                PutSpar(cells, props, (x, boomY, zz), t.Mast, "z");
                PutSpar(cells, props, (x, gaffY, zz), t.Mast, "z");
                for (int y = boomY + 1; y < gaffY; y++) cells[(x, y, zz)] = t.Sail;
            }
    }

    // 寝た帆桁・ブーム・ガフ。丸太は axis を持つので Rotate が向きを追従させる。
    private static void PutSpar(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        (int x, int y, int z) key, string id, string axis)
    {
        cells[key] = id;
        props[key] = new Dictionary<string, string> { ["axis"] = axis };
    }
}
