using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 船体の構造材のうち、甲板より下に通るもの。貫通横梁と中心線舵。
// 船楼は HullExpander.Castle.cs、艤装は HullExpander.Rig.cs にある。
// もとは Castle.cs に同居していたが、1ファイル9KBの目安を超えたので分けた。
//
// 実物の根拠（コグ船 / ブレーメン・コグ 1380年）:
//   外板を貫いて横梁の木口が舷側の外へ突き出す。間隔4マスの貫通横梁として
//   甲板の1段下へ通す。
//   舵は船尾材に付く中心線舵。舵板は水線下の2つの舵金で吊られ、舵頭が立ち上がり、
//   舵柄が船内へ伸びる。舵柄は船楼甲板より下を通り、操舵手はその下に立つ。
public static partial class HullExpander
{
    // 舵柄の高さ。船尾の甲板の1マス上。船楼甲板より下なので、実船と同じく
    // 舵柄は船楼の下を通る。ここを舷墻の天端より上にすると、船尾は舷が細って
    // 舷墻が立たないため受けが無く、空中に浮く。
    // Castle.cs の BuildCastle も同じ値で妻面へ舵柄の口を開けるので、
    // 分けたあとも同じ partial クラスに置いて式を1か所に保つ。
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
}
