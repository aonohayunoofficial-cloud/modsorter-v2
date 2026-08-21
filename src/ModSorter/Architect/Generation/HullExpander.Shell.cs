using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 素の船体の組み立て。竜骨 → フレーム → 外板 → 甲板 → ブルワークの順で、
// 上部構造（船楼・操舵室・マスト）はフェーズ6で船種ごとに載せる。
public static partial class HullExpander
{
    private static void BuildBareHull(
        Dictionary<(int x, int y, int z), string> cells, Props props, Form f, Palette p)
    {
        // 1) 体積を先に決める。外板・甲板・フレームはこの体積の内外判定から拾う。
        //    面ごとに置く条件を書き分けると、船首テーパーや船尾の立ち上がりで
        //    斜めに走る面に穴が空く。境界抽出なら船型を変えても必ず閉じる。
        var solid = new HashSet<(int x, int y, int z)>();
        var deckY = new int[f.L];
        for (int z = 0; z < f.L; z++)
        {
            int b = f.BottomY(z);
            int dk = f.DeckY(z);
            deckY[z] = dk;
            for (int y = b; y <= dk; y++)
            {
                f.Span(f.HalfAt(z, y), out int x0, out int x1);
                for (int x = x0; x <= x1; x++) solid.Add((x, y, z));
            }
        }

        // 2) 外板と甲板。体積の境界だけを置いて中は空洞にする。
        //    甲板は station ごとの最上段なので、シアに沿って前後で上下する。
        //    舷側の最上列も甲板材で塗るため、実船の舷縁（covering board）と同じ見え方になる。
        foreach (var c in solid)
        {
            if (c.y == deckY[c.z]) { cells[c] = p.Deck; continue; }
            bool edge =
                !solid.Contains((c.x - 1, c.y, c.z)) || !solid.Contains((c.x + 1, c.y, c.z)) ||
                !solid.Contains((c.x, c.y - 1, c.z)) || !solid.Contains((c.x, c.y + 1, c.z)) ||
                !solid.Contains((c.x, c.y, c.z - 1)) || !solid.Contains((c.x, c.y, c.z + 1));
            if (edge) cells[c] = p.Shell;
        }

        // 3) フレーム（肋骨）。外板の1マス内側へ station ごとに立てる。
        //    実船のフレーム間隔は0.5〜0.9m級で1マス=1mでは表現できないので、
        //    見えるように2マス以上へ丸めた間隔で入れる。
        if (f.FrameStep >= 2)
        {
            for (int z = 0; z < f.L; z += f.FrameStep)
            {
                int b = f.BottomY(z), dk = deckY[z];
                for (int y = b; y < dk; y++)
                {
                    f.Span(f.HalfAt(z, y), out int x0, out int x1);
                    Put(cells, solid, (x0 + 1, y, z), p.Frame);
                    Put(cells, solid, (x1 - 1, y, z), p.Frame);
                    // 船底のフロア材。外板のすぐ上を横へ通して肋骨をつなぐ。
                    if (y == b + 1)
                        for (int x = x0 + 1; x <= x1 - 1; x++) Put(cells, solid, (x, y, z), p.Frame);
                }
            }
        }

        // 4) 竜骨・船首材・船尾材。船底線に沿って中心列を置く。船底線は船首材の走りで
        //    甲板まで上がるので、同じ1本の線が竜骨から船首材へつながる。
        int cx0 = (f.B - 1) / 2, cx1 = f.B / 2;
        for (int z = 0; z < f.L; z++)
        {
            int b = f.BottomY(z);
            for (int x = cx0; x <= cx1; x++)
            {
                cells[(x, b, z)] = p.Keel;
                if (b != 0) continue;
                for (int k = 1; k <= f.KeelDepth; k++) cells[(x, -k, z)] = p.Keel;
            }
        }

        // 5) ブルワーク（舷墻）。甲板の縁へ立ち上げる。実船は満載喫水線規則で1m以上。
        //    船首と船尾の端だけは横へ通して塞ぐ。
        if (f.Bulwark > 0)
        {
            for (int z = 0; z < f.L; z++)
            {
                int dk = deckY[z];
                f.Span(f.HalfAt(z, dk), out int x0, out int x1);
                if (x1 - x0 < 2) continue;   // 尖った船首では甲板が細いので立てない
                bool cap = z == 0 || z == f.L - 1;
                for (int k = 1; k <= f.Bulwark; k++)
                {
                    if (cap) for (int x = x0; x <= x1; x++) cells[(x, dk + k, z)] = p.Rail;
                    else { cells[(x0, dk + k, z)] = p.Rail; cells[(x1, dk + k, z)] = p.Rail; }
                }
            }
        }
    }

    // 体積の内側で、まだ何も置いていないマスにだけ置く（外板・甲板を壊さない）。
    private static void Put(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int y, int z)> solid,
        (int x, int y, int z) key, string id)
    {
        if (!solid.Contains(key)) return;
        if (cells.ContainsKey(key)) return;
        cells[key] = id;
    }
}
