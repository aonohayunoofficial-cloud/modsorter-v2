using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 手動生成モードの船体（structure_type="hull:<船種>"）の座標生成。
// harbor / airport / railway / bridge / industry と同じ早期リターン方式なので、
// ExpandCore の 床・壁・屋根・開口部・入口保証・フットプリントマスクは一切通らない。
//
// 1マス=1m。
//
// AI生成側の資産 ShipExpander（structure_type="ship"）とは別系統。あちらは船種ごとに
// 船体を作り込んだ既存資産で、こちらは共通の断面生成器から31船種を作る新系統。
// 併存させるため接頭辞（"ship" と "hull:"）とプロパティ（ship_* と hull_*）を分ける。
// ShipExpander には手を入れない。
//
// 生成の順番は 竜骨 → フレーム → 外板 → 甲板 → 上部構造 → 開放艇の内部。
// ファイル分割（partial・1ファイル9KB以下を目安）:
//   HullExpander.cs        … 入口・素材・回転・正規化・外寸
//   HullExpander.Form.cs   … 断面生成器（主要目から各station の船底線・甲板高さ・半幅を出す）
//   HullExpander.Shell.cs  … 竜骨・フレーム・外板・甲板・ブルワークの組み立て
//   HullExpander.Thwart.cs … 開放艇の床板と漕ぎ座
//
// canonical は船首が +z（南）。facade_face で回す。写像は IndustryExpander と同じ
// (x,z)→(-z,x) で、west が1手・north が2手・east が3手。
//
// 共通ヘルパ（Pick / Clamp / Rotate / Normalize）は他の Expander と同じく各 Expander が
// private で持つ既存の作りに合わせている。
public static partial class HullExpander
{
    public const string Prefix = "hull:";

    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private sealed class Palette
    {
        public readonly string Shell, Deck, Keel, Frame, Rail;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Shell = Pick(spec.HullBlock ?? spec.WallBlock, allowed, fallback);
            Deck = Pick(spec.DeckBlock ?? spec.FloorBlock, allowed, Shell);
            Keel = Pick(spec.BaseBlock, allowed, Shell);
            Frame = Pick(spec.AccentBlock, allowed, Shell);
            Rail = Pick(spec.ParapetBlock, allowed, Deck);
        }
    }

    // 座標 -> ブロックステート。梯子やドアの facing を持たせる器で、素の船体では使わない。
    private sealed class Props : Dictionary<(int x, int y, int z), Dictionary<string, string>> { }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var t = new TopPalette(spec, allowedBlocks, p.Shell);
        var form = new Form(spec);
        var top = new Top(spec, form);
        var cells = new Dictionary<(int x, int y, int z), string>();
        var props = new Props();

        // 開放艇かどうかは甲板を置く前に要るので、素の船体へ渡す。
        // BuildTopside は自前で Top を作る既存の作りのままにしてある（Rig.cs へ手を
        // 入れない）。同じコンストラクタを通るので値は食い違わない。
        BuildBareHull(cells, props, form, p, top.OpenBoat);
        BuildTopside(cells, props, form, spec, t);

        // 床板と漕ぎ座は舷縁の内側なので、外板・甲板・艤装のあとに通す。
        BuildOpenBoat(cells, form, top, p, t);

        Rotate(ref cells, ref props, Face(spec.FacadeFace));
        return Normalize(cells, props);
    }

    // UI が Width / Height を先に出すために使う。展開側と同じ Form を通すので、
    // スライダーの表示値と生成物の外寸が食い違わない。
    // 返す値は canonical（船首 +z）での外寸。facade_face が east / west のときは
    // 呼び側で Width と Depth を入れ替える。
    public static (int Width, int Depth, int Height) Extent(StructureSpec spec)
    {
        var f = new Form(spec);
        var (w, h) = f.Bounds();
        var t = new Top(spec, f);

        // 舷の外へ出る部品のうち、いちばん遠くまで出るものを左右へ足す。
        // 盾掛けと貫通横梁の木口は1マス、櫂は Top.OarSide マス（最大3）。
        // 櫂は水面の手前で止まるので、乾舷が1マスの端艇では1マスしか出ない。
        // 一律3マスにすると外寸だけが太るため、Top が数えた実際の張り出しを使う。
        // 中心線舵は船尾材の後ろへ1マス出るので奥行きが1増える。
        // マスト・船楼・船首材の飾りは甲板より上へ伸びるので、竜骨の張り出しぶんを足して比べる。
        // 開放艇の床板・漕ぎ座は舷縁の内側なので外寸には効かない。
        int side = Math.Max(
            t.OarSide,
            t.ShieldPerSide > 0 || t.BeamStep >= 2 ? 1 : 0);
        int width = w + side * 2;
        int depth = f.L + (t.SternRudder ? 1 : 0);
        int height = Math.Max(h, t.TopY + f.KeelDepth + 1);
        return (width, depth, height);
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

    private static int Face(string? face) => (face ?? "south").Trim().ToLowerInvariant() switch
    {
        "west" => 1,
        "north" => 2,
        "east" => 3,
        _ => 0,
    };

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
            if (dst.TryGetValue("facing", out var fc)) dst["facing"] = RotateFacing(fc, t);
            if (dst.TryGetValue("axis", out var ax)) dst["axis"] = RotateAxis(ax, t);
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

    // 竜骨の張り出しで y が負になるので、ここで 0 起点へ寄せる。
    // StructureNbtWriter.Save は負座標を扱えない。
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
