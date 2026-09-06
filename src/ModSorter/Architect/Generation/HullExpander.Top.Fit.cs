using System;

namespace ModSorter.Architect.Generation;

// Top の寸法計算のうち長いもの。櫂の実際の張り出し・マストを立てる station・
// 上部構造の天端。HullExpander.Top.cs が10,434バイトになったので分けた。
//
// readonly フィールドはコンストラクタでしか代入できないので、ここは計算だけを持ち、
// 戻り値をコンストラクタが受け取る。式がここ1か所にあるので、Extent（UI の外寸）と
// 組み立て（BuildOars / BuildRig / BuildCastle / BuildDeckHouse）で値が食い違わない。
public static partial class HullExpander
{
    private sealed partial class Top
    {
        // 櫂が実際に何マス外へ出るか。BuildOars と同じ station の選び方（船体中央から
        // 前後へ振り分け・舷が寄る station は飛ばす）と、同じ止め方（1マスごとに1段
        // 下げ、水面の手前で止める）で数える。乾舷が1マスしかない端艇では1マスしか
        // 出ないため、一律3マス（OarReach）として外寸を取ると生成物と食い違う。
        private static int OarReachOf(Form f, int oarPerSide)
        {
            if (oarPerSide <= 0) return 0;

            int reach = 0;
            int oz = Math.Max(1, f.L / 2 - oarPerSide / 2);
            for (int i = 0; i < oarPerSide; i++)
            {
                int z = oz + i;
                if (z >= f.L - 1) break;

                int dk = f.DeckY(z);
                f.Span(f.HalfAt(z, dk), out int a, out int b);
                if (b - a < 2) continue;

                int y = dk + f.Bulwark;
                int k = 0;
                while (k < OarReach && y - k > f.WL) k++;
                if (k > reach) reach = k;
            }
            return reach;
        }

        // マストを立てられる station。船楼の占める範囲（船尾なら z = 0〜CastleLen-1）と、
        // その内側の妻面のすぐ前を外す。妻面には出入口が開くので、その正面に柱が立つと
        // 戸口が塞がる。実船でもマストは隔壁と戸口を避けて竜骨の上へ据える。
        // 前後の船楼で船体が埋まる小舟は避けようがないので、そのときは全長へ戻す。
        private static int[] MastZsOf(
            Form f, int count, int castleAft, int castleFore, int castleLen)
        {
            int lo = castleAft > 0 ? castleLen + 1 : 1;
            int hi = castleFore > 0 ? f.L - castleLen - 2 : f.L - 2;
            lo = Math.Clamp(lo, 1, Math.Max(1, f.L - 2));
            hi = Math.Clamp(hi, 1, Math.Max(1, f.L - 2));
            if (hi < lo) { lo = 1; hi = Math.Max(1, f.L - 2); }

            var zs = new int[count];
            int prev = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                int z = (int)Math.Round(f.L * (i + 1.0) / (count + 1.0));
                z = Math.Clamp(z, lo, hi);
                // 範囲へ詰めた結果2本が同じ station へ重なると1本ぶん消えるので、
                // 前のマストより後ろへ1マスずつ送る。
                if (z <= prev) z = Math.Min(prev + 1, hi);
                prev = z;
                zs[i] = z;
            }
            return zs;
        }

        // 上部構造の天端。TopY はコンストラクタの最後に代入するので、ここへ渡る t は
        // マスト・船首飾り・船楼・デッキハウスのフィールドがすべて埋まっている。
        //
        // 船楼は CastleFloorY（Castle.cs）、デッキハウスは BuildDeckHouse（House.cs）と
        // 同じ式を通す。マストの頂は BuildRig の mastTop（= DeckY + MastHeight）と同じ。
        private static int TopYOf(Form f, Top t)
        {
            int top = 0;

            foreach (int z in t.MastZs)
            {
                int y = f.DeckY(z) + t.MastHeight;
                if (y > top) top = y;
            }

            if (t.HeadHeight > 0)
            {
                int y = Math.Max(f.DeckY(0), f.DeckY(f.L - 1)) + t.HeadHeight;
                if (y > top) top = y;
            }

            // 船楼は船体中央を向く端の甲板から高さを取り、その上に手すりが1マス載る。
            if (t.CastleAft > 0)
            {
                int zi = Math.Min(t.CastleLen - 1, f.L - 1);
                int y = CastleFloorY(f, zi, t.CastleAft) + 1;
                if (y > top) top = y;
            }
            if (t.CastleFore > 0)
            {
                int zi = Math.Max(f.L - t.CastleLen, 0);
                int y = CastleFloorY(f, zi, t.CastleFore) + 1;
                if (y > top) top = y;
            }

            // デッキハウスと煙突の天端。下端は範囲内の甲板の最大＋1、1層は HouseFloorH。
            if (t.HouseDecks > 0)
            {
                int len = Math.Max(3, f.L * t.HouseLen / 100);
                int z0 = Math.Max(1, (f.L - len) / 2 + t.HouseShift);
                int z1 = Math.Min(f.L - 2, z0 + len - 1);
                int baseY = f.DeckY(Math.Max(0, z0)) + 1;
                for (int z = Math.Max(0, z0); z <= Math.Max(0, z1); z++)
                    baseY = Math.Max(baseY, f.DeckY(z) + 1);

                int y = baseY + t.HouseDecks * HouseFloorH - 1 + t.Funnel;
                if (y > top) top = y;
            }

            return top;
        }
    }
}
