using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 船(structure_type="ship")を確定的に座標へ展開する専用ビルダー。
// StructureExpander.ExpandCore の ship 分岐から呼ばれる。
// 床/壁/屋根/開口部・入口保証は一切通さず、ここで船体・甲板・上部構造物・出入口を作る。
//
// 座標系（共通骨格）:
//   x = 船の幅（左舷 0 .. 右舷 w-1）
//   z = 船の長さ（船首・船尾方向）。bow_face="north" なら z=0 が船首、"south" なら z=d-1 が船首。
//   y = 高さ（0 が船底、上へ）。
// 船体は下すぼまり（船底を左右から絞る）＋船首テーパー（船首側を数マス絞って尖らせる）。
// その上に甲板を張り、船種ごとの上部構造物・マスト・出入口を載せる。
public static class ShipExpander
{
    // 全船種の候補（自動選択用）。
    private static readonly string[] AllClasses =
    {
        "rowboat", "motorboat", "trawler", "caravel", "galleon",
        "liner", "cargo", "destroyer", "battleship", "carrier", "submarine"
    };

    public static List<GeneratedBlock> Build(
        StructureSpec spec, int w, int d, int h,
        IReadOnlyList<string> allowedBlocks, string fallback)
    {
        // 素材決定（未指定は wall_block → fallback の順で流用）。
        string wall = Pick(spec.WallBlock, allowedBlocks, fallback);
        string hull = Pick(spec.HullBlock ?? spec.WallBlock, allowedBlocks, wall);
        string deck = Pick(spec.DeckBlock ?? spec.FloorBlock ?? spec.WallBlock, allowedBlocks, wall);
        string sup = Pick(spec.SuperstructureBlock ?? spec.WallBlock, allowedBlocks, wall);
        string glass = Pick("minecraft:glass", allowedBlocks, "minecraft:glass");

        // 船首の向き。既定は north（z=0 側が船首）。
        bool bowNorth = (spec.BowFace ?? "north").Trim().ToLowerInvariant() != "south";

        // 船種の決定。指定があればそれ、なければサイズ帯から確定的乱数で選ぶ。
        string shipClass = ResolveShipClass(spec, w, d, h);

        // 座標 -> ブロックID。後勝ち（上部構造物が甲板を上書きする）。
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 船底・船体・甲板の共通骨格を作る。甲板の高さ(deckY)を返す。
        int deckY = BuildHull(cells, w, d, h, hull, deck, bowNorth, shipClass);

        // 船種ごとの上部構造物・マスト・出入口。
        switch (shipClass)
        {
            case "rowboat": BuildRowboat(cells, w, d, h, deckY, hull, deck, bowNorth); break;
            case "motorboat": BuildMotorboat(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "trawler": BuildTrawler(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "caravel": BuildCaravel(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "galleon": BuildGalleon(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "liner": BuildLiner(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "cargo": BuildCargo(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "destroyer": BuildDestroyer(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "battleship": BuildBattleship(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "carrier": BuildCarrier(cells, w, d, h, deckY, sup, glass, deck, bowNorth); break;
            case "submarine": BuildSubmarine(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            default: BuildMotorboat(cells, w, d, h, deckY, sup, glass, bowNorth); break;
        }

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

    // ===== 船種の自動決定 =====
    // ship_class 指定があればそれを尊重。無ければサイズ帯で候補を絞り、シードで1つ選ぶ。
    private static string ResolveShipClass(StructureSpec spec, int w, int d, int h)
    {
        string? given = spec.ShipClass?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(given) && AllClasses.Contains(given))
            return given;

        // サイズ帯で候補を絞る。d(船の長さ)を主基準に、w(幅)・h(高さ)で補正。
        List<string> pool;
        if (d <= 6)
            pool = new() { "rowboat", "motorboat" };
        else if (d <= 12)
            pool = new() { "motorboat", "trawler", "caravel", "destroyer" };
        else if (d <= 20)
            pool = new() { "trawler", "caravel", "galleon", "destroyer", "cruiser_fallback" };
        else if (d <= 32)
            pool = new() { "galleon", "liner", "cargo", "destroyer", "battleship" };
        else
            pool = new() { "liner", "cargo", "battleship", "carrier" };

        // 潜水艦は「細長く低い」ときだけ候補に足す（幅が狭く高さが低い）。
        if (d >= 10 && w <= Math.Max(3, d / 4) && h <= Math.Max(3, d / 4))
            pool.Add("submarine");

        // 実在しない保険値を除去（cruiser_fallback は destroyer に寄せる）。
        pool = pool.Select(c => c == "cruiser_fallback" ? "destroyer" : c)
                   .Where(AllClasses.Contains).Distinct().ToList();
        if (pool.Count == 0) pool.Add("motorboat");

        // シード（0なら寸法から確定的に導く）で候補から1つ選ぶ。
        int seed = spec.ShipSeed != 0 ? spec.ShipSeed : (w * 73856093) ^ (d * 19349663) ^ (h * 83492791);
        int idx = Math.Abs(seed) % pool.Count;
        return pool[idx];
    }

    // ===== 共通骨格: 船底・船体・甲板 =====
    // 下すぼまり（船底1〜2マスを左右から絞る）＋船首テーパー（船首側を絞って尖らせる）。
    // 甲板の高さ deckY を返す。deckY より下は中身も詰めず、外殻＋甲板だけ作る（軽量）。
    private static int BuildHull(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, string hull, string deck, bool bowNorth, string shipClass)
    {
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

        // 船首テーパーの長さ（船の長さの約4割、最低2）。長く取って先端を鋭く絞る。
        int bowLen = Clamp((int)Math.Round(d * 0.4), 2, Math.Max(2, d - 1));
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

    // ===== 上部構造物の箱を置く汎用ヘルパー =====
    // (x0..x1, z0..z1) の範囲に、甲板の上(yFrom)から高さ ht の中空の箱（壁＋天井）を作る。
    // 各 z 断面ごとに船体の左右の縁を調べ、その内側にある柱だけに積む（はみ出さず、痩せもしない）。
    private static void AddSuperstructure(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int yFrom, int ht, int w, int d, string block)
    {
        x0 = Clamp(x0, 0, w - 1); x1 = Clamp(x1, 0, w - 1);
        z0 = Clamp(z0, 0, d - 1); z1 = Clamp(z1, 0, d - 1);
        if (x1 < x0 || z1 < z0 || ht <= 0) return;

        int yTop = yFrom + ht - 1;
        for (int z = z0; z <= z1; z++)
        {
            // この z 断面で船体/甲板セルがある x の左端・右端を求める。
            int hullLo = int.MaxValue, hullHi = int.MinValue;
            for (int hx = 0; hx < w; hx++)
                if (cells.ContainsKey((hx, yFrom - 1, z)))
                {
                    if (hx < hullLo) hullLo = hx;
                    if (hx > hullHi) hullHi = hx;
                }
            if (hullHi < hullLo) continue; // この断面に船体が無ければ積まない。

            // 指定範囲と船体幅の共通部分に上部構造を積む。
            int ax0 = Math.Max(x0, hullLo);
            int ax1 = Math.Min(x1, hullHi);
            if (ax1 < ax0) continue;

            for (int y = yFrom; y <= yTop; y++)
                for (int x = ax0; x <= ax1; x++)
                {
                    bool shell = x == ax0 || x == ax1 || z == z0 || z == z1 || y == yTop;
                    if (shell) cells[(x, y, z)] = block;
                }
        }
    }

    // 上部構造物の指定面の中央にドア（縦2マス）を開ける。
    // face: "north"(z0側) "south"(z1側) "east"(x1側) "west"(x0側)
    private static void AddDoor(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int yFrom, string face)
    {
        int cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        int dx, dz;
        switch (face)
        {
            case "north": dx = cx; dz = z0; break;
            case "south": dx = cx; dz = z1; break;
            case "west": dx = x0; dz = cz; break;
            default: dx = x1; dz = cz; break; // east
        }
        cells.Remove((dx, yFrom, dz));
        cells.Remove((dx, yFrom + 1, dz));
    }

    // 上部構造物の側面（左右舷=x0/x1面）に窓を並べる。glass で埋める（穴ではなくガラス）。
    private static void AddWindows(
        Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int z0, int z1, int yFrom, int ht, string glass)
    {
        int wy = yFrom + Math.Max(0, ht / 2); // 窓の高さ（中ほど）
        for (int z = z0 + 1; z <= z1 - 1; z += 2)
        {
            if (cells.ContainsKey((x0, wy, z))) cells[(x0, wy, z)] = glass;
            if (cells.ContainsKey((x1, wy, z))) cells[(x1, wy, z)] = glass;
        }
    }

    // マストを1本立てる（甲板の上へ垂直の柱）。
    private static void AddMast(
        Dictionary<(int x, int y, int z), string> cells,
        int x, int z, int yFrom, int height, int w, int d, string block)
    {
        if (x < 0 || x >= w || z < 0 || z >= d) return;
        for (int y = yFrom; y < yFrom + height; y++)
            cells[(x, y, z)] = block;
    }

    // 甲板の両縁に高さ1の手すり（開放甲板の縁取り）。
    private static void AddRail(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int deckY, int zFrom, int zTo, string block)
    {
        for (int z = Clamp(zFrom, 0, d - 1); z <= Clamp(zTo, 0, d - 1); z++)
            for (int x = 0; x < w; x++)
            {
                bool edge = x == 0 || x == w - 1;
                if (edge && cells.ContainsKey((x, deckY, z)))
                    cells[(x, deckY + 1, z)] = block;
            }
    }

    // 船首・船尾の位置を返すヘルパー（z座標）。
    private static int BowZ(int d, bool bowNorth) => bowNorth ? 0 : d - 1;
    private static int SternZ(int d, bool bowNorth) => bowNorth ? d - 1 : 0;

    // 船尾寄り・船首寄りの帯の z 範囲を返す（frac は船尾/船首からの割合 0..1）。
    private static (int lo, int hi) SternBand(int d, bool bowNorth, double frac)
    {
        int len = Math.Max(1, (int)Math.Round(d * frac));
        return bowNorth ? (d - len, d - 1) : (0, len - 1);
    }
    private static (int lo, int hi) BowBand(int d, bool bowNorth, double frac)
    {
        int len = Math.Max(1, (int)Math.Round(d * frac));
        return bowNorth ? (0, len - 1) : (d - len, d - 1);
    }

    // ================= 船種別ビルダー =================

    // rowboat: 最小。開放船体＋オール受け（縁の手すりだけ）。上部構造物なし。
    private static void BuildRowboat(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string hull, string deck, bool bowNorth)
    {
        AddRail(cells, w, d, deckY, 0, d - 1, hull);
    }

    // motorboat: 低船体＋船尾寄りに小さな操縦席（風防つき）。前は開放甲板。
    private static void BuildMotorboat(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, bool bowNorth)
    {
        var (lo, hi) = SternBand(d, bowNorth, 0.35);
        int x0 = 1, x1 = w - 2;
        int ht = Math.Max(1, Math.Min(2, h - deckY - 1));
        AddSuperstructure(cells, x0, x1, lo, hi, deckY + 1, ht, w, d, sup);
        // 前向き（船首側）に風防＝窓。船首向きの面にドアも。
        string bowFace = bowNorth ? "north" : "south";
        AddDoor(cells, x0, x1, lo, hi, deckY + 1, bowFace);
        AddWindows(cells, x0, x1, lo, hi, deckY + 1, ht, glass);
        AddRail(cells, w, d, deckY, 0, d - 1, sup);
    }

    // trawler: 船首寄りに背の高い操舵室＋マスト、船尾は開放作業甲板。
    private static void BuildTrawler(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        var (blo, bhi) = BowBand(d, bowNorth, 0.35);
        int x0 = 1, x1 = w - 2;
        int ht = Math.Max(2, h - deckY - 1);
        AddSuperstructure(cells, x0, x1, blo, bhi, deckY + 1, ht, w, d, sup);
        // 操舵室の船尾向き面にドア、左右に窓、船首向きに窓。
        string sternFace = bowNorth ? "south" : "north";
        AddDoor(cells, x0, x1, blo, bhi, deckY + 1, sternFace);
        AddWindows(cells, x0, x1, blo, bhi, deckY + 1, ht, glass);
        // マスト（操舵室の少し後ろに1本）。
        int mz = bowNorth ? bhi + 1 : blo - 1;
        AddMast(cells, w / 2, Clamp(mz, 0, d - 1), deckY + 1, Math.Max(2, h - deckY), w, d, hull);
        // 船尾側は開放作業甲板＝手すり。
        var (slo, shi) = SternBand(d, bowNorth, 0.6);
        AddRail(cells, w, d, deckY, slo, shi, hull);
    }

    // caravel: 船尾楼＋2〜3本マスト。
    private static void BuildCaravel(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        var (lo, hi) = SternBand(d, bowNorth, 0.3);
        int x0 = 1, x1 = w - 2;
        int ht = Math.Max(2, (h - deckY) * 2 / 3);
        AddSuperstructure(cells, x0, x1, lo, hi, deckY + 1, ht, w, d, sup);
        string bowFace = bowNorth ? "north" : "south";
        AddDoor(cells, x0, x1, lo, hi, deckY + 1, bowFace);
        AddWindows(cells, x0, x1, lo, hi, deckY + 1, ht, glass);
        // マスト2〜3本を船の長さに沿って等間隔。
        int mastH = Math.Max(3, h - deckY + 2);
        foreach (int mz in AxisPositions(2, d - 3, Math.Max(3, d / 3)))
            AddMast(cells, w / 2, mz, deckY + 1, mastH, w, d, hull);
        AddRail(cells, w, d, deckY, 0, d - 1, hull);
    }

    // galleon: 高い船尾楼＋やや低く細い船首楼＋3〜4本マスト＋砲門（舷側の窓列）。
    private static void BuildGalleon(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // 船首は細いので、船首楼は左右を1マスずつ絞る（船体からはみ出さない）。
        int bx0 = Math.Min(w / 2, x0 + 1);
        int bx1 = Math.Max(w / 2, x1 - 1);
        int ht = Math.Max(2, (h - deckY) * 3 / 4);

        // 船尾楼（高く・全幅）。
        var (slo, shi) = SternBand(d, bowNorth, 0.28);
        AddSuperstructure(cells, x0, x1, slo, shi, deckY + 1, ht, w, d, sup);
        AddWindows(cells, x0, x1, slo, shi, deckY + 1, ht, glass);

        // 船首楼（低く・細く・船首寄り）。
        var (blo, bhi) = BowBand(d, bowNorth, 0.16);
        int bowHt = Math.Max(1, ht - 2);
        AddSuperstructure(cells, bx0, bx1, blo, bhi, deckY + 1, bowHt, w, d, sup);

        // 砲門＝船体舷側に窓列。
        int gy = Math.Max(1, deckY - 1);
        for (int z = 3; z < d - 3; z += 2)
        {
            if (cells.ContainsKey((0, gy, z))) cells[(0, gy, z)] = glass;
            if (cells.ContainsKey((w - 1, gy, z))) cells[(w - 1, gy, z)] = glass;
        }

        // マスト3〜4本。
        int mastH = Math.Max(4, h - deckY + 3);
        foreach (int mz in AxisPositions(3, d - 4, Math.Max(3, d / 4)))
            AddMast(cells, w / 2, mz, deckY + 1, mastH, w, d, hull);
        AddRail(cells, w, d, deckY, 0, d - 1, hull);
    }

    // liner: 多層の上部構造物＋煙突＋舷側の窓列。
    private static void BuildLiner(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // 上部構造物を船の中央 6割に、多層（段々に短く）で積む。
        int avail = h - deckY - 1;
        int layers = Clamp(avail, 1, 4);
        for (int i = 0; i < layers; i++)
        {
            double frac = 0.6 - i * 0.1;
            int len = Math.Max(1, (int)Math.Round(d * frac));
            int lo = (d - len) / 2, hi = lo + len - 1;
            AddSuperstructure(cells, x0, x1, lo, hi, deckY + 1 + i, 1 + (avail / layers), w, d, sup);
        }
        // 煙突（中央やや後ろに1〜2本）。
        var (slo, shi) = SternBand(d, bowNorth, 0.5);
        int funnelZ = (slo + shi) / 2;
        AddMast(cells, w / 2, funnelZ, deckY + 1, Math.Max(2, avail), w, d, sup);
        // 舷側の窓列（客室の丸窓）。
        int wy = deckY;
        for (int z = 2; z < d - 2; z += 2)
        {
            if (cells.ContainsKey((0, wy, z))) cells[(0, wy, z)] = glass;
            if (cells.ContainsKey((w - 1, wy, z))) cells[(w - 1, wy, z)] = glass;
        }
        // 乗降口ドア（中央舷側）。
        int mid = d / 2;
        cells.Remove((0, deckY, mid));
        cells.Remove((w - 1, deckY, mid));
    }

    // cargo: 船尾寄りに高いブリッジ＋長い平甲板（コンテナ/タンク見立て）。
    private static void BuildCargo(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // 船尾ブリッジ（高い塊）。
        var (slo, shi) = SternBand(d, bowNorth, 0.2);
        int ht = Math.Max(2, h - deckY - 1);
        AddSuperstructure(cells, x0, x1, slo, shi, deckY + 1, ht, w, d, sup);
        string bowFace = bowNorth ? "north" : "south";
        AddDoor(cells, x0, x1, slo, shi, deckY + 1, bowFace);
        AddWindows(cells, x0, x1, slo, shi, deckY + 1, ht, glass);
        // 前方の長い平甲板にコンテナ/タンクの低い列（1〜2段の箱）。
        var (blo, bhi) = BowBand(d, bowNorth, 0.7);
        int stackH = Math.Max(1, Math.Min(2, (h - deckY) / 2));
        for (int z = Math.Min(blo, bhi) + 1; z <= Math.Max(blo, bhi) - 1; z += 2)
            AddSuperstructure(cells, x0, x1, z, z, deckY + 1, stackH, w, d, sup);
    }

    // destroyer: 細身・鋭い船首・中央前寄りブリッジ＋マスト。
    private static void BuildDestroyer(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // ブリッジ（中央やや船首寄り、コンパクト）。
        var (blo, bhi) = BowBand(d, bowNorth, 0.45);
        int len = Math.Max(2, (bhi - blo) / 2);
        int lo = bowNorth ? bhi - len : blo;
        int hi = lo + len;
        int ht = Math.Max(2, h - deckY - 1);
        AddSuperstructure(cells, x0, x1, lo, hi, deckY + 1, ht, w, d, sup);
        AddWindows(cells, x0, x1, lo, hi, deckY + 1, ht, glass);
        string sternFace = bowNorth ? "south" : "north";
        AddDoor(cells, x0, x1, lo, hi, deckY + 1, sternFace);
        // マスト1本（ブリッジ後方）。
        int mz = bowNorth ? hi + 1 : lo - 1;
        AddMast(cells, w / 2, Clamp(mz, 0, d - 1), deckY + 1, Math.Max(2, h - deckY), w, d, sup);
    }

    // battleship: 幅広・重厚な上部構造物＋主砲塔（前後の低い箱）。
    private static void BuildBattleship(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        int x0 = 1, x1 = w - 2;
        // 中央の重厚な艦橋。
        int clen = Math.Max(3, d / 4);
        int clo = (d - clen) / 2, chi = clo + clen - 1;
        int ht = Math.Max(3, h - deckY - 1);
        AddSuperstructure(cells, x0, x1, clo, chi, deckY + 1, ht, w, d, sup);
        AddWindows(cells, x0, x1, clo, chi, deckY + 1, ht, glass);
        string sternFace = bowNorth ? "south" : "north";
        AddDoor(cells, x0, x1, clo, chi, deckY + 1, sternFace);
        // 主砲塔（前後に低い箱＋短い砲身）。
        int tW0 = w / 2 - 1, tW1 = w / 2 + 1;
        var (blo, bhi) = BowBand(d, bowNorth, 0.18);
        var (slo, shi) = SternBand(d, bowNorth, 0.18);
        AddSuperstructure(cells, tW0, tW1, blo, bhi, deckY + 1, 1, w, d, sup);
        AddSuperstructure(cells, tW0, tW1, slo, shi, deckY + 1, 1, w, d, sup);
    }

    // carrier: 全通平甲板＋右舷アイランド（島型構造物）。
    private static void BuildCarrier(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string deck, bool bowNorth)
    {
        // 全通平甲板を全面に張り直す（既に BuildHull で甲板はある）。
        for (int z = 0; z < d; z++)
            for (int x = 0; x < w; x++)
                if (cells.ContainsKey((x, deckY, z)))
                    cells[(x, deckY, z)] = deck;
        // 右舷（x=w-1側）に寄せた小さな島型構造物（中央やや後ろ）。
        int ix0 = w - 2, ix1 = w - 1;
        var (slo, shi) = SternBand(d, bowNorth, 0.45);
        int ilen = Math.Max(2, (shi - slo) / 2);
        int lo = (slo + shi) / 2 - ilen / 2, hi = lo + ilen;
        int ht = Math.Max(2, h - deckY - 1);
        AddSuperstructure(cells, ix0, ix1, lo, hi, deckY + 1, ht, w, d, sup);
        AddWindows(cells, ix0, ix1, lo, hi, deckY + 1, ht, glass);
        AddDoor(cells, ix0, ix1, lo, hi, deckY + 1, "west"); // 甲板側へ出るドア
    }

    // submarine: 葉巻型（上面を丸めた船体）＋司令塔(sail)1基。
    private static void BuildSubmarine(
        Dictionary<(int x, int y, int z), string> cells,
        int w, int d, int h, int deckY, string sup, string glass, string hull, bool bowNorth)
    {
        // 上面を丸める: 最上段の左右端を1マス削って蒲鉾型にする。
        for (int z = 1; z < d - 1; z++)
        {
            if (cells.ContainsKey((0, deckY, z))) cells.Remove((0, deckY, z));
            if (cells.ContainsKey((w - 1, deckY, z))) cells.Remove((w - 1, deckY, z));
        }
        // 司令塔（中央に小さな塔＋ハッチ）。
        int clo = d / 2 - 1, chi = d / 2 + 1;
        int cx0 = Math.Max(0, w / 2 - 1), cx1 = Math.Min(w - 1, w / 2 + 1);
        int ht = Math.Max(1, Math.Min(2, h - deckY - 1));
        AddSuperstructure(cells, cx0, cx1, clo, chi, deckY + 1, ht, w, d, sup);
        // ハッチ（司令塔上面中央を1マス開ける）。
        cells.Remove((w / 2, deckY + ht, d / 2));
    }

    // ================= 小ヘルパー（StructureExpander と独立に持つ） =================

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

    // lo..hi を step 間隔で並べた位置リスト（両端を含む）。マスト等の等間隔配置用。
    private static List<int> AxisPositions(int lo, int hi, int step)
    {
        var list = new List<int>();
        if (hi <= lo) { list.Add(Clamp(lo, 0, int.MaxValue)); return list; }
        int span = hi - lo;
        int segments = Math.Max(1, span / Math.Max(1, step));
        for (int i = 0; i <= segments; i++)
        {
            int v = lo + (int)Math.Round((double)span * i / segments);
            if (list.Count == 0 || list.Last() != v) list.Add(v);
        }
        return list;
    }
}
