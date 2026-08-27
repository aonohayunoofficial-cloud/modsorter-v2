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

    // 船楼1基。船楼甲板を張り、その下を船室にする。
    //
    // 舷が細る station を切り捨てると船楼が船尾から前へ寄ってしまうので、甲板幅
    // 2マスまで含めて船尾材まで届かせる。ただし幅4マス未満では左右の壁が隣り合って
    // 中身の無い塊になるため、そこには壁を立てず、中央の1本だけ支柱を立てて甲板を
    // 受ける。実船でも細る船尾は船室にせず柱で船楼甲板を受けている。
    private static void BuildCastle(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, Top top, TopPalette t, bool aft, int height)
    {
        int end = aft ? 0 : f.L - 1;
        int step = aft ? 1 : -1;

        var zs = new List<int>();
        for (int z = end; z >= 0 && z < f.L && zs.Count < top.CastleLen; z += step)
        {
            if (DeckSpanAt(f, z, out _, out _) < 2) break;   // 竜骨1列まで細ったら打ち切る
            zs.Add(z);
        }
        if (zs.Count < 2) return;

        // 船楼甲板と手すりの高さ。Top の Extent と同じ式なので外寸表示と食い違わない。
        int railY = f.DeckY(end) + f.Bulwark + height;
        int floorY = railY - 1;

        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int tiller = TillerY(f);
        bool slot = aft && top.SternRudder;
        int mid = zs.Count / 2;

        bool prevWide = false;
        int prevX0 = 0, prevX1 = 0;

        for (int i = 0; i < zs.Count; i++)
        {
            int z = zs[i];
            int w = DeckSpanAt(f, z, out int x0, out int x1);
            int y0 = f.DeckY(z) + 1;   // 甲板の上から積む。舷墻と同じ列なので継ぎ目にならない
            bool cap = i == 0 || i == zs.Count - 1;
            bool wide = w >= 4;

            // 1) 妻面・船室の側壁・支柱。
            for (int y = y0; y <= floorY - 1; y++)
            {
                if (cap)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        // 船尾側の妻面は舵柄の通る口を開ける。
                        if (slot && i == 0 && y == tiller && x >= cx0 && x <= cx1) continue;
                        // 船体中央を向く妻面は船室の出入口を開ける（甲板から2マス）。
                        if (i == zs.Count - 1 && y <= y0 + 1 && x >= cx0 && x <= cx1) continue;
                        cells[(x, y, z)] = t.Castle;
                    }
                    continue;
                }

                if (wide)
                {
                    cells[(x0, y, z)] = t.Castle;
                    cells[(x1, y, z)] = t.Castle;

                    // 前の station との幅の差を埋める。埋めないと側壁が段差で切れて穴になる。
                    if (!prevWide) continue;
                    for (int x = Math.Min(x0, prevX0); x <= Math.Max(x0, prevX0); x++)
                        cells[(x, y, z)] = t.Castle;
                    for (int x = Math.Min(x1, prevX1); x <= Math.Max(x1, prevX1); x++)
                        cells[(x, y, z)] = t.Castle;
                }
                else if (i == mid)
                {
                    // 船室にならない幅。船楼甲板を受ける支柱だけ立てる。
                    for (int x = x0; x <= x1; x++) cells[(x, y, z)] = t.Castle;
                }
            }

            prevWide = wide;
            prevX0 = x0;
            prevX1 = x1;

            // 2) 船楼甲板。妻面と支柱で受けた床を全幅に張る。
            for (int x = x0; x <= x1; x++) cells[(x, floorY, z)] = t.Castle;

            // 3) 手すり。舷側の縁に立て、妻面のところは全幅で塞ぐ。
            if (railY <= floorY) continue;
            if (cap) for (int x = x0; x <= x1; x++) cells[(x, railY, z)] = t.Castle;
            else { cells[(x0, railY, z)] = t.Castle; cells[(x1, railY, z)] = t.Castle; }
        }

        // 4) 巻き上げ機。船楼甲板の中央に立てる。縦の丸太なので軸の指定は要らない。
        if (railY > floorY)
        {
            int zc = zs[mid];
            DeckSpanAt(f, zc, out int a, out int b);
            for (int x = Math.Max(a, cx0); x <= Math.Min(b, cx1); x++)
                cells[(x, railY, zc)] = t.Fitting;
        }
    }
}
