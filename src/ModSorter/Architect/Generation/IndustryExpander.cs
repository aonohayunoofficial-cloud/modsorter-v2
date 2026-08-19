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
// このファイルには入口と共通の幾何だけを置く。種類ごとの組み立ては partial の
// 別ファイルに分ける。
//   IndustryExpander.Vessel.cs  縦型容器（サイロ・給水塔・タンク）
//   IndustryExpander.Rotor.cs   風車・水車
//   IndustryExpander.Power.cs   発電所（ボイラ建屋・タービン建屋・煙突・冷却塔・
//                               原子炉格納容器・変電ヤード）
//   ※ 種類を足すときは KindOf と Build の両方に足す。
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
            case "wind_turbine": return "wind_turbine";
            case "water_wheel": return "water_wheel";
            case "boiler_house": return "boiler_house";
            case "turbine_hall": return "turbine_hall";
            case "stack": return "stack";
            case "cooling_tower": return "cooling_tower";
            case "containment": return "containment";
            case "switchyard": return "switchyard";
            case "silo":
            default: return "silo";
        }
    }

    private sealed class Palette
    {
        public readonly string Shell, Roof, Base, Deck, Rail, Accent, Glaze, Light, Stair,
            Blade, Cap, Lattice;

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
            // 回転体（風車・水車）で使う。翼・羽根・水輪の骨組みと、風車のキャップ。
            Blade = Pick(spec.TowerBlock, allowed, Deck);
            Cap = Pick(spec.TowerRoofBlock, allowed, Roof);
            // オランダ型の帆、垂直軸型のガイワイヤ。フェンス・鉄柵など透けるブロック。
            Lattice = Pick(spec.IndustryLatticeBlock, allowed, Rail);
        }
    }

    // 梯子と水は形状・種類が決まっているので選ばせない。
    private const string LadderId = "minecraft:ladder";
    private const string WaterId = "minecraft:water";

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
            case "wind_turbine": BuildWindTurbine(cells, spec, p); break;
            case "water_wheel": BuildWaterWheel(cells, spec, p); break;
            case "boiler_house": BuildPowerHall(cells, props, spec, p, true); break;
            case "turbine_hall": BuildPowerHall(cells, props, spec, p, false); break;
            case "stack": BuildStack(cells, props, spec, p); break;
            case "cooling_tower": BuildCoolingTower(cells, props, spec, p); break;
            case "containment": BuildContainment(cells, props, spec, p); break;
            case "switchyard": BuildSwitchyard(cells, props, spec, p); break;
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

    // 半径が段ごとに縮む回転体の殻。各段は「自分の半径」から「次の段の半径」または
    // 「自分の半径-1」の内側を除いた環を置き、最後の段だけ中身まで埋めて頂部を塞ぐ。
    //
    // 内側の抜き半径に r-1 の下限（min）を入れる理由。ドーム屋根で 1/n を小さくすると
    // 高さが直径に近いところまで立ち上がり、隣り合う段の半径差が1マスを大きく下回る。
    // 次の段の半径だけで内側を抜くと環の幅が0マスになり、その段が空になって
    // 屋根が浮いたり側面に隙間が空いた。min を取れば環の幅は必ず1マス以上になり、
    // 勾配の緩い円錐屋根では従来どおり次の段までを一気に埋める。
    private static void Revolve(Dictionary<(int x, int y, int z), string> cells,
        int ox, int oz, int d, int baseY, IReadOnlyList<double> radii, string id)
    {
        for (int k = 0; k < radii.Count; k++)
        {
            bool last = k == radii.Count - 1;
            double r = radii[k];
            double rn = last ? -1 : Math.Min(radii[k + 1], r - 1.0);
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
    // 半径が1マスを下回る段は加えない。半径0の段を持つと Revolve の最終段（中身まで
    // 埋める段）が空になり、段数（RoofLevels）だけ増えて頂部の点検口・投入シュートが
    // 宙に浮いた。最終段の半径が1マス以上なら必ず円板で塞がる。
    private static List<double> DomeRadii(int d, int h)
    {
        if (h < 1) h = 1;
        var list = new List<double>();
        double half = d / 2.0;
        for (int k = 0; k <= h; k++)
        {
            double r = half * Math.Sqrt(Math.Max(0.0, 1.0 - (double)k * k / ((double)h * h)));
            if (r < 1.0) break;
            list.Add(r);
        }
        if (list.Count == 0) list.Add(half);
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

    // 方角を単位ベクトルへ。north が -z、south が +z、east が +x、west が -x。
    private static (int dx, int dz) Dir(string? face) =>
        (face ?? "south").Trim().ToLowerInvariant() switch
        {
            "north" => (0, -1),
            "east" => (1, 0),
            "west" => (-1, 0),
            _ => (0, 1),
        };

    // 直径 d の円を dir 方向の中心線で切り、円に入っている外側の端の添字を返す。
    // fix は中心線の座標（直交方向）。円が空なら false。
    private static bool AxisEnd(int d, (int dx, int dz) dir, out int fix, out int outer)
    {
        double c = (d - 1) / 2.0;
        fix = (int)Math.Round(c);
        outer = 0;

        int lo = -1, hi = -1;
        for (int i = 0; i < d; i++)
        {
            int x = dir.dx != 0 ? i : fix;
            int z = dir.dz != 0 ? i : fix;
            if (!InR(x, z, d, d / 2.0)) continue;
            if (lo < 0) lo = i;
            hi = i;
        }
        if (lo < 0) return false;

        outer = dir.dx + dir.dz > 0 ? hi : lo;
        return true;
    }

    // 外周の環だけを face の側で抜く／塗り替える。内側のホッパーや床には当たらない。
    // id が null なら抜いて開口にし、指定があればその材に塗り替える。
    //
    // 幅の判定は中心からの距離で見る。直径が偶数だと中心がマス境界に来るため、
    // 距離0のマスが存在せず幅1の開口が1マスも当たらなかった（給水塔の塔身は既定
    // 直径4＝偶数なので出入口が開かなかった）。偶数径では半マスずらした閾値にする。
    private static void OpenRing(Dictionary<(int x, int y, int z), string> cells,
        int ox, int oz, int d, int y0, int y1, int width, string? face, string? id = null)
    {
        var dir = Dir(face);
        double c = (d - 1) / 2.0;
        double r = d / 2.0;

        double lim = (width - 1) / 2.0;
        if (d % 2 == 0) lim = Math.Floor(lim) + 0.5;

        for (int y = y0; y <= y1; y++)
            for (int x = 0; x < d; x++)
                for (int z = 0; z < d; z++)
                {
                    if (!InR(x, z, d, r) || InR(x, z, d, r - 1.0)) continue;
                    // along は face へ進む量。0以下は反対側と真横なので触らない。
                    double along = dir.dx != 0 ? (x - c) * dir.dx : (z - c) * dir.dz;
                    double cross = dir.dx != 0 ? z - c : x - c;
                    if (along <= 0) continue;
                    if (Math.Abs(cross) > lim) continue;
                    if (id == null) cells.Remove((ox + x, y, oz + z));
                    else cells[(ox + x, y, oz + z)] = id;
                }
    }

    // 外部ラダー。minecraft:ladder を胴板の外側1マスに1列立てる。
    // facing は「取り付く壁から梯子へ向かう向き」なので、置いた側の方角そのもの。
    // 置く側は face で決める。開口部と同じ方角にすると梯子が開口を塞ぐので、
    // UI の既定値は梯子と開口で別方角にしてある。
    private static void Ladder(Dictionary<(int x, int y, int z), string> cells, Props props,
        int ox, int oz, int d, int y0, int y1, string? face)
    {
        var dir = Dir(face);
        if (!AxisEnd(d, dir, out int fix, out int outer)) return;

        int step = dir.dx + dir.dz > 0 ? 1 : -1;
        int lx = dir.dx != 0 ? outer + step : fix;
        int lz = dir.dz != 0 ? outer + step : fix;
        string facing = dir.dx != 0
            ? (dir.dx > 0 ? "east" : "west")
            : (dir.dz > 0 ? "south" : "north");

        for (int y = y0; y <= y1; y++)
        {
            var key = (ox + lx, y, oz + lz);
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

    // 回転回数。Rotate の写像は (x,z)→(-z,x) で、canonical の正面 +z（南）は
    // 1手ごとに 南→西→北→東 と移る。ゆえに west が1手・north が2手・east が3手。
    // （従来 east と west が入れ替わっていた。縦型容器は facade_face を渡していないため
    // 出力は変わらないが、回転体は向きを使うので正す。）
    private static int Face(string? face) => (face ?? "south").Trim().ToLowerInvariant() switch
    {
        "west" => 1,
        "north" => 2,
        "east" => 3,
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
