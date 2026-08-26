using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 貫通横梁・中心線舵・船楼の組み立て。艤装は HullExpander.Rig.cs にある。
//
// 実物の根拠（コグ船 / ブレーメン・コグ 1380年）:
//   全長23.27m・最大幅7.62m・舷側高4m級で、喫水2.25mのとき排水量139t。
//   外板を貫いて横梁の木口が舷側の外へ突き出す。これがコグ船の外見上の要点で、
//   外へ開いた高い舷側の形をこの梁で保つ。
//   舵は船尾材に付く中心線舵。1200年頃に側舵から置き換わった。
//   船尾に高い船楼を載せる。初期のコグ船は船首楼を持たない。盾掛けは持たない。
public static partial class HullExpander
{
    // 貫通横梁。甲板のすぐ下に通し、木口を舷側の外へ1マスずつ出す。
    // 舷が細る船首・船尾の端には通さない。
    private static void BuildBeams(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.BeamStep < 2) return;

        for (int z = top.BeamStep; z < f.L - 1; z += top.BeamStep)
        {
            int dk = f.DeckY(z);
            int y = dk - 1;
            if (y < 1) continue;

            f.Span(f.HalfAt(z, y), out int x0, out int x1);
            if (x1 - x0 < 2) continue;

            cells[(x0 - 1, y, z)] = t.Fitting;
            cells[(x1 + 1, y, z)] = t.Fitting;
        }
    }

    // 中心線舵。船尾材（z=0 側）の後ろへ舵板を吊り、舵頭を甲板の上へ出す。
    // z=-1 を使うので奥行きが1増える。負座標は Normalize が 0 起点へ寄せる。
    private static void BuildSternRudder(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (!top.SternRudder) return;

        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        int dk = f.DeckY(0);
        int lo = -f.KeelDepth;

        for (int y = dk; y >= lo; y--)
            for (int x = cx0; x <= cx1; x++) cells[(x, y, -1)] = t.Fitting;

        for (int x = cx0; x <= cx1; x++)
            cells[(x, dk + 1 + f.Bulwark, 0)] = t.Fitting;
    }

    private static void BuildCastles(
        Dictionary<(int x, int y, int z), string> cells, Form f, Top top, TopPalette t)
    {
        if (top.CastleAft > 0) BuildCastle(cells, f, top, t, 0, top.CastleAft);
        if (top.CastleFore > 0) BuildCastle(cells, f, top, t, f.L - 1, top.CastleFore);
    }

    // 船楼1基。end は船尾なら 0・船首なら L-1。舷側に沿って壁を立て、天端を張り、
    // 船体中央を向く妻面を塞ぐ。舷が細って幅3マスを割る station には載せない。
    private static void BuildCastle(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, Top top, TopPalette t, int end, int height)
    {
        bool aft = end == 0;
        for (int k = 0; k < top.CastleLen; k++)
        {
            int z = aft ? end + k : end - k;
            if (z < 0 || z >= f.L) break;

            int dk = f.DeckY(z);
            f.Span(f.HalfAt(z, dk), out int x0, out int x1);
            if (x1 - x0 < 2) continue;

            int y0 = dk + f.Bulwark + 1;
            bool gable = k == top.CastleLen - 1;   // 船体中央を向く妻面
            for (int j = 0; j < height; j++)
            {
                int y = y0 + j;
                if (gable || j == height - 1)
                {
                    for (int x = x0; x <= x1; x++) cells[(x, y, z)] = t.Castle;
                    continue;
                }
                cells[(x0, y, z)] = t.Castle;
                cells[(x1, y, z)] = t.Castle;
            }
        }
    }
}
