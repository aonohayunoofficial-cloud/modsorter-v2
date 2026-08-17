using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 産業インフラ（structure_type="industry:<種類>"）の座標生成。
// harbor / airport / railway / bridge と同じ早期リターン方式なので、ExpandCore の
// 床・壁・屋根・開口部・入口保証・フットプリントマスクは一切通らない。
//
// 1マス=1m。他の土木・建築系と同じ縮尺なので並べて置いても寸法が食い違わない。
//
// このファイルには入口と回転体の幾何だけを置く。種類ごとの組み立ては partial の
// 別ファイルに分ける。
//   IndustryExpander.Vessel.cs  縦型容器（サイロ・給水塔・タンク）
//   ※ 発電所・風車・水車は未実装。実装時は KindOf と Build に足す。
//
// StructureSpec との対応。
//   industry_diameter / industry_body_height … 円筒
//   industry_roof / industry_roof_pitch … 屋根
//   industry_skirt / industry_hopper / industry_chute … サイロ
//   industry_shaft_width / industry_shaft_height / industry_balcony … 給水塔
//   industry_stair / industry_wind_girder / industry_dike … タンク
//   wall_block=胴板・塔身 / roof_block=屋根 / base_block=基礎・スカート・防油堤
//   floor_block=床・デッキ・踏板 / parapet_block=手すり・ラダー
//   accent_block=バンド・風止めリング / glazing_block=点検口 / seat_block=灯火
public static partial class IndustryExpander
{
    public const string Prefix = "industry:";

    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "water_tower": return "water_tower";
            case "tank": return "tank";
            case "silo":
            default: return "silo";
        }
    }

    private sealed class Palette
    {
        public readonly string Shell, Roof, Base, Deck, Rail, Accent, Glaze, Light, Stair;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Shell = Pick(spec.WallBlock, allowed, fallback);
            Roof = Pick(spec.RoofBlock, allowed, Shell);
            Base = Pick(spec.BaseBlock, allowed, Shell);
            Deck = Pick(spec.FloorBlock, allowed, Shell);
            Rail = Pick(spec.ParapetBlock, allowed, Shell);
            Accent = Pick(spec.AccentBlock, allowed, Shell);
            Glaze = Pick(spec.GlazingBlock, allowed, Shell);
            Light = Pick(spec.SeatBlock, allowed, Roof);
            Stair = Pick(spec.VerandaBlock, allowed, Deck);
        }
    }

    // 梯子は形状が決まっているので選ばせない。
    private const string LadderId = "minecraft:ladder";

    // 座標 -> ブロックステート。梯子の facing、階段の facing/half/shape を持たせる。
    // 状態を持たないブロックはここへ入れない（GeneratedBlock.Properties が null になる）。
    private sealed class Props : Dictionary<(int x, int y, int z), Dictionary<string, string>> { }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();
        var props = new Props();

        switch (KindOf(spec.StructureType))
        {
            case "water_tower": BuildWaterTower(cells, props, spec, p); break;
            case "tank": BuildTank(cells, props, spec, p); break;
            case "silo":
            default: BuildSilo(cells, props, spec, p); break;
        }

        Rotate(ref cells, ref props, Face(spec.FacadeFace));
        return Normalize(cells, props);
    }

    // ===== 回転体の幾何 =====
    // 直径 d の円は 0..d-1 の箱に収め、中心を (d-1)/2 に置く。d が偶数でも奇数でも
    // 左右対称になる。半径を引数で渡して環・円錐・ドームを同じ判定で作る。
    private static bool InR(int x, int z, int d, double r)
    {
        if (r <= 0) return false;
        double c = (d - 1) / 2.0;
        double dx = x - c, dz = z - c;
        return dx * dx + dz * dz <= r * r;
    }

    // 中身の詰まった円板を y0..y1 に積む。
    private static void Disc(Dictionary<(int x, int y, int z), string> cells,
        int ox, int oz, int d, int y0, int y1, string id)
    {
        double r = d / 2.0;
        for (int y = y0; y <= y1; y++)
            for (int x = 0; x < d; x++)
                for (int z = 0; z < d; z++)
                    if (InR(x, z, d, r)) cells[(ox + x, y, oz + z)] = id;
    }

    // 厚さ1マスの環（円筒の側壁）を y0..y1 に積む。
    private static void Ring(Dictionary<(int x, int y, int z), string> cells,
        int ox, int oz, int d, int y0, int y1, string id)
    {
        double r = d / 2.0;
        for (int y = y0; y <= y1; y++)
            for (int x = 0; x < d; x++)
                for (int z = 0; z < d; z++)
                    if (InR(x, z, d, r) && !InR(x, z, d, r - 1.0))
                        cells[(ox + x, y, oz + z)] = id;
    }

    // 半径が段ごとに縮む回転体の殻。各段は自分の半径から次の段の半径を除いた環を置き、
    // 最後の段だけ中身まで埋めて頂部を塞ぐ。勾配が急でも面が抜けない。
    private static void Revolve(Dictionary<(int x, int y, int z), string> cells,
        int ox, int oz, int d, int baseY, IReadOnlyList<double> radii, string id)
    {
        for (int k = 0; k < radii.Count; k++)
        {
            bool last = k == radii.Count - 1;
            double r = radii[k];
            double rn = last ? -1 : radii[k + 1];
            for (int x = 0; x < d; x++)
                for (int z = 0; z < d; z++)
                {
                    if (!InR(x, z, d, r)) continue;
                    if (!last && InR(x, z, d, rn)) continue;
                    cells[(ox + x, baseY + k, oz + z)] = id;
                }
        }
    }

    // 円錐屋根。run マス進むごとに1マス上がる（勾配 1/run）。
    private static List<double> ConeRadii(int d, int run)
    {
        if (run < 1) run = 1;
        var list = new List<double>();
        for (double r = d / 2.0; ; r -= run)
        {
            list.Add(r);
            if (r - run <= 1.0) break;
        }
        return list;
    }

    // ドーム屋根。高さ h の半楕円体。h = 直径/n。
    private static List<double> DomeRadii(int d, int h)
    {
        if (h < 1) h = 1;
        var list = new List<double>();
        double half = d / 2.0;
        for (int k = 0; k <= h; k++)
        {
            double r = half * Math.Sqrt(Math.Max(0.0, 1.0 - (double)k * k / ((double)h * h)));
            list.Add(r);
            if (r <= 1.0) break;
        }
        return list;
    }

    private static List<double> RoofRadii(int d, string? roof, int pitch)
    {
        int n = Math.Max(1, pitch);
        switch ((roof ?? "dome").Trim().ToLowerInvariant())
        {
            case "flat": return new List<double> { d / 2.0 };
            case "cone": return ConeRadii(d, n);
            default: return DomeRadii(d, Math.Max(1, (int)Math.Round(d / (double)n)));
        }
    }

    // UI が Height を先に出すために使う。展開側と同じ式なので値が食い違わない。
    public static int RoofLevels(int diameter, string? roof, int pitch)
        => RoofRadii(Math.Max(1, diameter), roof, pitch).Count;

    // 屋根を積み、使った段数を返す。
    private static int BuildRoof(Dictionary<(int x, int y, int z), string> cells,
        int ox, int oz, int d, int baseY, string? roof, int pitch, string id)
    {
        var radii = RoofRadii(d, roof, pitch);
        Revolve(cells, ox, oz, d, baseY, radii, id);
        return radii.Count;
    }

    // 外周の環だけを手前（+z）側で抜く／塗り替える。内側のホッパーや床には当たらない。
    // id が null なら抜いて開口にし、指定があればその材に塗り替える。
    private static void OpenRing(Dictionary<(int x, int y, int z), string> cells,
        int ox, int oz, int d, int y0, int y1, int width, string? id = null)
    {
        double c = (d - 1) / 2.0;
        double r = d / 2.0;
        for (int y = y0; y <= y1; y++)
            for (int x = 0; x < d; x++)
            {
                if (Math.Abs(x - c) > (width - 1) / 2.0) continue;
                for (int z = 0; z < d; z++)
                {
                    if (z < c) continue;
                    if (!InR(x, z, d, r) || InR(x, z, d, r - 1.0)) continue;
                    if (id == null) cells.Remove((ox + x, y, oz + z));
                    else cells[(ox + x, y, oz + z)] = id;
                }
            }
    }

    // 外部ラダー。minecraft:ladder を胴板の外側1マスに1列立てる。
    // facing は「取り付く壁から梯子へ向かう向き」なので、+z 側なら south、-z 側なら north。
    // dir=+1 で手前（+z）側、dir=-1 で奥（-z）側。タンクはらせん階段が +x〜+z の弧を
    // 使うので -1 側へ逃がす。
    private static void Ladder(Dictionary<(int x, int y, int z), string> cells, Props props,
        int ox, int oz, int d, int y0, int y1, int dir)
    {
        double c = (d - 1) / 2.0;
        int x = (int)Math.Round(c);

        int zEnd = -1;
        for (int z = 0; z < d; z++)
        {
            if (!InR(x, z, d, d / 2.0)) continue;
            if (dir > 0) zEnd = z;              // 最大 z（+z 側の外周）
            else if (zEnd < 0) zEnd = z;        // 最小 z（-z 側の外周）
        }
        if (zEnd < 0) return;

        int zl = oz + zEnd + (dir > 0 ? 1 : -1);
        string facing = dir > 0 ? "south" : "north";

        for (int y = y0; y <= y1; y++)
        {
            var key = (ox + x, y, zl);
            cells[key] = LadderId;
            props[key] = new Dictionary<string, string> { ["facing"] = facing };
        }
    }

    // らせん階段。踏面2マスにつき蹴上げ1マス＝約26.6度（跨線橋の階段と同じ勾配）。
    //
    // 平ブロックと階段ブロックを交互に置く。平ブロックの天端 → 階段の半段（+0.5）→
    // 一つ上の平ブロックの天端（+0.5）と上がるので、ジャンプなしで登れる。
    // 階段の facing は進行方向（階段の高い側が向く向き）に合わせる。
    private static void Helix(Dictionary<(int x, int y, int z), string> cells, Props props,
        int ox, int oz, int d, int y0, int y1, string tread, string stair)
    {
        int rise = y1 - y0;
        if (rise <= 0) return;

        var path = PerimeterPath(d);
        if (path.Count < 2) return;

        int need = rise * 2 + 1;
        for (int i = 0; i < need; i++)
        {
            var cur = path[i % path.Count];

            if (i % 2 == 0)
            {
                cells[(ox + cur.x, y0 + i / 2, oz + cur.z)] = tread;
                continue;
            }

            var next = path[(i + 1) % path.Count];
            int y = y0 + (i + 1) / 2;
            var key = (ox + cur.x, y, oz + cur.z);
            cells[key] = stair;
            props[key] = new Dictionary<string, string>
            {
                ["facing"] = DirOf(next.x - cur.x, next.z - cur.z),
                ["half"] = "bottom",
                ["shape"] = "straight",
            };
        }
    }

    // 外周の1マス外側を角度順にたどる閉経路。斜めに飛ぶ箇所へ直交1マスを挿し込み、
    // 隣り合うマスが必ず直交で繋がるようにする。斜めのままだと階段が繋がらない。
    private static List<(int x, int z)> PerimeterPath(int d)
    {
        double c = (d - 1) / 2.0;
        double r = d / 2.0 + 0.5;
        var path = new List<(int x, int z)>();
        int steps = Math.Max(48, (int)Math.Round(2 * Math.PI * r * 4));

        for (int k = 0; k < steps; k++)
        {
            double ang = 2 * Math.PI * k / steps;
            int x = (int)Math.Round(c + r * Math.Cos(ang));
            int z = (int)Math.Round(c + r * Math.Sin(ang));

            if (path.Count > 0)
            {
                var prev = path[path.Count - 1];
                if (prev.x == x && prev.z == z) continue;
                if (prev.x != x && prev.z != z) path.Add((x, prev.z));
            }
            path.Add((x, z));
        }

        if (path.Count > 1)
        {
            var first = path[0];
            var last = path[path.Count - 1];
            if (first.x != last.x && first.z != last.z) path.Add((first.x, last.z));
        }
        return path;
    }

    // 進行方向を向きの名前へ。+x が east、+z が south。
    private static string DirOf(int dx, int dz)
    {
        if (dx > 0) return "east";
        if (dx < 0) return "west";
        if (dz > 0) return "south";
        return "north";
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

    // 座標と一緒にブロックステートも回す。座標の写像が (x,z)→(-z,x) なので、
    // 向きは east→south→west→north の順に1手ずつ送る。
    private static void Rotate(
        ref Dictionary<(int x, int y, int z), string> cells, ref Props props, int turns)
    {
        int t = turns & 3;
        if (t == 0) return;

        var rc = new Dictionary<(int x, int y, int z), string>();
        var rp = new Props();

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

            var key = (x, kv.Key.y, z);
            rc[key] = kv.Value;

            if (!props.TryGetValue(kv.Key, out var src)) continue;

            var dst = new Dictionary<string, string>(src);
            if (dst.TryGetValue("facing", out var f)) dst["facing"] = RotateFacing(f, t);
            rp[key] = dst;
        }

        cells = rc;
        props = rp;
    }

    private static string RotateFacing(string face, int turns)
    {
        string[] cycle = { "east", "south", "west", "north" };
        int i = Array.IndexOf(cycle, face);
        if (i < 0) return face;
        return cycle[(i + (turns & 3)) % 4];
    }

    private static List<GeneratedBlock> Normalize(
        Dictionary<(int x, int y, int z), string> cells, Props props)
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
                Id = kv.Value,
                Properties = props.TryGetValue(kv.Key, out var pr) && pr.Count > 0 ? pr : null
            })
            .ToList();
    }
}
