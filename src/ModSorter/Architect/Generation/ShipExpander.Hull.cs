using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// ShipExpander の共通骨格（partial）。船底・船体・甲板を作る。
// 船体は下すぼまり（船底を左右から絞る）＋船首テーパー（船首側を数マス絞って尖らせる）。
public static partial class ShipExpander
{
    // ===== 共通骨格: 船底・船体・甲板 =====
    // 下すぼまり（船底1〜2マスを左右から絞る）＋船首テーパー（船首側を絞って尖らせる）。
    // 甲板の高さ deckY を返す。deckY より下は中身も詰めず、外殻＋甲板だけ作る（軽量）。
    private static int BuildHull(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, string hull, string deck, bool bowNorth, string shipClass)
    {
        // 小型艇など極端に低い指定でも船体を作れるよう、内部的に高さの下限を設ける。
        h = Math.Max(h, 4);

        // 船体の高さ（喫水＋乾舷）。船種で少し変える。全高 h の半分前後を船体、上を上部構造物へ。
        int hullTop = Math.Max(1, h switch
        {
            <= 3 => 1,
            <= 6 => 2,
            _ => Math.Max(2, h / 2)
        });
        // 潜水艦は船体を全高近くまで使う（葉巻型）。
        if (shipClass == "submarine") hullTop = Math.Max(2, h - 1);
        int deckY = Math.Min(h - 1, hullTop); // 甲板の高さ

        // 船首テーパーの割合。貨物船・タンカーは箱型で平行部が長いので短く絞る。
        double bowFrac = (shipClass == "cargo" || shipClass == "liner") ? 0.22 : 0.4;
        // 船首テーパーの長さ（最低2）。貨物船は短く取り、幅を保つ区間を長くする。
        int bowLen = Clamp((int)Math.Round(d * bowFrac), 2, Math.Max(2, d - 1));
        // 船尾もわずかに絞る（約2割）。船首ほど鋭くはしない。
        int sternLen = Clamp((int)Math.Round(d * 0.2), 1, Math.Max(1, d - 1));
        // 先端に残す最小の幅（半幅）。1 なら先端幅1〜2マスまで尖る。
        int tipHalf = (w >= 5) ? 1 : 0;
        int maxHalf = Math.Max(0, (w - 1) / 2); // 中央での最大の半幅

        for (int z = 0; z < d; z++)
        {
            // 船首・船尾からの距離（bowNorth なら z が小さいほど船首）。
            int distFromBow = bowNorth ? z : (d - 1 - z);
            int distFromStern = bowNorth ? (d - 1 - z) : z;

            // その z 位置で許される半幅を決める。船首側は強く、船尾側は緩く絞る。
            int half = maxHalf;
            if (distFromBow < bowLen)
            {
                // 船首テーパー: 先端(dist=0)で tipHalf、bowLen で maxHalf に非線形で開く。
                // t=0..1（先端→内側）。二乗を使い、先端付近をより鋭く絞る。
                double t = (double)distFromBow / bowLen;
                double curved = t * t;
                int bowHalf = tipHalf + (int)Math.Round((maxHalf - tipHalf) * curved);
                half = Math.Min(half, bowHalf);
            }
            if (distFromStern < sternLen)
            {
                // 船尾テーパー: 端で maxHalf-1 程度まで軽く絞る（角を丸める）。
                double t = (double)distFromStern / sternLen;
                int sternHalf = Math.Max(0, maxHalf - 1) + (int)Math.Round(1 * t);
                half = Math.Min(half, Math.Min(maxHalf, sternHalf));
            }

            int cxLo = (w - 1) / 2;
            int cxHi = w / 2;
            int x0 = Clamp(cxLo - half, 0, w - 1);
            int x1 = Clamp(cxHi + half, 0, w - 1);
            if (x1 < x0) { x0 = x1 = w / 2; }

            for (int y = 0; y <= deckY; y++)
            {
                // 船底(y=0)は下すぼまりで左右をさらに1マス絞る（V/U字断面）。
                int shrink = (y == 0 && (x1 - x0) >= 2) ? 1 : 0;
                int sx0 = x0 + shrink;
                int sx1 = x1 - shrink;
                if (sx1 < sx0) { sx0 = sx1 = w / 2; }

                for (int x = sx0; x <= sx1; x++)
                {
                    bool isDeck = (y == deckY);
                    bool isShell =
                        x == sx0 || x == sx1 ||        // 舷側
                        z == 0 || z == d - 1 ||        // 船首・船尾端
                        y == 0;                        // 船底
                    if (isDeck)
                        cells[(x, y, z)] = deck;       // 甲板は全面
                    else if (isShell)
                        cells[(x, y, z)] = hull;       // 側面・船底の殻のみ（中は空洞）
                }
            }
        }

        return deckY;
    }
}
