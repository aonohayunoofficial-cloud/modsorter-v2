using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 縦型容器（サイロ・給水塔・タンク）。IndustryExpander の partial。
//
// 実寸の出典。
//   サイロ … コンクリート製の円型は直径1.5〜6m・高さ5〜18m（直径の2.5〜3倍）
//            〔日本大百科全書「サイロ」〕。600tのセメントサイロはスカート支持で
//            直径6.4m×高さ20.1m〔MRSエンジニアリング 設計実績〕。
//   給水塔 … 千葉県水道局 千葉高架水槽は内径11m・有効水深5m・容量475m³・
//            満水位標高50m、中央に直径約2mの昇降路〔土木学会 選奨土木遺産解説シート〕。
//   タンク … 10万バレル級で直径39m×高さ20.7m。円錐屋根の勾配は tanθ=1/16 以下が
//            放爆構造の条件で、直径15m以上かつ高さ9m以上のタンクに適用
//            〔高圧力技術協会誌 47(6) 石油タンクの放爆構造〕。
//   防油堤 … 高さ0.5m以上、容量はタンク容量の110%以上。タンク側板から堤までは
//            直径15m未満でタンク高さの1/3以上、15m以上で1/2以上〔消防予第4号〕。
//
// 丸めの扱い。1マス=1m なので防油堤の0.5mは1マス、胴板・屋根板（実寸5〜10mm）も
// 1マスとする。いずれも実寸より厚い・高い方向の丸め。
//
// 昇降設備と開口の向きは industry_ladder_face / industry_opening_face で別々に決める。
// 同じ方角にすると梯子が開口を塞ぐため、UI の既定値は別方角にしてある。
public static partial class IndustryExpander
{
    // ===== サイロ =====
    // 下からスカート（払い出しの空間）→ホッパー→胴→屋根。
    private static void BuildSilo(
        Dictionary<(int x, int y, int z), string> cells,
        Props props,
        StructureSpec spec,
        Palette p)
    {
        int d = Clamp(spec.IndustryDiameter ?? 6, 3, 16);
        int body = Clamp(spec.IndustryBodyHeight ?? 18, 4, 48);
        int skirt = Clamp(spec.IndustrySkirt ?? 4, 0, 16);
        bool hopper = spec.IndustryHopper && skirt >= 2;
        string roof = spec.IndustryRoof ?? "dome";
        int pitch = Clamp(spec.IndustryRoofPitch ?? 2, 1, 24);
        string ladderFace = spec.IndustryLadderFace ?? "south";
        string openFace = spec.IndustryOpeningFace ?? "north";

        Disc(cells, 0, 0, d, 0, 0, p.Base);                                 // 土間

        if (skirt > 0)
        {
            Ring(cells, 0, 0, d, 1, skirt, p.Base);                         // スカート支持
            OpenRing(cells, 0, 0, d, 1, Math.Min(2, skirt), 3, openFace);   // 払い出し口
        }

        int bodyY0 = skirt + 1;

        if (hopper)
        {
            // 上へ広がる漏斗。各段は自分の半径から、一つ下の段の半径（ただし
            // 自分の半径-1 を下限）を除いた環。半径差が1マス未満でも面が抜けない。
            double half = d / 2.0;
            for (int k = 0; k < skirt; k++)
            {
                double r = 1.0 + (half - 1.0) * (k + 1) / skirt;
                double inner = Math.Min(1.0 + (half - 1.0) * k / skirt, r - 1.0);
                for (int x = 0; x < d; x++)
                    for (int z = 0; z < d; z++)
                        if (InR(x, z, d, r) && !InR(x, z, d, inner))
                            cells[(x, 1 + k, z)] = p.Shell;
            }
        }
        else
        {
            Disc(cells, 0, 0, d, bodyY0, bodyY0, p.Deck);                   // 平底
        }

        Ring(cells, 0, 0, d, bodyY0, bodyY0 + body - 1, p.Shell);           // 胴

        // 補強バンド。コンクリートステーブサイロは外周のフープで締める。
        for (int y = bodyY0 + 3; y < bodyY0 + body - 1; y += 4)
            Ring(cells, -1, -1, d + 2, y, y, p.Accent);

        int roofY = bodyY0 + body;
        int levels = BuildRoof(cells, 0, 0, d, roofY, roof, pitch, p.Roof);
        int apex = roofY + levels - 1;
        int cx = (d - 1) / 2;

        if (spec.IndustryManhole) cells[(cx, apex, cx)] = p.Glaze;

        if (spec.IndustryChute)
            for (int z = cx; z <= d + 1; z++) cells[(cx, apex + 1, z)] = p.Accent;

        // 梯子は最後。補強バンドと同じ列に来ても梯子が勝つ。
        if (spec.IndustryLadder) Ladder(cells, props, 0, 0, d, 1, roofY, ladderFace);
    }

    // ===== 給水塔 =====
    // 高置水槽方式。塔身（昇降路シャフト）の上に水槽を載せる。
    private static void BuildWaterTower(
        Dictionary<(int x, int y, int z), string> cells,
        Props props,
        StructureSpec spec,
        Palette p)
    {
        int td = Clamp(spec.IndustryDiameter ?? 11, 5, 24);
        int depth = Clamp(spec.IndustryBodyHeight ?? 5, 2, 12);
        int sw = Clamp(spec.IndustryShaftWidth ?? 4, 2, Math.Max(2, td - 2));
        int sh = Clamp(spec.IndustryShaftHeight ?? 30, 4, 60);
        string roof = spec.IndustryRoof ?? "dome";
        int pitch = Clamp(spec.IndustryRoofPitch ?? 3, 1, 24);
        string ladderFace = spec.IndustryLadderFace ?? "south";
        string openFace = spec.IndustryOpeningFace ?? "north";

        int so = (td - sw) / 2;                 // 塔身を水槽の中心へ寄せる
        int fd = Math.Min(td, sw + 4);          // 基礎の直径
        int fo = (td - fd) / 2;

        Disc(cells, fo, fo, fd, 0, 0, p.Base);
        Ring(cells, so, so, sw, 1, sh, p.Shell);

        if (spec.IndustryManhole && sw >= 3)
            OpenRing(cells, so, so, sw, 1, 2, 1, openFace);

        int tankY = sh + 1;
        Disc(cells, 0, 0, td, tankY, tankY, p.Deck);                        // 水槽の底
        Ring(cells, 0, 0, td, tankY + 1, tankY + depth, p.Shell);           // 水槽の胴

        if (spec.IndustryBalcony)
        {
            Ring(cells, -1, -1, td + 2, tankY, tankY, p.Deck);
            Ring(cells, -1, -1, td + 2, tankY + 1, tankY + 1, p.Rail);
        }

        int roofY = tankY + depth + 1;
        int levels = BuildRoof(cells, 0, 0, td, roofY, roof, pitch, p.Roof);

        if (spec.IndustryLadder) Ladder(cells, props, so, so, sw, 1, sh, ladderFace);

        int cx = (td - 1) / 2;
        cells[(cx, roofY + levels, cx)] = p.Light;                          // 航空障害灯
    }

    // ===== タンク =====
    // 屋外貯蔵タンク。底板・側板・円錐屋根に、風止めリング・らせん階段・防油堤を付ける。
    private static void BuildTank(
        Dictionary<(int x, int y, int z), string> cells,
        Props props,
        StructureSpec spec,
        Palette p)
    {
        int d = Clamp(spec.IndustryDiameter ?? 39, 6, 80);
        int body = Clamp(spec.IndustryBodyHeight ?? 21, 4, 32);
        string roof = spec.IndustryRoof ?? "cone";
        int pitch = Clamp(spec.IndustryRoofPitch ?? 16, 4, 24);
        int girder = Clamp(spec.IndustryWindGirder ?? 0, 0, 16);
        int dike = Clamp(spec.IndustryDike ?? 1, 0, 4);
        // らせん階段が +x〜+z（東→南）の弧を使うので、梯子の既定は北、
        // 側板マンホールの既定は西。どちらも UI で変えられる。
        string ladderFace = spec.IndustryLadderFace ?? "north";
        string openFace = spec.IndustryOpeningFace ?? "west";

        Disc(cells, -1, -1, d + 2, 0, 0, p.Base);       // 基礎パッド
        Disc(cells, 0, 0, d, 1, 1, p.Deck);             // 底板
        Ring(cells, 0, 0, d, 2, body + 1, p.Shell);     // 側板

        if (girder > 0)
            for (int y = 2 + girder; y <= body + 1; y += girder)
                Ring(cells, -1, -1, d + 2, y, y, p.Accent);     // 風止めリング

        int roofY = body + 2;
        BuildRoof(cells, 0, 0, d, roofY, roof, pitch, p.Roof);

        if (spec.IndustryManhole) OpenRing(cells, 0, 0, d, 2, 3, 1, openFace, p.Glaze);

        if (spec.IndustryStair) Helix(cells, props, 0, 0, d, 2, body + 1, p.Deck, p.Stair);

        if (spec.IndustryLadder) Ladder(cells, props, 0, 0, d, 2, roofY, ladderFace);

        if (dike > 0)
        {
            // 側板から堤までの距離。直径15m未満でタンク高さの1/3、15m以上で1/2。
            int dist = Math.Max(1, body / (d < 15 ? 3 : 2));
            int m = dist + 1;
            int x0 = -m, x1 = d - 1 + m, z0 = -m, z1 = d - 1 + m;
            Fill(cells, x0, x1, 0, dike - 1, z0, z0, p.Base);
            Fill(cells, x0, x1, 0, dike - 1, z1, z1, p.Base);
            Fill(cells, x0, x0, 0, dike - 1, z0, z1, p.Base);
            Fill(cells, x1, x1, 0, dike - 1, z0, z1, p.Base);
        }
    }
}
