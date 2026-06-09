using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// StructureSpec を確定的に座標へ展開する。
// 壁の外周リングは必ずここで生成するため、塊化や壁抜けは原理的に起きない。
public static class StructureExpander
{
    public static List<GeneratedBlock> Expand(StructureSpec spec, IReadOnlyList<string> allowedBlocks)
    {
        // 寸法の健全化（最低 2x2x2、極端な値は抑える）
        int w = Clamp(spec.Width, 2, 64);
        int d = Clamp(spec.Depth, 2, 64);
        int h = Clamp(spec.Height, 2, 64);

        // 素材決定（許可リスト外なら先頭ブロックにフォールバック）
        string fallback = allowedBlocks.Count > 0 ? allowedBlocks[0] : "minecraft:oak_planks";
        string wall = Pick(spec.WallBlock, allowedBlocks, fallback);
        string floor = Pick(spec.FloorBlock ?? spec.WallBlock, allowedBlocks, wall);
        string roof = Pick(spec.RoofBlock ?? spec.WallBlock, allowedBlocks, wall);

        // 座標 -> ブロックID。後勝ち（開口部で上書きするため）。
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 床（y=0 全面）
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, 0, z)] = floor;

        // 土台段（base course）: y=0 の外周一周を土台材に差し替える。
        // 未指定なら floor と同じ＝従来の見た目（差し替えても影響なし）。座標系は変えない。
        string baseBlock = Pick(spec.BaseBlock, allowedBlocks, floor);
        if (spec.HasBase)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    if (x == 0 || x == w - 1 || z == 0 || z == d - 1)
                        cells[(x, 0, z)] = baseBlock;
        }

        // 屋根（roof_type で分岐）
        string roofType = (spec.RoofType ?? "flat").Trim().ToLowerInvariant();
        if (roofType == "gable")
            BuildGableRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "dome")
            BuildDomeRoof(cells, spec, w, d, h, roof);
        else
            BuildFlatRoof(cells, w, d, h, roof);

        string buildingStyle = (spec.BuildingStyle ?? "walled").Trim().ToLowerInvariant();

        if (buildingStyle == "colonnade")
        {
            // 開放型: 壁を立てず、外周の角＋等間隔の位置に円柱を立てる（神殿風）。
            BuildColonnade(cells, w, d, h, wall);
        }
        else
        {
            // アクセント材（柱型リズム用）。未指定なら wall と同じ＝従来の見た目。
            string accent = Pick(spec.AccentBlock, allowedBlocks, wall);
            // 柱なし(0)はそのまま尊重。柱ありの場合は最低4間隔を強制して密集を防ぐ。
            int pilasterStep = 0;
            if (spec.PilasterStep.HasValue && spec.PilasterStep.Value >= 2)
                pilasterStep = Math.Max(4, spec.PilasterStep.Value);

            // 壁（中間層 y=1..h-2 の外周リングのみ）
            for (int y = 1; y <= h - 2; y++)
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        if (x == 0 || x == w - 1 || z == 0 || z == d - 1)
                        {
                            bool isCorner = (x == 0 || x == w - 1) && (z == 0 || z == d - 1);
                            bool isPilaster = pilasterStep > 0 &&
                                ((x == 0 || x == w - 1) ? (z % pilasterStep == 0)
                                                        : (x % pilasterStep == 0));
                            cells[(x, y, z)] = (isCorner || isPilaster) ? accent : wall;
                        }
        }

        // 中間床（複数階）。指定された各 y に内部も含む全面の床を敷く。
        foreach (int fy in (spec.FloorLevels ?? new List<int>()).Distinct())
        {
            // 1階の床(0)・屋根の領域(h-1以上)とぶつかる指定は無視
            if (fy <= 0 || fy >= h - 1) continue;
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                    cells[(x, fy, z)] = floor;
        }

        // 開口部の適用（中間床より後。床に窓・ドアが指定されても壁セルのみ作用するので安全）
        // colonnade（開放型）は壁がないので開口部は適用しない。
        if (buildingStyle != "colonnade")
            foreach (var op in spec.Openings ?? new List<Opening>())
                ApplyOpening(cells, op, w, d, h, allowedBlocks);

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x,
                Y = kv.Key.y,
                Z = kv.Key.z,
                Id = kv.Value
            })
            .ToList();
    }

    // 平屋根: 最上層 y=h-1 を全面で塞ぐ
    private static void BuildFlatRoof(
        Dictionary<(int x, int y, int z), string> cells, int w, int d, int h, string roof)
    {
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, h - 1, z)] = roof;
    }

    // 切妻屋根: 棟の向き(ridge_axis)に沿って段々に三角を作る。
    // 屋根は本体の上(y=h から上)に積む。傾斜方向の端から中央へ向け段を上げる。
    private static void BuildGableRoof(
    Dictionary<(int x, int y, int z), string> cells,
    StructureSpec spec, int w, int d, int h, string roof, string wall)
    {
        string axis = (spec.RidgeAxis ?? "x").Trim().ToLowerInvariant();
        // 棟がx軸に平行 → z方向に傾斜（zの端から中央へ高くなる）
        // 棟がz軸に平行 → x方向に傾斜（xの端から中央へ高くなる）
        bool ridgeAlongX = (axis != "z");

        int slopeLen = ridgeAlongX ? d : w; // 傾斜する方向の長さ
        // 端から中央までの段数。中央で最も高い。
        int half = (slopeLen + 1) / 2;

        for (int i = 0; i < slopeLen; i++)
        {
            // 端(0 と slopeLen-1)が低く、中央が高い。step = 端からの距離。
            int step = System.Math.Min(i, slopeLen - 1 - i);
            int yLevel = (h - 1) + step; // 壁の最上層と同じ高さ(y=h-1)から積む

            if (ridgeAlongX)
            {
                // 屋根: z=i の列。棟方向(x)は全幅に渡って同じ高さ。
                for (int x = 0; x < w; x++)
                    cells[(x, yLevel, i)] = roof;

                // 妻壁: 妻側の面(x=0 と x=w-1)で、壁の上端(y=h-1)から
                //       この列の屋根の手前(yLevel-1)までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(0, y, i)] = wall;
                    cells[(w - 1, y, i)] = wall;
                }
            }
            else
            {
                // 屋根: x=i の列。棟方向(z)は全奥行きに渡って同じ高さ。
                for (int z = 0; z < d; z++)
                    cells[(i, yLevel, z)] = roof;

                // 妻壁: 妻側の面(z=0 と z=d-1)で、壁の上端からこの列の屋根の手前までを埋める。
                for (int y = h - 1; y < yLevel; y++)
                {
                    cells[(i, y, 0)] = wall;
                    cells[(i, y, d - 1)] = wall;
                }
            }
        }

    }

    // ドーム屋根: 建物の上面(w×d)を底面とする半楕円体の殻を、y=h-1 から上に積む。
    // 半径 rx=w/2, rz=d/2。ドーム高さ ry は spec.DomeHeight（未指定なら控えめな既定）。
    // 殻だけ残す（中空）ことで屋根らしくする。底面 y=h-1 は全面塞いで天井とする。
    private static void BuildDomeRoof(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string roof)
    {
        double rx = w / 2.0;
        double rz = d / 2.0;
        // 中心（底面）。整数格子の中央。
        double cx = (w - 1) / 2.0;
        double cz = (d - 1) / 2.0;

        // ドームの高さ。未指定なら水平半径の小さい方に合わせる（半球に近い）。
        int ry = spec.DomeHeight.HasValue && spec.DomeHeight.Value >= 1
            ? spec.DomeHeight.Value
            : Math.Max(2, (int)Math.Round(Math.Min(rx, rz)));

        int baseY = h - 1; // ドームの底面（壁の最上層の上）

        // まず底面を天井として全面塞ぐ（ドームの足元の穴を防ぐ）
        for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
                cells[(x, baseY, z)] = roof;

        // 半楕円体の殻。yLayer=0..ry の各層で、その高さの輪郭リングを置く。
        for (int yi = 0; yi <= ry; yi++)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < d; z++)
                {
                    double nx = (x - cx) / (rx <= 0 ? 1 : rx);
                    double nz = (z - cz) / (rz <= 0 ? 1 : rz);
                    double ny = (double)yi / (ry <= 0 ? 1 : ry);
                    double v = nx * nx + nz * nz + ny * ny;
                    if (v > 1.0) continue; // 楕円体の外

                    // 殻判定: 隣接が外側になるセルだけ残す（表面）
                    bool surface =
                        Outside(x + 1, cx, rx, z, cz, rz, yi, ry) ||
                        Outside(x - 1, cx, rx, z, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z + 1, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z - 1, cz, rz, yi, ry) ||
                        Outside(x, cx, rx, z, cz, rz, yi + 1, ry);
                    if (!surface) continue;

                    cells[(x, baseY + yi, z)] = roof;
                }
        }
    }

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

    // 指定セルが半楕円体の外側か（殻判定用）
    private static bool Outside(int x, double cx, double rx, int z, double cz, double rz, int yi, int ry)
    {
        double nx = (x - cx) / (rx <= 0 ? 1 : rx);
        double nz = (z - cz) / (rz <= 0 ? 1 : rz);
        double ny = (double)yi / (ry <= 0 ? 1 : ry);
        return (nx * nx + nz * nz + ny * ny) > 1.0;
    }

    private static void ApplyOpening(
        Dictionary<(int x, int y, int z), string> cells,
        Opening op, int w, int d, int h, IReadOnlyList<string> allowedBlocks)
    {
        string face = (op.Face ?? "").Trim().ToLowerInvariant();
        bool isWindow = (op.Kind ?? "").Trim().ToLowerInvariant() != "door";

        int y = Clamp(op.Level, 1, Math.Max(1, h - 2)); // 中間層に収める
                                                        // 窓が床ぎわ(level=1)に張り付くのを防ぎ、壁の中ほどへ引き上げる。
                                                        // ドアは床から立てるので対象外。
        if (isWindow)
        {
            int mid = Math.Max(1, (h - 1) / 2); // 壁のおよそ中段
            if (y < mid) y = mid;
        }

        // 面ごとに、面に沿った座標(offset)から壁上の1セルを特定する
        (int x, int z)? target = face switch
        {
            "north" => (Clamp(op.Offset, 0, w - 1), 0),       // z=0 の面
            "south" => (Clamp(op.Offset, 0, w - 1), d - 1),   // z=d-1 の面
            "west" => (0, Clamp(op.Offset, 0, d - 1)),       // x=0 の面
            "east" => (w - 1, Clamp(op.Offset, 0, d - 1)),   // x=w-1 の面
            _ => null
        };
        if (target == null) return;

        var key = (target.Value.x, y, target.Value.z);
        // 壁セルでなければ無視（角や非外周を壊さない）
        if (!cells.ContainsKey(key)) return;

        bool isDoor = (op.Kind ?? "").Trim().ToLowerInvariant() == "door";
        if (isDoor)
        {
            cells.Remove(key); // ドア下段
                               // ドアは縦2マス。1つ上の段も同じ面・同じ位置を開ける（壁セルのときのみ）。
            var upper = (target.Value.x, y + 1, target.Value.z);
            if (cells.ContainsKey(upper)) cells.Remove(upper);
        }
        else
        {
            string glass = Pick(op.Block ?? "minecraft:glass", allowedBlocks, "minecraft:glass");
            cells[key] = glass; // 窓=ガラス置換
        }
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? candidate, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = allowed.FirstOrDefault(
                a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return fallback;
    }
}
