using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 付加部品: 軒・縁側・煙突・塔・塔の頂部・円柱・列柱・神殿のファサード。
// 屋根の上や平面の外側に足す要素なので、呼ぶ順序が結果を左右する。
// 順序の責任は StructureExpander.cs の ExpandCore が持つ。
// StructureExpander の partial。
public static partial class StructureExpander
{
    // 軒の出: 選択された面(north/south/east/west)の外側 eave マスへ、屋根を張り出す。
    //   各面の軒の高さは「屋根の実際の高さ」に合わせる。妻側(傾斜方向)は列ごとに
    //   階段状の高さで、軒先(棟に平行な面)は端の列の高さ(=水平)で伸びる。
    //   隣接2面がともに選択されたときは、その角も屋根の角の高さで埋めて穴を防ぐ。
    // 負座標(x=-1 等)も書くが、呼び出し元の一括シフトで 0 以上へ正規化される。
    private static void BuildEaves(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, StructureSpec spec,
        int w, int d, int h, string roof, string roofType, int eave)
    {
        bool en = spec.EaveNorth, es = spec.EaveSouth, ee = spec.EaveEast, ew = spec.EaveWest;
        if (!en && !es && !ee && !ew) return; // どの面も選ばれていなければ軒なし。

        // 屋根の (x,z) 列の最高y。屋根が無い列は h-1（壁上端）を返す。
        // flat/gable/shed いずれも、既に cells に積まれた屋根の実高さをそのまま使うので
        // 屋根形状に依らず正しい高さで軒が揃う。
        int RoofTop(int x, int z)
        {
            int top = int.MinValue;
            foreach (var k in cells.Keys)
                if (k.x == x && k.z == z && k.y >= h - 1 && k.y > top) top = k.y;
            return top == int.MinValue ? (h - 1) : top;
        }

        // 北面(z=0の外側 z<0)。x=0..w-1 の各列を、その列の屋根高さで z<0 へ伸ばす。
        if (en)
            for (int x = 0; x < w; x++)
            {
                int y = RoofTop(x, 0);
                for (int e = 1; e <= eave; e++) cells[(x, y, -e)] = roof;
            }
        // 南面(z=d-1の外側 z>=d)。
        if (es)
            for (int x = 0; x < w; x++)
            {
                int y = RoofTop(x, d - 1);
                for (int e = 1; e <= eave; e++) cells[(x, y, d - 1 + e)] = roof;
            }
        // 西面(x=0の外側 x<0)。
        if (ew)
            for (int z = 0; z < d; z++)
            {
                int y = RoofTop(0, z);
                for (int e = 1; e <= eave; e++) cells[(-e, y, z)] = roof;
            }
        // 東面(x=w-1の外側 x>=w)。
        if (ee)
            for (int z = 0; z < d; z++)
            {
                int y = RoofTop(w - 1, z);
                for (int e = 1; e <= eave; e++) cells[(w - 1 + e, y, z)] = roof;
            }

        // ===== 角埋め: 隣接2面がともに選ばれたら、その隅の eave×eave を屋根の角高さで埋める =====
        // 角の高さは屋根のその隅(0,0)/(w-1,0)/(0,d-1)/(w-1,d-1)の実高さに合わせる。
        void FillCorner(bool cond, int cornerX, int cornerZ, int sxSign, int szSign)
        {
            if (!cond) return;
            int y = RoofTop(cornerX, cornerZ);
            for (int ex = 1; ex <= eave; ex++)
                for (int ez = 1; ez <= eave; ez++)
                    cells[(cornerX + sxSign * ex, y, cornerZ + szSign * ez)] = roof;
        }
        FillCorner(ew && en, 0, 0, -1, -1);         // 北西
        FillCorner(ee && en, w - 1, 0, +1, -1);      // 北東
        FillCorner(ew && es, 0, d - 1, -1, +1);      // 南西
        FillCorner(ee && es, w - 1, d - 1, +1, +1);  // 南東
    }

    // 縁側／基壇の縁: 平面マスクの外側へ v マスぶん、y=0 に床を敷き足す。
    // マスク内のどのマスからチェビシェフ距離 v 以内かで判定するので、
    // L字・十字などの非矩形でも輪郭に沿って回り縁ができる（角が欠けない）。
    // 建物の真下は既に床があるので触らない。軒と同じく負座標を一時的に作る。
    private static void BuildVeranda(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, int w, int d, int v, string block)
    {
        if (v <= 0) return;

        for (int x = -v; x < w + v; x++)
            for (int z = -v; z < d + v; z++)
            {
                if (foot.Contains((x, z))) continue;

                bool near = false;
                for (int ox = -v; ox <= v && !near; ox++)
                    for (int oz = -v; oz <= v; oz++)
                        if (foot.Contains((x + ox, z + oz))) { near = true; break; }

                if (near) cells[(x, 0, z)] = block;
            }
    }

    // 煙突: 屋根の上に本数ぶん自動で等間隔に立てる。位置は寄せ方向(chimney_align)で決める。
    //   center（既定）… 建物の中心線上に、x軸に沿って等間隔で並ぶ。
    //   north/south   … その面寄り（z を端側へ）に寄せ、x軸に沿って並ぶ。
    //   east/west     … その面寄り（x を端側へ）に寄せ、z軸に沿って並ぶ。
    // 各煙突の(x,z)で、既に積まれた屋根の「その列の最大y」を調べ、そこから上へ
    // chimney_height マス積む。貫通ON(chimney_pierce)なら床上(y=1)から屋根を貫いて通す。
    private static void BuildChimney(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h, string chimney)
    {
        int count = spec.ChimneyCount;
        if (count <= 0) return;

        int stackH = spec.ChimneyHeight.HasValue && spec.ChimneyHeight.Value > 0
            ? spec.ChimneyHeight.Value : 2;

        string align = (spec.ChimneyAlign ?? "center").Trim().ToLowerInvariant();

        // 端から少し内側に寄せる余白（角に食い込ませない）。
        int margin = 1;

        // 並ぶ軸(along)と、寄せる固定座標を決める。
        // north/south は z を固定して x 方向に並ぶ。east/west は x を固定して z 方向に並ぶ。
        bool alongX; // true: x方向に並ぶ, false: z方向に並ぶ
        int fixedCoord; // 並ぶ軸に直交する側の固定値

        switch (align)
        {
            case "north": alongX = true; fixedCoord = margin; break;                 // z=手前寄り
            case "south": alongX = true; fixedCoord = d - 1 - margin; break;          // z=奥寄り
            case "west": alongX = false; fixedCoord = margin; break;                  // x=左寄り
            case "east": alongX = false; fixedCoord = w - 1 - margin; break;          // x=右寄り
            default: alongX = true; fixedCoord = (d - 1) / 2; break;                  // center: z中央、x方向
        }
        if (fixedCoord < 0) fixedCoord = 0;

        // 太さ → 断面オフセット（中心(cx,cz)からの相対(dx,dz)）と占有幅。
        //   thin   … 中心1マスのみ（従来）。占有幅1。
        //   medium … プラス型（中心を抜いた上下左右4マス・中空）。占有幅3。
        //   thick  … 中央2×2を抜いた4×4外周（12マス・中空2×2）。占有幅4。
        string thickness = (spec.ChimneyThickness ?? "thin").Trim().ToLowerInvariant();
        (int dx, int dz)[] section;
        int footprint;
        if (thickness == "medium")
        {
            section = new[] { (0, -1), (-1, 0), (1, 0), (0, 1) };
            footprint = 3;
        }
        else if (thickness == "thick")
        {
            // -1..2 の 4×4 から、中央2×2(0..1, 0..1)を除いた外周12マス。
            var ring = new List<(int, int)>();
            for (int ox = -1; ox <= 2; ox++)
                for (int oz = -1; oz <= 2; oz++)
                {
                    bool hole = (ox >= 0 && ox <= 1 && oz >= 0 && oz <= 1);
                    if (!hole) ring.Add((ox, oz));
                }
            section = ring.ToArray();
            footprint = 4;
        }
        else
        {
            section = new[] { (0, 0) };
            footprint = 1;
        }

        // 並ぶ軸の有効範囲（角を避けた内側）。太い煙突は断面が縁からはみ出さないよう
        // さらに (footprint-1) ぶん内側へ寄せる。
        int span = alongX ? w : d;
        int extra = footprint - 1;
        int lo = margin + extra, hi = span - 1 - margin - extra;
        if (hi < lo) { lo = 0; hi = span - 1; }

        // 本数クランプ: 並ぶ範囲に占有幅ぶんの間隔で収まる数を上限にする。
        int rangeLen = hi - lo + 1;
        int capacity = Math.Max(1, rangeLen / footprint);
        int n = Math.Min(count, capacity);

        // 固定座標側も断面が範囲外に出ないようクランプ（中/太で端寄せしたとき用）。
        int fixedClamped = Math.Clamp(fixedCoord, extra + 0,
            (alongX ? d : w) - 1 - extra);
        if (fixedClamped < 0) fixedClamped = 0;

        for (int i = 0; i < n; i++)
        {
            int p = (n == 1)
                ? (lo + hi) / 2
                : lo + (int)System.Math.Round((double)(hi - lo) * i / (n - 1));

            int cx = alongX ? p : fixedClamped;
            int cz = alongX ? fixedClamped : p;
            if (cx < 0 || cx >= w || cz < 0 || cz >= d) continue;

            // その中心列の屋根の最高y（既に cells に積まれた最大y）。無ければ壁上端 h-1。
            int topY = h - 1;
            foreach (var k in cells.Keys)
                if (k.x == cx && k.z == cz && k.y > topY) topY = k.y;

            // 積み始めのy。貫通ONは床上(y=1)から、OFFは屋根上端の1つ上から。
            int startY = spec.ChimneyPierce ? 1 : topY + 1;
            // 煙突頂上 = 屋根上端 + stackH。
            int endY = topY + stackH;

            // 断面を全高に積む。medium/thick は中心が抜けるので自然に中空の筒になる。
            foreach (var (ox, oz) in section)
            {
                int bx = cx + ox, bz = cz + oz;
                if (bx < 0 || bx >= w || bz < 0 || bz >= d) continue;
                for (int y = startY; y <= endY; y++)
                    cells[(bx, y, bz)] = chimney;
            }
        }
    }

    // 塔（鐘塔・尖塔・ミナレット）: 建物の平面内に正方形の塔を立て、屋根より上へ突き出す。
    // 四周の壁を y=1 から塔の上端(topY = h-1+tower_height)まで塞ぎ、内側は抜いて吹き抜けにする。
    // 内側を抜くと下の屋根面に穴が空くが、四周の壁と頂部で覆われるので外から内部は見えない。
    // 位置は tower_align、頂部の形は tower_roof で決める。
    // 塔は開口部の適用より後に作られるため、正面中央に塔を置くと壁に開けたドア・大開口が
    // 塔の壁で塞がれる。正面側の外周に接する塔には足元の入口をここで開け直す。
    private static void BuildTower(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot, StructureSpec spec,
        int w, int d, int h, string tower, string roof)
    {
        int s = Clamp(spec.TowerWidth ?? 0, 0, Math.Min(w, d));
        int th = Clamp(spec.TowerHeight ?? 0, 0, 32);
        if (s < 3 || th < 1) return;

        string align = (spec.TowerAlign ?? "front").Trim().ToLowerInvariant();
        string cap = (spec.TowerRoof ?? "spire").Trim().ToLowerInvariant();
        string face = (spec.FacadeFace ?? "south").Trim().ToLowerInvariant();
        if (face != "north" && face != "south" && face != "east" && face != "west")
            face = "south";

        int topY = h - 1 + th;               // 塔の壁の上端
        int cx = (w - s) / 2, cz = (d - s) / 2;
        int xMax = Math.Max(0, w - s), zMax = Math.Max(0, d - s);

        // 塔の左手前角(x0,z0)を align から作る。正面は facade_face で決まる。
        var spots = new List<(int x0, int z0)>();
        switch (align)
        {
            case "center":
                spots.Add((cx, cz));
                break;

            case "rear":
                if (face == "south") spots.Add((cx, 0));
                else if (face == "north") spots.Add((cx, zMax));
                else if (face == "east") spots.Add((0, cz));
                else spots.Add((xMax, cz));
                break;

            case "front_corners":
                if (face == "south" || face == "north")
                {
                    int fz = face == "south" ? zMax : 0;
                    spots.Add((0, fz));
                    spots.Add((xMax, fz));
                }
                else
                {
                    int fx = face == "east" ? xMax : 0;
                    spots.Add((fx, 0));
                    spots.Add((fx, zMax));
                }
                break;

            case "four_corners":
                spots.Add((0, 0));
                spots.Add((xMax, 0));
                spots.Add((0, zMax));
                spots.Add((xMax, zMax));
                break;

            default: // "front"
                if (face == "south") spots.Add((cx, zMax));
                else if (face == "north") spots.Add((cx, 0));
                else if (face == "east") spots.Add((xMax, cz));
                else spots.Add((0, cz));
                break;
        }

        foreach (var (rx0, rz0) in spots)
        {
            int x0 = Clamp(rx0, 0, xMax);
            int z0 = Clamp(rz0, 0, zMax);
            int x1 = x0 + s - 1, z1 = z0 + s - 1;

            // 平面マスクから外れる位置（L字の欠けの上など）には立てない。宙抜けを防ぐ。
            bool inMask = true;
            for (int x = x0; x <= x1 && inMask; x++)
                for (int z = z0; z <= z1; z++)
                    if (!foot.Contains((x, z))) { inMask = false; break; }
            if (!inMask) continue;

            // 内側は吹き抜け。屋根・中間床・パラペットが塔の中に残ると頂部と二重になるので抜く。
            for (int x = x0 + 1; x <= x1 - 1; x++)
                for (int z = z0 + 1; z <= z1 - 1; z++)
                    for (int y = 1; y <= topY; y++)
                        cells.Remove((x, y, z));

            // 四周の壁。y=1 から上端まで塞ぐ。
            for (int y = 1; y <= topY; y++)
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (x != x0 && x != x1 && z != z0 && z != z1) continue;
                        cells[(x, y, z)] = tower;
                    }

            // 鐘楼の開口。上端の2段だけ四面の中央を抜く。建物の壁の高さより下は抜かない。
            if (spec.TowerBelfry && th >= 4)
            {
                int bm = s / 2;
                for (int y = topY - 2; y <= topY - 1; y++)
                {
                    if (y <= h - 1) continue;
                    cells.Remove((x0 + bm, y, z0));
                    cells.Remove((x0 + bm, y, z1));
                    cells.Remove((x0, y, z0 + bm));
                    cells.Remove((x1, y, z0 + bm));
                }
            }

            // 足元の入口。塔が正面側の外周に接しているときだけ、その面の中央を抜く。
            bool touchFront =
                (face == "south" && z1 == d - 1) ||
                (face == "north" && z0 == 0) ||
                (face == "east" && x1 == w - 1) ||
                (face == "west" && x0 == 0);
            if (touchFront)
            {
                int doorW = s >= 5 ? 3 : 1;
                int doorH = Clamp(h - 2, 2, 4);
                int mx = x0 + s / 2, mz = z0 + s / 2;
                for (int y = 1; y <= doorH; y++)
                    for (int o = -(doorW / 2); o <= doorW / 2; o++)
                    {
                        if (face == "south") cells.Remove((mx + o, y, z1));
                        else if (face == "north") cells.Remove((mx + o, y, z0));
                        else if (face == "east") cells.Remove((x1, y, mz + o));
                        else cells.Remove((x0, y, mz + o));
                    }
            }

            BuildTowerCap(cells, x0, z0, s, topY, cap, roof);
        }
    }

    // 塔の頂部。spire=尖塔（2段ごとに1マス絞る）、dome=丸屋根、flat=陸屋根。
    // どの形でも最初に s×s を全面へ敷き、塔の吹き抜けを確実に塞ぐ。
    private static void BuildTowerCap(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int z0, int s, int topY, string cap, string roof)
    {
        int x1 = x0 + s - 1, z1 = z0 + s - 1;

        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                cells[(x, topY + 1, z)] = roof;

        if (cap == "flat") return;

        if (cap == "dome")
        {
            // 半球。段ごとの水平半径を球の式で求め、円板を敷く（中実なので穴が空かない）。
            double r = (s - 1) / 2.0;
            double ccx = x0 + r, ccz = z0 + r;
            int hr = Math.Max(2, (int)Math.Round(r));
            for (int k = 1; k <= hr; k++)
            {
                double t = (double)k / hr;
                double rr = r * Math.Sqrt(Math.Max(0.0, 1.0 - t * t));
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        double dx = x - ccx, dz = z - ccz;
                        if (dx * dx + dz * dz <= (rr + 0.5) * (rr + 0.5))
                            cells[(x, topY + 1 + k, z)] = roof;
                    }
            }
            return;
        }

        // 尖塔: 2段ごとに全周を1マスずつ内側へ絞る。1段ごとに絞る四角錐より鋭く伸びる。
        for (int k = 1; ; k++)
        {
            int inset = k / 2;
            int ax0 = x0 + inset, ax1 = x1 - inset;
            int az0 = z0 + inset, az1 = z1 - inset;
            if (ax0 > ax1 || az0 > az1) break;
            for (int x = ax0; x <= ax1; x++)
                for (int z = az0; z <= az1; z++)
                    cells[(x, topY + 1 + k, z)] = roof;
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

    // ファサード型（temple）: 指定された面(facadeFace)に柱廊、その奥に壁の部屋。
    // 柱は建物範囲内に収める（張り出さない）。柱廊と部屋の間に gap マスの空きを設け、
    // 柱廊側の壁の中央に縦2マスの入口を空ける。
    private static void BuildTemple(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, string wall, string col, string facadeFace)
    {
        int rByHeight = h < 10 ? 1 : (h < 18 ? 2 : 3);
        int r = Math.Max(1, rByHeight);

        int yTop = h - 2;
        if (yTop < 1) yTop = 1;
        int step = Math.Max(4, r * 2 + 3);

        int gap = r * 2 + 1;

        string face = (facadeFace ?? "south").Trim().ToLowerInvariant();
        bool frontAlongX = (face == "south" || face == "north");

        if (frontAlongX)
        {
            r = Math.Min(r, Math.Max(1, w / 5));
            int lo = r, hi = w - 1 - r;
            if (hi < lo) hi = lo;

            int rzLo, rzHi, doorZ;
            if (face == "south")
            {
                rzLo = 0;
                rzHi = Math.Max(rzLo + 1, d - 1 - gap - 1);
                doorZ = rzHi; // 柱廊に面した壁
            }
            else
            {
                rzHi = d - 1;
                rzLo = Math.Min(rzHi - 1, gap + 1);
                doorZ = rzLo; // 柱廊に面した壁
            }

            for (int y = 1; y <= h - 2; y++)
                for (int x = 0; x < w; x++)
                    for (int z = rzLo; z <= rzHi; z++)
                        if (x == 0 || x == w - 1 || z == rzLo || z == rzHi)
                            cells[(x, y, z)] = wall;

            // 入口: 柱廊側の壁(z=doorZ)の中央に縦2マスの開口。
            int doorX = w / 2;
            cells.Remove((doorX, 1, doorZ));
            if (h - 2 >= 2) cells.Remove((doorX, 2, doorZ));

            int frontZ = (face == "south") ? d - 1 - r : r;
            if (frontZ < 0) frontZ = 0;
            if (frontZ > d - 1) frontZ = d - 1;

            foreach (int cxp in AxisPositions(lo, hi, step))
                BuildColumn(cells, cxp, frontZ, r, 1, yTop, col, w, d);
        }
        else
        {
            r = Math.Min(r, Math.Max(1, d / 5));
            int lo = r, hi = d - 1 - r;
            if (hi < lo) hi = lo;

            int rxLo, rxHi, doorX2;
            if (face == "east")
            {
                rxLo = 0;
                rxHi = Math.Max(rxLo + 1, w - 1 - gap - 1);
                doorX2 = rxHi;
            }
            else
            {
                rxHi = w - 1;
                rxLo = Math.Min(rxHi - 1, gap + 1);
                doorX2 = rxLo;
            }

            for (int y = 1; y <= h - 2; y++)
                for (int x = rxLo; x <= rxHi; x++)
                    for (int z = 0; z < d; z++)
                        if (z == 0 || z == d - 1 || x == rxLo || x == rxHi)
                            cells[(x, y, z)] = wall;

            // 入口: 柱廊側の壁(x=doorX2)の中央に縦2マスの開口。
            int doorZ2 = d / 2;
            cells.Remove((doorX2, 1, doorZ2));
            if (h - 2 >= 2) cells.Remove((doorX2, 2, doorZ2));

            int frontX = (face == "east") ? w - 1 - r : r;
            if (frontX < 0) frontX = 0;
            if (frontX > w - 1) frontX = w - 1;

            foreach (int czp in AxisPositions(lo, hi, step))
                BuildColumn(cells, frontX, czp, r, 1, yTop, col, w, d);
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
