using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 船楼（船尾楼・船首楼）の組み立て。貫通横梁・中心線舵は HullExpander.Beam.cs、
// 艤装は HullExpander.Rig.cs、船体中央のデッキハウスは HullExpander.House.cs にある。
//
// 実物の根拠:
//   コグ船（ブレーメン・コグ 1380年）は全長23.23m・最大幅7.62m、船体中央で竜骨から
//   舷縁まで4.26m。喫水2.25mで残る乾舷2m、貨物84t。船尾の船楼は「甲板」であって
//   塊ではない。船楼甲板の下が船室（両舷に長椅子）で、舵柄はその下を通る。操舵手は
//   船楼甲板の下に立つので外が見えず、船長が船楼の上に立って指示した。船楼と
//   巻き上げ機を含めた全高は7.02m。初期のコグ船は船首楼を持たない。
//   リバティ船（EC2型 1941年）の船首楼・船尾楼は三島型の一段高い甲板で、外板が
//   そのまま船楼甲板まで立ち上がる。
//
//   舷側は船体の輪郭に沿う。船首・船尾では甲板が細るので船楼の平面形も細り、
//   舷から横へ張り出すことはない。コグ船の船尾楼が張り出すのは船尾方向だけ。
//   範囲内の最大幅で1つの箱を作ると、細る船首・船尾で舷の外へ数マス出て、船体とは
//   縁の切れた箱が載った形になる。そこで平面形は station ごとの甲板幅で決める。
public static partial class HullExpander
{
    // station z の甲板の幅（マス）と左右端。舷墻・貫通横梁は3マス以上にしか載らない。
    private static int DeckSpanAt(Form f, int z, out int x0, out int x1)
    {
        f.Span(f.HalfAt(z, f.DeckY(z)), out x0, out x1);
        return x1 - x0 + 1;
    }

    private static void BuildCastles(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.CastleAft > 0) BuildCastle(cells, f, top, t, true, top.CastleAft);
        if (top.CastleFore > 0) BuildCastle(cells, f, top, t, false, top.CastleFore);
    }

    // 船楼1基。station ごとの甲板の縁から舷側を船楼甲板の高さまで立ち上げ、
    // その上に甲板と手すりを張る。
    //
    // 甲板が4マス未満へ細る station は、側壁の2列のあいだに人の入る空所が残らない
    // ので、全幅を詰めて船首材・船尾材の塊にする。実船でも舷が合わさる先端は塊で、
    // 船室の空所はそこまで届かない。
    //
    // 船楼甲板の高さだけは全長で揃える。station ごとの甲板へ合わせると天端が
    // 段々になって甲板に見えない。
    private static void BuildCastle(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, Top top, TopPalette t, bool aft, int height)
    {
        int end = aft ? 0 : f.L - 1;
        int step = aft ? 1 : -1;

        // 幅で切り捨てない。船尾材まで素直に指定の長さぶんを取る。
        var zs = new List<int>();
        for (int z = end; z >= 0 && z < f.L && zs.Count < top.CastleLen; z += step) zs.Add(z);
        if (zs.Count < 2) return;

        // 範囲のどこも3マス未満の小舟には船楼が載らない。
        int widest = 0;
        foreach (int z in zs) widest = Math.Max(widest, DeckSpanAt(f, z, out _, out _));
        if (widest < 3) return;

        int last = zs.Count - 1;

        // 船楼甲板と手すりの高さ。Extent 側（Top の TopY）も同じ CastleFloorY を
        // 通るので、外寸表示と生成物が食い違わない。
        int floorY = CastleFloorY(f, zs[last], height);
        int railY = floorY + 1;

        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int tiller = TillerY(f);
        bool slot = aft && top.SternRudder;

        for (int i = 0; i <= last; i++)
        {
            int z = zs[i];
            int w = DeckSpanAt(f, z, out int dx0, out int dx1);
            int baseY = f.DeckY(z) + 1;

            // 全幅を詰める station。船体の端（船首材・船尾材）、船体中央を向く端
            //（隔壁）、甲板が4マス未満へ細るところ。
            bool solid = i == 0 || i == last || w < 4;

            for (int y = baseY; y < floorY; y++)
                for (int x = dx0; x <= dx1; x++)
                {
                    if (!solid && x != dx0 && x != dx1) continue;   // 途中は舷側だけ

                    // 舵柄の通る口。舵柄は z=0〜1 を走るので両方の station で開ける。
                    if (slot && y == tiller && z < 2 && x >= cx0 && x <= cx1) continue;

                    // 船体中央を向く隔壁は甲板から2マスぶんの戸口を開ける。
                    if (i == last && y <= baseY + 1 && x >= cx0 && x <= cx1) continue;

                    cells[(x, y, z)] = t.Castle;
                }

            // 船楼甲板。上書きで置くので、舷墻や隔壁と重なっても必ず床が通る。
            for (int x = dx0; x <= dx1; x++) cells[(x, floorY, z)] = t.Castle;

            // 手すり。船体中央を向く端は全幅（船楼の切れ目の手すり）、
            // 途中と船体の端は左右の縁だけ。
            if (i == last) for (int x = dx0; x <= dx1; x++) cells[(x, railY, z)] = t.Castle;
            else { cells[(dx0, railY, z)] = t.Castle; cells[(dx1, railY, z)] = t.Castle; }
        }

        // 巻き上げ機。船楼甲板の中央に立てる。縦の丸太なので軸の指定は要らない。
        int zc = zs[zs.Count / 2];
        for (int x = cx0; x <= cx1; x++) cells[(x, railY, zc)] = t.Fitting;
    }

    // 船楼甲板の高さ。zInner は船楼のうち船体中央を向く端の station。
    // 床の下の空所は必ず2マス残す。1マスだと人が入れないうえ、隔壁が1段しか
    // 積まれないので、開けているはずの戸口が1マスの隙間にしかならない。
    // 実物の船尾楼も甲板の下が船室で、前側の隔壁に戸口が開く。
    // 舷墻の天端より下へは張らない。
    //
    // 船首・船尾はシアで甲板が上がるので、内側の端だけで高さを決めると、シアの
    // 大きい船種では船楼甲板が船体の甲板より下へ来て埋もれる。範囲内でいちばん高い
    // 甲板の1マス上までは必ず上げる。範囲は zInner が船体中央より前か後かで決まる
    // （呼び出し側は船尾なら 0〜zInner、船首なら zInner〜L-1 を組む）。
    private static int CastleFloorY(Form f, int zInner, int height)
    {
        int floorY = f.DeckY(zInner) + 1 + Math.Max(2, height - 1);
        floorY = Math.Max(floorY, f.DeckY(zInner) + f.Bulwark + 1);

        bool aft = zInner < f.L / 2;
        int za = Math.Clamp(aft ? 0 : zInner, 0, f.L - 1);
        int zb = Math.Clamp(aft ? zInner : f.L - 1, 0, f.L - 1);
        for (int z = za; z <= zb; z++) floorY = Math.Max(floorY, f.DeckY(z) + 1);

        return floorY;
    }
}
