using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 帆と帆桁。横帆（square）と縦帆（ガフ帆）の2通り。
// マストの柱そのものは HullExpander.Rig.cs が最後に上書きで立てる。
// Rig.cs が8.9KBになり足す余地がなくなったので帆だけを分けた。
//
// 実物の根拠:
//   横帆は帆桁を舷と直角に吊り、帆はその下へ広がる。帆桁は船体幅より長く、
//   カティサークの主檣下桁は23.8mで型幅10.97mの倍以上ある。
//   縦帆（ガフ帆）は帆をマストの後ろへ張る。下辺をブーム、上辺をガフが持ち、
//   ガフは前端（スロート＝マスト側）より後端（ピーク）が高い。実船のピーク角は
//   水平から30〜45度で、帆は長方形ではなく後ろへ行くほど背の高い四角形になる。
//   ブルーノーズ（1921）は主ブーム24.7m・主帆386m²、前ブーム9.9m。
//   前檣の帆は後ろの主檣に当たらない長さで切る。前ブーム9.9mに対して本生成器の
//   マスト間隔は15m級なので、帆の長さは間隔の2/3・空きは1/3が実物の比になる。
//   ブームの下は人が通るので、甲板から2マス以上の空きを取る。
public static partial class HullExpander
{
    // 最後尾のマストの帆を船尾材の手前で止めるマス数。z=0〜1 には中心線舵の舵柄が
    // 通り、その後ろの z=-1 に舵頭が立つので、そこへ帆を掛けると舵とぶつかる。
    // 実船の主ブームは船尾から張り出すが、外寸と生成物を食い違わせないため
    // 船尾材の内側で切る。
    private const int SternSailClear = 2;
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

    // 縦帆（ガフ帆）。帆はマストの後ろ（船尾側）へ張る。板は中心線の列に立つので
    // 舷の外へは出ない。
    private static void BuildForeAftSail(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t, int index, int z, int mastTop)
    {
        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;

        // 帆の後端。後ろにマストがあるときは2本の間隔の1/3を空けて止める。
        // 隣のマスまで伸ばすと帆が後ろのマストへ張り付く。最後尾の帆は舵柄と
        // 舵頭の場所を空けて船尾材の手前で止める。
        int za = index > 0
            ? Math.Max(top.MastZs[index - 1] + Math.Max(2, (z - top.MastZs[index - 1]) / 3),
                       z - top.SailW)
            : Math.Max(SternSailClear, z - top.SailW);
        if (za > z - 1) return;                    // 張る余地がない

        // ブームの高さ。張る範囲すべての甲板を見て決める。マストの位置だけで決めると、
        // シアで持ち上がる船尾側の甲板・舷墻・舵柄へブームが食い込む。
        // 舷墻の天端より2マス上なので、下は人が通れる。
        int boomY = 0;
        for (int zz = za; zz <= z; zz++)
            boomY = Math.Max(boomY, f.DeckY(zz) + f.Bulwark + 2);

        // スロート（マスト側のガフの前端）。マスト頂より下に収める。
        int throatY = Math.Min(boomY + top.SailH, mastTop - 1);
        if (throatY - boomY < 2) return;           // 帆を張る高さが取れない

        // ピーク（ガフの後端）の持ち上げ。1 station につき1マスを超えると桁が
        // 飛び石になるので、上限は張る長さと同じにする。マスト頂も超えない。
        int span = z - 1 - za;
        int rise = Math.Min(span, mastTop - throatY);

        int prevY = int.MinValue;
        for (int zz = z - 1; zz >= za; zz--)
        {
            int gy = span > 0
                ? throatY + (int)Math.Round(rise * (double)(z - 1 - zz) / span)
                : throatY;

            // 段差の下も桁材で埋める。1マス上がるところで角しか接しない飛び石に
            // なるのを防ぐ。
            int gy0 = prevY != int.MinValue && gy > prevY ? prevY : gy;
            prevY = gy;

            for (int x = cx0; x <= cx1; x++)
            {
                PutSpar(cells, props, (x, boomY, zz), t.Mast, "z");
                for (int y = gy0; y <= gy; y++) PutSpar(cells, props, (x, y, zz), t.Mast, "z");
                for (int y = boomY + 1; y < gy0; y++) cells[(x, y, zz)] = t.Sail;
            }
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
