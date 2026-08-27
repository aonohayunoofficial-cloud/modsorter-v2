using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 貫通横梁・中心線舵・船楼の組み立て。艤装は HullExpander.Rig.cs にある。
//
// 実物の根拠（コグ船 / ブレーメン・コグ 1380年）:
//   全長23.27m・最大幅7.62m・舷側高4m級で、喫水2.25mのとき排水量139t。
//   外板を貫いて横梁の木口が舷側の外へ突き出す。これがコグ船の外見上の要点で、
//   外へ開いた高い舷側の形をこの梁で保つ。
//   舵は船尾材に付く中心線舵。1200年頃に側舵から置き換わった。舵頭が船尾材の
//   後ろを立ち上がり、その天端から舵柄が船内へ伸びる。
//   船尾に高い船楼を載せる。初期のコグ船は船首楼を持たない。盾掛けは持たない。
public static partial class HullExpander
{
    // 甲板の幅が3マス以上ある station か。舷墻・船楼・横梁はここにしか載らない。
    // BuildBareHull のブルワークと同じ条件に揃えてあるので、船楼の壁は必ず舷墻の
    // 天端に乗る。条件がずれると壁が舷墻から浮く。
    private static bool DeckSpan(Form f, int z, out int x0, out int x1)
    {
        f.Span(f.HalfAt(z, f.DeckY(z)), out x0, out x1);
        return x1 - x0 >= 2;
    }

    // 貫通横梁。甲板のすぐ下に通し、木口を舷側の外へ1マスずつ出す。
    //
    // 木口の x は「梁を通す高さ y の半幅」で決める。そこは外板の実際の位置なので、
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

    // 中心線舵。船尾材（z=0 側）の後ろへ舵板を吊り、舵頭を甲板の上へ立ち上げ、
    // その天端から舵柄を船内へ出す。z=-1 を使うので奥行きが1増える。
    // 負座標は Normalize が 0 起点へ寄せる。
    //
    // 舵柄だけを dk+1+Bulwark の高さへ置くと空中に浮く。船尾の station は舷が
    // 細って幅3マスに満たず、舷墻が立たないので下に受けが無いため。舵板から
    // 舵柄の高さまで z=-1 の列を途切れなく通して、舵頭でつなぐ。
    private static void BuildSternRudder(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (!top.SternRudder) return;

        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int dk = f.DeckY(0);
        int lo = -f.KeelDepth;
        int headY = dk + 1 + f.Bulwark;   // 舵柄の高さ＝舷墻の天端の1マス上

        // 舵板＋舵頭。下端（竜骨の下）から舵柄の高さまで1本で通す。
        for (int y = headY; y >= lo; y--)
            for (int x = cx0; x <= cx1; x++) cells[(x, y, -1)] = t.Fitting;

        // 舵柄。舵頭の天端から船内へ2マス伸ばす。舵頭と地続きなので浮かない。
        int reach = Math.Min(2, f.L);
        for (int z = 0; z < reach; z++)
            for (int x = cx0; x <= cx1; x++) cells[(x, headY, z)] = t.Fitting;
    }

    private static void BuildCastles(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.CastleAft > 0) BuildCastle(cells, f, top, t, 0, top.CastleAft);
        if (top.CastleFore > 0) BuildCastle(cells, f, top, t, f.L - 1, top.CastleFore);
    }

    // 船楼1基。end は船尾なら 0・船首なら L-1。
    //
    // 端から CastleLen 本を数えると、舷が細って幅3マスに満たない station が
    // 全部落ちて、残った1本が「妻面＝全幅」の分岐に入り、船体を横切る1枚壁になる。
    // そこで、載せられる station（甲板幅3マス以上）を端から探して CastleLen 本
    // 集め、その範囲の両端を妻面にする。2本に満たなければ載せない。
    private static void BuildCastle(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, Top top, TopPalette t, int end, int height)
    {
        int step = end == 0 ? 1 : -1;

        var zs = new List<int>();
        for (int z = end; z >= 0 && z < f.L; z += step)
        {
            if (!DeckSpan(f, z, out _, out _))
            {
                if (zs.Count == 0) continue;   // 端の細りぶんは飛ばして内側から始める
                break;                          // 途中で細るならそこで打ち切る
            }
            zs.Add(z);
            if (zs.Count >= top.CastleLen) break;
        }
        if (zs.Count < 2) return;   // 1本だけでは壁1枚になるので載せない

        int prevX0 = int.MinValue, prevX1 = int.MinValue;

        for (int i = 0; i < zs.Count; i++)
        {
            int z = zs[i];
            DeckSpan(f, z, out int x0, out int x1);

            int y0 = f.DeckY(z) + f.Bulwark + 1;   // 舷墻の天端の上から積む
            bool cap = i == 0 || i == zs.Count - 1;

            for (int j = 0; j < height; j++)
            {
                int y = y0 + j;

                // 妻面（範囲の両端）と天端は全幅を塞ぐ。
                if (cap || j == height - 1)
                {
                    for (int x = x0; x <= x1; x++) cells[(x, y, z)] = t.Castle;
                    continue;
                }

                cells[(x0, y, z)] = t.Castle;
                cells[(x1, y, z)] = t.Castle;

                // 前の station との幅の差を埋める。埋めないと舷側の壁が段差で切れる。
                if (prevX0 == int.MinValue) continue;
                for (int x = Math.Min(x0, prevX0); x <= Math.Max(x0, prevX0); x++)
                    cells[(x, y, z)] = t.Castle;
                for (int x = Math.Min(x1, prevX1); x <= Math.Max(x1, prevX1); x++)
                    cells[(x, y, z)] = t.Castle;
            }

            prevX0 = x0;
            prevX1 = x1;
        }
    }
}
