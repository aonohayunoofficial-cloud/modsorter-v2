using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 砲門。舷側の外板に1マスの口を開け、その1マス内側へ砲身を寝かせる。
//
// 実物の根拠:
//   ヴィクトリー（1765・104門）は砲甲板長186ft＝56.7m・型幅51ft10in＝15.80m。
//   下甲板30門＝片舷15門なので、砲門の中心間は186ft÷15＝12.4ft＝3.8m。
//   下甲板の砲門は水面上4ft9in＝1.4mと低く、荒天では開けられなかった。
//   18ポンド砲の砲門は幅2ft10in＝0.86m・高さ2ft6in＝0.76mなので1マスに収まる。
//   段どうしの上下の間隔は甲板の階高そのままで6〜7ft＝2m級。
//   フリゲート（レダ級トリンコマリー 1817）は砲甲板長150ft4in＝45.8m・
//   型幅39ft11in＝12.2mで18ポンド砲28門＝片舷14門。砲甲板が水面から高いのが
//   フリゲートの利点で、下段の砲門でも水面上2.4m級を確保する。
public static partial class HullExpander
{
    // 段どうしの上下の間隔。階高2m級なので2マス。口が1マスなので段の間には
    // 外板が1列残り、口が縦につながらない。
    private const int GunRowStep = 2;

    private static void BuildGunPorts(
        Dictionary<(int x, int y, int z), string> cells, Props props,
        Form f, Top top, TopPalette t)
    {
        if (top.GunRows <= 0 || top.GunStep < 2) return;

        for (int r = 0; r < top.GunRows; r++)
        {
            int y = f.WL + top.GunBase + r * GunRowStep;

            for (int z = top.GunStep; z < f.L - 1; z += top.GunStep)
            {
                // 甲板のすぐ下には外板を1列残す。残さないと口が舷縁まで抜けて
                // 切り欠きになり、舷墻の受けも無くなる。
                if (y + 1 >= f.DeckY(z)) continue;
                if (y <= f.BottomY(z)) continue;

                f.Span(f.HalfAt(z, y), out int x0, out int x1);
                // 舷が寄る船首・船尾には開けない。左右の口と砲身で4列を使うので、
                // 幅5マス未満だと左右の口が中心でぶつかって船体に穴が開く。
                if (x1 - x0 < 4) continue;

                // 口。外板を取り除く。
                cells.Remove((x0, y, z));
                cells.Remove((x1, y, z));
                props.Remove((x0, y, z));
                props.Remove((x1, y, z));

                // 砲身。口の1マス内側へ寝かせるので、外から覗くと砲口が見える。
                // 実船の砲門は肋骨と肋骨のあいだに開けるが、ここは間隔が合った
                // ところでフレームを砲身で置き換える。
                PutSpar(cells, props, (x0 + 1, y, z), t.Fitting, "x");
                PutSpar(cells, props, (x1 - 1, y, z), t.Fitting, "x");
            }
        }
    }
}
