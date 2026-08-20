using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 柱まわりの部品。円柱1本・列柱（開放型）・柱位置の等間隔割り。
// AxisPositions は神殿のファサード(StructureExpander.Parts.Temple.cs)からも使う。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // 円柱を1本立てる純粋な部品。中心(cx,cz)、半径r、y=yFrom..yTo に各層 半径rの円を置く。
    private static void BuildColumn(
        Dictionary<(int x, int y, int z), string> cells,
        int cx, int cz, int r, int yFrom, int yTo, string block, int w, int d)
    {
        for (int y = yFrom; y <= yTo; y++)
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    // 円の内側か（半径rの塗りつぶし円）
                    if (dx * dx + dz * dz > r * r) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || x >= w || z < 0 || z >= d) continue; // 建物範囲内のみ
                    cells[(x, y, z)] = block;
                }
    }

    // 開放型（列柱）: 外周の角＋等間隔の位置に円柱を立てる。
    // 柱の太さ(半径)は高さで決め、建物の幅・奥行きが小さければ抑える。
    private static void BuildColonnade(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int h, string col)
    {
        // 柱の太さ: 高さで段階的に。小さい建物には太すぎないよう幅奥行の1/4で上限。
        int rByHeight = h < 10 ? 1 : (h < 18 ? 2 : 3);
        int rByFootprint = Math.Max(1, Math.Min(w, d) / 5);
        int r = Math.Min(rByHeight, rByFootprint);

        // 柱の中心が建物範囲に収まるよう、端からr内側に置く。
        int lo = r, hiX = w - 1 - r, hiZ = d - 1 - r;
        if (hiX < lo) hiX = lo;
        if (hiZ < lo) hiZ = lo;

        // 柱を立てる高さ範囲（床のすぐ上〜屋根の下）。
        int yTop = h - 2;
        if (yTop < 1) yTop = 1;

        // 柱の間隔: 寸法から自動（柱の直径＋2マスの隙間を目安）。最低3。
        int step = Math.Max(4, r * 2 + 3);

        // 柱を立てる位置（x座標群・z座標群）を等間隔で集める。両端は必ず含む。
        var xs = AxisPositions(lo, hiX, step);
        var zs = AxisPositions(lo, hiZ, step);

        // 外周（最初と最後のxまたはz）にあたる位置にだけ柱を立てる。
        foreach (int cxp in xs)
            foreach (int czp in zs)
            {
                bool onPerimeter =
                    cxp == xs.First() || cxp == xs.Last() ||
                    czp == zs.First() || czp == zs.Last();
                if (!onPerimeter) continue;
                BuildColumn(cells, cxp, czp, r, 1, yTop, col, w, d);
            }
    }

    // lo..hi を step 間隔で並べた位置リスト（両端を必ず含む）。
    // lo..hi に柱を均等配置する。両端を必ず含み、柱同士が step 以上離れるよう
    // 本数を決めてから均等割りするので、端数で最後の柱が詰まることがない。
    private static List<int> AxisPositions(int lo, int hi, int step)
    {
        var list = new List<int>();
        if (hi <= lo) { list.Add(lo); return list; }

        int span = hi - lo;
        // 端から端までに入る「区間数」。step 以上の間隔を保てる最大数。
        int segments = Math.Max(1, span / step);
        // segments+1 本の柱を等間隔に置く（両端含む）。
        for (int i = 0; i <= segments; i++)
        {
            int v = lo + (int)Math.Round((double)span * i / segments);
            if (list.Count == 0 || list.Last() != v) list.Add(v);
        }
        return list;
    }
}
