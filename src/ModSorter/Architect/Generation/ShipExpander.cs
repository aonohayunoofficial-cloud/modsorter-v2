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
//
// ファイル分割（partial）:
//   ShipExpander.cs       … 入口(Build) / 船種決定 / 共通骨格(BuildHull)
//   ShipExpander.Parts.cs … 部品ヘルパー（上部構造物・ドア・窓・マスト・砲塔・手すり）と座標/小ヘルパー
//   ShipExpander.Civil.cs … 民間船（motorboat/trawler/caravel/galleon/liner/cargo）
//   ShipExpander.Naval.cs … 軍艦（destroyer/frigate/cruiser/battleship/carrier/submarine）
public static partial class ShipExpander
{
    // 全船種の候補（自動選択用）。
    private static readonly string[] AllClasses =
    {
        "motorboat", "trawler", "caravel", "galleon",
        "liner", "cargo", "destroyer", "battleship", "carrier", "submarine",
        "cruiser", "frigate"
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
            case "motorboat": BuildMotorboat(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "trawler": BuildTrawler(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "caravel": BuildCaravel(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "galleon": BuildGalleon(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "liner": BuildLiner(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "cargo": BuildCargo(cells, w, d, h, deckY, sup, glass, bowNorth); break;
            case "destroyer": BuildDestroyer(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "frigate": BuildFrigate(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
            case "cruiser": BuildCruiser(cells, w, d, h, deckY, sup, glass, hull, bowNorth); break;
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
            pool = new() { "motorboat" };
        else if (d <= 12)
            pool = new() { "motorboat", "trawler", "caravel", "destroyer", "frigate" };
        else if (d <= 20)
            pool = new() { "trawler", "caravel", "galleon", "destroyer", "frigate", "cruiser" };
        else if (d <= 32)
            pool = new() { "galleon", "liner", "cargo", "destroyer", "cruiser", "battleship" };
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
