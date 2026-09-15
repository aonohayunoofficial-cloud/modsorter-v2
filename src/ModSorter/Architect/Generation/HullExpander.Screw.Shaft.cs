using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 推進器の軸・シャフトストラット・舵。寸法（ScrewFit）とプロペラは
// HullExpander.Screw.cs にある。Screw.cs が8,319バイトで、軸の支持を直すと
// 1ファイル9KBの目安を超えるので分けた。
//
// 実物の根拠:
//   軸の傾斜は滑走艇で船底に対し10〜13度（Seaboard Marine の据付基準）。
//   1マス=1mでは4マスで1マス上げる約14度がいちばん近い刻み。
//   軸が船底から離れて走る区間は、実艇ではシャフトストラット（軸受の支柱）が
//   船底から下りて軸を受ける。舵はプロペラの後ろに立ち、舵頭が船体へ入る。
//
// 深いVの滑走艇（バートラム46・リバなど）は基線で船底が竜骨の1列に落ちるので、
// 軸の高さの真上に船体が無い station が続く。船底線だけで止める判定にすると、
// プロペラと軸が船体と面で接しないまま水中に浮く。船体の有無は Inside
//（station と高さでの半幅）で見て、届かない区間は支柱で船底へつなぐ。
public static partial class HullExpander
{
    // 軸。プロペラから船首側へ、4マスで1マス上がる勾配で走らせる。船体へ届いた
    // ところが船内への貫通口（シャフトログ）で、そこで止める。届かないまま前端まで
    // 来たら支柱で船底へ吊る。どちらでも軸は船体からつながった1本になる。
    private static void PutShaft(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, ScrewFit s, TopPalette t, int xc)
    {
        int y = s.ShaftY;
        int lim = Math.Max(1, f.L / 2);
        int z = 1;
        bool hit = false;

        for (; z <= lim; z++)
        {
            if (Inside(f, xc, y, z)) { hit = true; break; }
            PutIfEmpty(cells, f, (xc, y, z), t.Fitting);

            if (z % 4 != 0) continue;
            if (Inside(f, xc, y + 1, z)) { hit = true; break; }

            // 折れ点。1段上げた先へ斜めに跳ぶと面で接しないので、同じ station で
            // 1マス上げてから前へ進む。
            y++;
            PutIfEmpty(cells, f, (xc, y, z), t.Fitting);
        }

        if (!hit) PutStrut(cells, f, t, xc, y, Math.Min(z, lim));
    }

    // シャフトストラット。軸の前端から真上へ、船体に当たるまで1列立てる。
    // すぐ上が船体なら軸はそこで受かっているので何も置かない。
    private static void PutStrut(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, TopPalette t, int xc, int y0, int z)
    {
        for (int y = y0 + 1; y <= f.DeckY(z); y++)
        {
            if (Inside(f, xc, y, z)) return;
            PutIfEmpty(cells, f, (xc, y, z), t.Fitting);
        }
    }

    // 舵。プロペラの後ろ（z=-1 から前後長ぶん）へ、下端をプロペラの下端へ合わせて
    // 立てる。上端は船尾の station でその x に船体がある高さまで伸ばし、舵頭を
    // 船体へ接続する。幅の細い船尾では中心から離れた軸の真上に外板が無いので、
    // この探索が要る。負座標は Normalize が 0 起点へ寄せる。
    private static void PutScrewRudder(
        Dictionary<(int x, int y, int z), string> cells,
        Form f, ScrewFit s, TopPalette t, int xc)
    {
        int top = RudderTopY(f, xc);

        for (int c = 1; c <= s.Chord; c++)
            for (int y = s.PropBottom; y <= top; y++)
                PutIfEmpty(cells, f, (xc, y, -c), t.Fitting);
    }

    private static int RudderTopY(Form f, int xc)
    {
        int b0 = f.BottomY(0);
        for (int y = b0; y <= f.DeckY(0); y++)
            if (Inside(f, xc, y, 0)) return y;
        return b0;
    }
}
