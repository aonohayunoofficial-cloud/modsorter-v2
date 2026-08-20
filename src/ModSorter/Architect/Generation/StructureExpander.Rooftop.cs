using System;
using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 屋根の上に載るもの（パラペット・塔屋）。どちらも平屋根専用。
public static partial class StructureExpander
{
    // パラペット（陸屋根の立ち上がり）。平屋根のときだけ、屋根面(y=h-1)の外周を
    // その上へ立ち上げる。研究所・倉庫・オフィスなど陸屋根の建物の輪郭を作る。
    // マスクの縁(IsEdge)に沿って回すので、L字・コの字の平面でも内側角まで正しく続く。
    // 勾配屋根では軒先と衝突して破綻するため flat 以外では作らない。
    // parapet_crenel が true なら最上段だけを周期的に抜いて狭間（城の胸壁）にする。
    // 抜くのは最上段のみなので、下に必ず1段以上の環が残り屋根面は外から見えない。
    private static void BuildParapet(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot,
        StructureSpec spec, int h,
        IReadOnlyList<string> allowedBlocks, string wall,
        string roofType, int parapet)
    {
        if (parapet <= 0 || roofType != "flat") return;

        string parapetBlock = Pick(spec.ParapetBlock, allowedBlocks, wall);
        int crenelStep = spec.ParapetCrenel ? Clamp(spec.ParapetCrenelStep ?? 3, 2, 6) : 0;
        foreach (var (x, z) in foot)
        {
            if (!IsEdge(foot, x, z)) continue;
            for (int py = 1; py <= parapet; py++)
            {
                if (crenelStep > 0 && py == parapet && IsCrenelGap(foot, x, z, crenelStep))
                    continue;
                cells[(x, h - 1 + py, z)] = parapetBlock;
            }
        }
    }

    // 塔屋（屋上の機械室・階段室）。平屋根のときだけ、屋根面に壁と天面を持つ
    // 小さな箱を載せる。下の屋根面がそのまま塔屋の床になるので床は作らない。
    // 位置は penthouse_align で決める。x 方向と z 方向の寄せを独立に見るので、
    // "northeast" のような複合指定で4隅寄せになる。
    //   center（既定）… 平面の中央。
    //   north / south … z 方向の端寄せ（north = z 小側、south = z 大側）。
    //   west / east   … x 方向の端寄せ（west = x 小側、east = x 大側）。
    // 寄せたときはパラペットがあるぶん1マス内側に置き、パラペットの環を切らない。
    // 勾配屋根では軒・棟と干渉するため作らない。
    private static void BuildPenthouse(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot,
        StructureSpec spec, int w, int d, int h,
        IReadOnlyList<string> allowedBlocks, string wall, string roof,
        string roofType, int parapet)
    {
        int phH = Clamp(spec.PenthouseHeight ?? 0, 0, 12);
        int phW = Clamp(spec.PenthouseWidth ?? 0, 0, w);
        int phD = Clamp(spec.PenthouseDepth ?? 0, 0, d);
        if (!(phH > 0 && phW >= 3 && phD >= 3 && roofType == "flat")) return;

        string phBlock = Pick(spec.PenthouseBlock, allowedBlocks, wall);
        string phAlign = (spec.PenthouseAlign ?? "center").Trim().ToLowerInvariant();
        int inset = parapet > 0 ? 1 : 0;

        // 含まれる方角で x・z を別々に決める。両方含めば角寄せ、無ければ中央。
        int px0 = phAlign.Contains("west") ? inset
                : phAlign.Contains("east") ? w - phW - inset
                : (w - phW) / 2;
        int pz0 = phAlign.Contains("north") ? inset
                : phAlign.Contains("south") ? d - phD - inset
                : (d - phD) / 2;

        // 寄せた結果が平面から出ないよう最後にクランプする。
        px0 = Clamp(px0, 0, Math.Max(0, w - phW));
        pz0 = Clamp(pz0, 0, Math.Max(0, d - phD));

        for (int x = px0; x < px0 + phW; x++)
            for (int z = pz0; z < pz0 + phD; z++)
            {
                // 非矩形平面では屋根が無い位置に浮かせないよう、マスク内だけに置く。
                if (!foot.Contains((x, z))) continue;

                bool edge = x == px0 || x == px0 + phW - 1 ||
                            z == pz0 || z == pz0 + phD - 1;
                if (edge)
                    for (int py = 1; py < phH; py++)
                        cells[(x, h - 1 + py, z)] = phBlock;

                cells[(x, h - 1 + phH, z)] = roof;
            }
    }
}
