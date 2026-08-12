using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 鉄道（structure_type="railway:<種類>"）の座標生成。
// harbor / airport と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・開口部・
// 入口保証・フットプリントマスクは一切通らない。既存の小分類には影響しない。
// 座標ヘルパー（Fill/Rotate/Normalize/Pick）は HarborExpander・AirportExpander でも
// それぞれ private に閉じているため、このクラスも自前で持つ。
//
// ===== 寸法の扱い =====
// 1マス=1m。空港の建物系（管制塔・ターミナル・格納庫）と同じ縮尺なので、
// 並べて置いても寸法が食い違わない。
//
// ===== 実寸の出典 =====
//   ホーム高さ   … レール面上 1100mm（電車専用）/ 920mm（電車・客車共用）/
//                  1250mm（新幹線）。1マス=1m なのでどれも 1 マスに落ちる。
//   ホーム縁端   … 軌道中心から 1475mm（JR 在来線）。中心から 2 マス目を縁端にすると
//                  1.5m 相当になり実寸に最も近い。
//   ホーム幅     … 島式 3.0m 以上・相対式 2.0m 以上（整備計画の下限）。
//                  実際の通勤駅は 5〜10m 級。
//   点状ブロック … 縁端警告は縁端から 80cm 以上離す。1 マス内側で約 1m 相当。
//   ホーム長     … 20m 車 × 両数。10 両で 200m 強。
//   軌道中心間隔 … 在来線 4.0m、新幹線 4.3m。駅部は広げる。
//   ホームドア   … 20m 車 4 扉＝およそ 5m 間隔に開口が来る。
//   建築限界     … 直流電化で高さ 5700mm、幅 3800mm（軌道中心から左右 1900mm）。
//                  上屋・跨線橋はこの断面を侵さない位置に建てる（別の小分類で使う）。
//   高架橋       … ラーメン高架橋の柱スパンは 8〜10m 級。
//   軌間         … 1067mm / 1435mm。1マス=1m では 1 マス未満なので 2 本のレールには
//                  分けられない。加えて minecraft:rail は隣接から shape が決まる機能
//                  ブロックで、shape を書かない生成物では曲線状態を拾って斜めに描かれる。
//                  よって軌道はレールを置かず道床だけで表す。敷設は実機側で行う。
//
// 断面は「線路が z 方向に走る」向きで組み、最後に Rotate で facade_face の向きへ回す。
// 座標は負へ出るが Normalize が 0 起点へ寄せる。
//
// StructureSpec との対応（プラットフォーム）。
//   width=ホーム幅 / depth=ホーム長 / height=レール面からホーム天端までの高さ
//   rail_platform_type … "island" | "side" | "opposed"
//   rail_track_pitch … 相対式の2線の軌道中心間隔 / rail_track_margin … 線路の延長
//   rail_platform_door … ホームドアの高さ / rail_tactile … 点状ブロック
//   rail_end_ramp … ホーム端の勾配 / rail_viaduct … 高架 / rail_pier_step … 橋脚の間隔
//   floor_block=天端 / accent_block=縁端の白線 / base_block=躯体・柱・橋脚
//   wall_block=点状ブロック・壁 / tower_block=道床 / seat_block=照明・小物
//   veranda_block=採光帯・シャッター / parapet_block=柵・手すり・ホームドア
//   roof_block=屋根・床版
//
// 部品は partial の別ファイルに分けてある。
//   RailwayExpander.Roof.cs     屋根形状の共通ヘルパー（上屋・跨線橋・車庫で共用）
//   RailwayExpander.Canopy.cs   ホーム上屋
//   RailwayExpander.Overpass.cs 跨線橋
//   RailwayExpander.Depot.cs    車庫
public static partial class RailwayExpander
{
    public const string Prefix = "railway:";

    // 軌道中心からホーム縁端までのマス数。実寸 1.475m に対し 2 マス目＝1.5m 相当。
    private const int EdgeOffset = 2;

    // 道床の片側の張り出し（マス）。実寸の道床肩は片側 1.7m 級。
    private const int BallastHalf = 1;

    // ホームドアの開口周期（マス）と開口幅。20m 車 4 扉＝およそ 5m 間隔。
    private const int DoorStep = 5;
    private const int DoorOpen = 2;

    // StructureExpander から呼ぶ判定。"railway:" で始まる structure_type だけを受け持つ。
    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "platform_canopy":
            case "canopy": return "platform_canopy";
            case "overpass":
            case "footbridge": return "overpass";
            case "depot":
            case "car_shed": return "depot";
            case "platform":
            default: return "platform";
        }
    }

    private static string TypeOf(string? type) => (type ?? "island").Trim().ToLowerInvariant() switch
    {
        "side" => "side",
        "opposed" => "opposed",
        _ => "island",
    };

    private sealed class Palette
    {
        public readonly string Pave, Edge, Body, Tactile, Ballast, Fence, Girder, Trim, Glass;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Pave = Pick(spec.FloorBlock, allowed, fallback);
            Edge = Pick(spec.AccentBlock, allowed, Pave);
            Body = Pick(spec.BaseBlock, allowed, Pave);
            Tactile = Pick(spec.WallBlock, allowed, Edge);
            Ballast = Pick(spec.TowerBlock, allowed, Body);
            Fence = Pick(spec.ParapetBlock, allowed, Edge);
            Girder = Pick(spec.RoofBlock, allowed, Body);
            Trim = Pick(spec.SeatBlock, allowed, Edge);
            Glass = Pick(spec.VerandaBlock, allowed, Edge);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 駅舎はここに case を足す。
        switch (KindOf(spec.StructureType))
        {
            case "platform_canopy":
                BuildCanopy(cells, spec, p);
                break;
            case "overpass":
                BuildOverpass(cells, spec, p);
                break;
            case "depot":
                BuildDepot(cells, spec, p);
                break;
            case "platform":
            default:
                BuildPlatform(cells, spec, p);
                break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }

    // ===== プラットフォーム =====
    // 断面は x 方向。中心から 2 マス目を縁端にするので、軌道中心とホームの間には
    // 必ず 1 マスの空きが残る（車両の張り出しぶん）。
    //
    // y の積み方。
    //   s     … 路盤面（道床・ホーム基礎）
    //   s+1   … レール面
    //   s+1+h … ホーム天端（h=1 でレール面上 1m ＝実物 1100mm 相当）
    private static void BuildPlatform(
        Dictionary<(int x, int y, int z), string> cells, StructureSpec spec, Palette p)
    {
        string type = TypeOf(spec.RailPlatformType);
        int w = Clamp(spec.Width, 2, 24);
        int len = Clamp(spec.Depth, 8, 256);
        int h = Clamp(spec.Height, 1, 4);
        int pitch = Clamp(spec.RailTrackPitch ?? 4, 4, 12);
        int margin = Clamp(spec.RailTrackMargin ?? 8, 0, 32);
        int doorH = Clamp(spec.RailPlatformDoor ?? 0, 0, 3);
        int pierStep = Clamp(spec.RailPierStep ?? 10, 4, 24);
        bool tactile = spec.RailTactile;
        bool endRamp = spec.RailEndRamp;

        int via = spec.RailViaduct ?? 0;
        if (via > 0) via = Clamp(via, 3, 32);   // 床版と柱が入る最小の高さ

        int s = via;                 // 路盤面
        int railY = s + 1;           // レール面
        int topY = railY + h;        // ホーム天端

        // ===== 平面の割り付け =====
        var tracks = new List<int>();
        var plats = new List<(int X0, int X1, bool TrackLeft, bool TrackRight)>();

        if (type == "island")
        {
            plats.Add((0, w - 1, true, true));
            tracks.Add(-EdgeOffset);
            tracks.Add(w - 1 + EdgeOffset);
        }
        else if (type == "opposed")
        {
            plats.Add((0, w - 1, false, true));
            int t1 = w - 1 + EdgeOffset;
            int t2 = t1 + pitch;
            tracks.Add(t1);
            tracks.Add(t2);
            int rx0 = t2 + EdgeOffset;
            plats.Add((rx0, rx0 + w - 1, true, false));
        }
        else
        {
            plats.Add((0, w - 1, false, true));
            tracks.Add(w - 1 + EdgeOffset);
        }

        int z0 = -margin;
        int z1 = len - 1 + margin;

        int xMin = Math.Min(plats.Min(t => t.X0), tracks.Min() - BallastHalf);
        int xMax = Math.Max(plats.Max(t => t.X1), tracks.Max() + BallastHalf);

        // ===== 高架橋 =====
        // 床版を全幅に張り、橋脚の位置に横梁と柱を落とす。
        if (via > 0)
        {
            Fill(cells, xMin, xMax, s - 1, s - 1, z0, z1, p.Girder);
            for (int z = z0; z <= z1; z += pierStep)
            {
                Fill(cells, xMin, xMax, s - 2, s - 2, z, z, p.Girder);
                foreach (int cx in new[] { xMin + 1, (xMin + xMax) / 2, xMax - 1 })
                    Fill(cells, cx, cx, 0, s - 3, z, z, p.Body);
            }
        }

        // ===== 道床 =====
        // レールは置かない。railY（レール面）はホーム天端の高さを決める基準としてだけ使う。
        foreach (int c in tracks)
        {
            Fill(cells, c - BallastHalf, c + BallastHalf, s, s, z0, z1, p.Ballast);
        }

        // 隣り合う線路の間にホームが無いときは道床をつなげる（線間の空きを埋める）。
        var sorted = tracks.OrderBy(t => t).ToList();
        for (int i = 0; i + 1 < sorted.Count; i++)
        {
            int a = sorted[i], b = sorted[i + 1];
            bool blocked = plats.Any(pl => pl.X1 > a && pl.X0 < b);
            if (!blocked) Fill(cells, a, b, s, s, z0, z1, p.Ballast);
        }

        // ===== ホーム =====
        foreach (var pl in plats)
        {
            for (int z = 0; z < len; z++)
            {
                int drop = endRamp ? EndDrop(z, len, h) : 0;
                int top = topY - drop;

                Fill(cells, pl.X0, pl.X1, s, top - 1, z, z, p.Body);
                Fill(cells, pl.X0, pl.X1, top, top, z, z, p.Pave);

                // 線路に面した側は縁端の白線と点状ブロック、陸側は転落防止の柵。
                if (pl.TrackRight)
                {
                    cells[(pl.X1, top, z)] = p.Edge;
                    if (tactile && pl.X1 - 1 > pl.X0) cells[(pl.X1 - 1, top, z)] = p.Tactile;
                }
                else
                {
                    cells[(pl.X1, top + 1, z)] = p.Fence;
                }

                if (pl.TrackLeft)
                {
                    cells[(pl.X0, top, z)] = p.Edge;
                    if (tactile && pl.X0 + 1 < pl.X1) cells[(pl.X0 + 1, top, z)] = p.Tactile;
                }
                else
                {
                    cells[(pl.X0, top + 1, z)] = p.Fence;
                }

                // ホームドア。勾配で下がった端部には立てない。
                if (doorH > 0 && drop == 0 && z % DoorStep >= DoorOpen)
                {
                    if (pl.TrackRight) Fill(cells, pl.X1, pl.X1, top + 1, top + doorH, z, z, p.Fence);
                    if (pl.TrackLeft) Fill(cells, pl.X0, pl.X0, top + 1, top + doorH, z, z, p.Fence);
                }
            }
        }
    }

    // ホーム端の勾配。端から 2 マスにつき 1 段上がって定高に達する。
    private static int EndDrop(int z, int len, int h)
    {
        int d = Math.Min(z, len - 1 - z);
        int drop = h - d / 2;
        if (drop < 0) return 0;
        return drop > h ? h : drop;
    }

    // ===== 共通の小物 =====
    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? want, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(want) &&
            allowed.Any(a => string.Equals(a, want, StringComparison.OrdinalIgnoreCase)))
            return want!;
        return fallback;
    }

    private static void Fill(Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1, string id)
    {
        if (x1 < x0 || y1 < y0 || z1 < z0) return;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    cells[(x, y, z)] = id;
    }

    private static int Face(string? face) => (face ?? "south").Trim().ToLowerInvariant() switch
    {
        "east" => 1,
        "north" => 2,
        "west" => 3,
        _ => 0,
    };

    private static Dictionary<(int x, int y, int z), string> Rotate(
        Dictionary<(int x, int y, int z), string> cells, int turns)
    {
        int t = turns & 3;
        if (t == 0) return cells;

        var result = new Dictionary<(int x, int y, int z), string>();
        foreach (var kv in cells)
        {
            int x = kv.Key.x, z = kv.Key.z;
            for (int i = 0; i < t; i++)
            {
                int nx = -z;
                int nz = x;
                x = nx;
                z = nz;
            }
            result[(x, kv.Key.y, z)] = kv.Value;
        }
        return result;
    }

    private static List<GeneratedBlock> Normalize(Dictionary<(int x, int y, int z), string> cells)
    {
        int minX = 0, minY = 0, minZ = 0;
        foreach (var k in cells.Keys)
        {
            if (k.x < minX) minX = k.x;
            if (k.y < minY) minY = k.y;
            if (k.z < minZ) minZ = k.z;
        }

        return cells
            .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
            .Select(kv => new GeneratedBlock
            {
                X = kv.Key.x - minX,
                Y = kv.Key.y - minY,
                Z = kv.Key.z - minZ,
                Id = kv.Value
            })
            .ToList();
    }
}
