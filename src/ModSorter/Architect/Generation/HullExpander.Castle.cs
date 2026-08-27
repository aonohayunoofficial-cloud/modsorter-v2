using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 貫通横梁・中心線舵・船楼の組み立て。艤装は HullExpander.Rig.cs にある。
//
// 実物の根拠（コグ船 / ブレーメン・コグ 1380年）:
//   全長23.23m・最大幅7.62m、船体中央で竜骨から舷縁まで4.26m。喫水2.25mで
//   残る乾舷2m、貨物84t。外板を貫いて横梁の木口が舷側の外へ突き出す。
//   舵は船尾材に付く中心線舵。舵板は水線下の2つの舵金で吊られ、舵頭が立ち上がり、
//   舵柄が船内へ伸びる。
//   船尾の船楼は「甲板」であって塊ではない。船楼甲板の下が船室（両舷に長椅子）で、
//   舵柄はその下を通る。操舵手は船楼甲板の下に立つので外が見えず、船長が船楼の
//   上に立って指示した。船楼と巻き上げ機を含めた全高は7.02m。
//   初期のコグ船は船首楼を持たない。盾掛けは持たない。
public static partial class HullExpander
{
    // station z の甲板の幅（マス）と左右端。舷墻・貫通横梁は3マス以上にしか載らない。
    private static int DeckSpanAt(Form f, int z, out int x0, out int x1)
    {
        f.Span(f.HalfAt(z, f.DeckY(z)), out x0, out x1);
        return x1 - x0 + 1;
    }

    // 舵柄の高さ。船尾の甲板の1マス上。船楼甲板より下なので、実船と同じく
    // 舵柄は船楼の下を通る。ここを舷墻の天端より上にすると、船尾は舷が細って
    // 舷墻が立たないため受けが無く、空中に浮く。
    private static int TillerY(Form f) => f.DeckY(0) + 1;

    // 貫通横梁。甲板のすぐ下に通し、木口を舷側の外へ1マスずつ出す。
    //
    // 木口の x は「梁を通す高さ y の半幅」で決める。そこが外板の実際の位置なので
    // 木口（x0-1 / x1+1）が必ず外板と接する。甲板の半幅を基準にすると、フレアの
    // 付いた船では甲板のほうが外側にあるため、梁の高さでは外板から離れて浮く。
    private static void BuildBeams(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.BeamStep < 2) return;

        for (int z = top.BeamStep; z < f.L - 1; z += top.BeamStep)
        {
            int y = f.DeckY(z) - 1;
            if (y < 1) continue;

            f.Span(f.HalfAt(z, y), out int x0, out int x1);
            if (x1 - x0 < 2) continue;

            // 外板の位置も梁材で置き換えて、板を貫いた形にする。
            cells[(x0, y, z)] = t.Fitting;
            cells[(x1, y, z)] = t.Fitting;
            cells[(x0 - 1, y, z)] = t.Fitting;
            cells[(x1 + 1, y, z)] = t.Fitting;
        }
    }

    // 中心線舵。船尾材（z=0 側）の後ろに舵板を吊り、舵頭を甲板の高さまで立ち上げ、
    // そこから舵柄を船内へ伸ばす。z=-1 を使うので奥行きが1増える。
    // 負座標は Normalize が 0 起点へ寄せる。
    private static void BuildSternRudder(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (!top.SternRudder) return;

        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int tiller = TillerY(f);

        // 舵板＋舵頭。竜骨の下端から舵柄の高さまで1本で通すので途切れない。
        for (int y = tiller; y >= -f.KeelDepth; y--)
            for (int x = cx0; x <= cx1; x++) cells[(x, y, -1)] = t.Fitting;

        // 舵柄。舵頭から船内へ2マス。船楼の支柱（中央の station）とぶつからない長さ。
        int reach = Math.Min(2, f.L);
        for (int z = 0; z < reach; z++)
            for (int x = cx0; x <= cx1; x++) cells[(x, tiller, z)] = t.Fitting;
    }

    private static void BuildCastles(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.CastleAft > 0) BuildCastle(cells, f, top, t, true, top.CastleAft);
        if (top.CastleFore > 0) BuildCastle(cells, f, top, t, false, top.CastleFore);
    }

    // 船楼1基。船尾（船首）の上へ箱として載せる。
    //
    // station ごとの甲板幅へ合わせると、舷が細る船尾は幅2マスまで縮むので側壁が
    // 立たず、柱と床だけの骨組みになる。実船の船楼は船体の細りに追従せず、幅
    // いっぱいの箱が船尾材の上へ張り出して載る。そこで範囲内でいちばん広い甲板幅を
    // 箱の平面形として全 station に使い、細るぶんは張り出しにする。張り出しの下は
    // 塞がないので、そこを舵柄が通り、細い舵板が下へ下りる形になる。
    //
    // 箱の下端も全長で揃える。station ごとに甲板の高さへ合わせると裾が段々になって
    // 箱に見えない。範囲の内側の端（船体中央を向く側）の甲板を基準にする。
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

        // 箱の平面形。いちばん広い station では側壁が甲板の縁に立ち、
        // そこから船尾側は張り出しになる。
        int bx0 = int.MaxValue, bx1 = int.MinValue;
        foreach (int z in zs)
        {
            DeckSpanAt(f, z, out int a, out int b);
            if (a < bx0) bx0 = a;
            if (b > bx1) bx1 = b;
        }
        if (bx1 - bx0 < 2) return;

        int last = zs.Count - 1;

        // 箱の下端。船体中央を向く端の甲板の1マス上から積む。
        int baseY = f.DeckY(zs[last]) + 1;

        // 船楼甲板の高さは下端から数える。Extent 側（CastleY）と同じ式なので
        // 外寸表示と食い違わない。舷墻の天端より下へは下げない。
        int floorY = CastleFloorY(f, zs[last], height);
        int railY = floorY + 1;

        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int tiller = TillerY(f);
        bool slot = aft && top.SternRudder;

        for (int i = 0; i <= last; i++)
        {
            int z = zs[i];
            bool cap = i == 0 || i == last;
            DeckSpanAt(f, z, out int dx0, out int dx1);

            // 1) 張り出しの底。船尾は舷が細って甲板が幅2マスまで縮むので、箱の幅で
            //    載せると船体の甲板が無いところが下から素通しになる。実船でも張り出しは
            //    梁の上に床板を張って塞いでいる。船体の甲板が無いぶんだけ底を張る。
            for (int x = bx0; x <= bx1; x++)
            {
                if (x >= dx0 && x <= dx1) continue;   // ここは船体の甲板がある
                cells[(x, baseY - 1, z)] = t.Castle;
            }

            // 2) 妻面と側壁。船楼甲板より下は操舵手の入る空所で、塞がない。
            for (int y = baseY; y < floorY; y++)
            {
                if (cap)
                {
                    for (int x = bx0; x <= bx1; x++)
                    {
                        // 船尾の妻面は舵柄の通る口を開ける。
                        if (slot && i == 0 && y == tiller && x >= cx0 && x <= cx1) continue;
                        // 船体中央を向く妻面は甲板から2マスぶんの出入口を開ける。
                        if (i == last && y <= baseY + 1 && x >= cx0 && x <= cx1) continue;
                        cells[(x, y, z)] = t.Castle;
                    }
                    continue;
                }

                cells[(bx0, y, z)] = t.Castle;
                cells[(bx1, y, z)] = t.Castle;
            }

            // 3) 船楼甲板。箱の全面に張る。上書きで置くので、舷墻や妻面と
            //    重なっても必ず床が通る。
            for (int x = bx0; x <= bx1; x++) cells[(x, floorY, z)] = t.Castle;

            // 4) 手すり。妻面のところは全幅、途中は左右の縁だけ。
            if (cap) for (int x = bx0; x <= bx1; x++) cells[(x, railY, z)] = t.Castle;
            else { cells[(bx0, railY, z)] = t.Castle; cells[(bx1, railY, z)] = t.Castle; }
        }

        // 4) 巻き上げ機。船楼甲板の中央に立てる。縦の丸太なので軸の指定は要らない。
        int zc = zs[zs.Count / 2];
        for (int x = cx0; x <= cx1; x++) cells[(x, railY, zc)] = t.Fitting;
    }

    // 船楼甲板の高さ。zInner は船楼のうち船体中央を向く端の station。
    // 床の下に人の入る空所を必ず1マス残し、舷墻の天端より下へは張らない。
    // Top の TopY もこれを通すので、UI の外寸と生成物が食い違わない。
    private static int CastleFloorY(Form f, int zInner, int height)
    {
        int baseY = f.DeckY(zInner) + 1;
        int floorY = baseY + Math.Max(1, height - 1);
        int minY = f.DeckY(zInner) + f.Bulwark + 1;
        return Math.Max(floorY, minY);
    }
}
