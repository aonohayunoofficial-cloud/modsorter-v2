using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 橋梁（structure_type="bridge:<種類>"）の座標生成。
// harbor / airport / railway と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・
// 開口部・入口保証・フットプリントマスクは一切通らない。既存の小分類には影響しない。
// 座標ヘルパー（Fill/Rotate/Normalize/Pick）は他の Expander でもそれぞれ private に
// 閉じているため、このクラスも自前で持つ。
//
// StructureExpander.Civil.cs の BuildBridge（structure_type="bridge" の完全一致）は
// AI生成モード側の簡易橋として残す。ここは接頭辞付き（"bridge:girder_bridge" 等）
// だけを受け持つので、両者は衝突しない。
//
// ===== 寸法の扱い =====
// 1マス=1m。鉄道・空港の建物系と同じ縮尺なので、並べて置いても寸法が食い違わない。
//
// ===== 実寸の出典 =====
//   桁高       … 支間長の 1/20 級。連続桁は中間支点で曲げを分担できるぶん
//                 単純桁より低くできるので、展開側でさらに 8 割へ落とす。
//   支間割     … 3径間連続の側径間:中央径間＝1:1.25（側径間80%）が最も鋼重が軽い
//                 （土木学会・鋼連続合成桁の設計法に関する検討）。
//   適用支間   … 桁橋の一般的な適用支間は 25〜150m。
//   車線幅     … 3.25〜3.5m。
//   歩道幅     … 2.0m 級。車道との段差（地覆）は 0.15〜0.25m なので 1 マスで表す。
//   高欄       … 1.1m。1 マスが実寸相当。
//   区画線     … 線幅 0.15m。実線長5m・空白長5mの破線（車線境界線）。1マス=1m では
//                 線に専用の1マス列を与えるので、車線幅は指定どおり保たれる代わりに
//                 全幅が線の本数ぶん広くなる（実寸より広くなる方向の丸め）。
//   照明       … 道路照明の灯具間隔は 30m 級（道路照明施設設置基準）。
//   橋脚形式   … 張出式（T型）が最も一般的。幅員が広い橋では壁式。
//
// 断面は「橋が z 方向に渡る」向きで組み、最後に Rotate で facade_face の向きへ回す。
// 座標は負へ出るが Normalize が 0 起点へ寄せる。
//
// StructureSpec との対応。
//   bridge_spans / bridge_span / bridge_continuous / bridge_side_ratio … 支間割
//   bridge_depth_ratio / bridge_girders / bridge_cross_step … 主桁
//   bridge_lanes / bridge_lane_width / bridge_median / bridge_sidewalk … 横断構成
//   bridge_railing / bridge_lane_mark / bridge_light_step … 付帯設備
//   bridge_pier_type / bridge_pier_height / bridge_abutment … 下部工
//   floor_block=車道舗装 / accent_block=区画線 / wall_block=床版 / roof_block=主桁・横桁
//   base_block=橋脚・橋台 / tower_block=地覆・中央分離帯 / veranda_block=歩道舗装
//   parapet_block=高欄・照明柱 / seat_block=照明
//
// 部品は partial の別ファイルに分けてある。
//   BridgeExpander.Girder.cs  桁橋
public static partial class BridgeExpander
{
    public const string Prefix = "bridge:";

    // StructureExpander から呼ぶ判定。"bridge:" で始まる structure_type だけを受け持つ。
    public static bool Handles(string? structureType)
        => (structureType ?? string.Empty).Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string? structureType)
    {
        string s = (structureType ?? string.Empty).Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(Prefix.Length);
        switch (s.Trim().ToLowerInvariant())
        {
            case "girder_bridge":
            case "girder":
            default: return "girder";
        }
    }

    private sealed class Palette
    {
        public readonly string Pave, Mark, Deck, Girder, Pier, Curb, Walk, Rail, Light;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Pave = Pick(spec.FloorBlock, allowed, fallback);
            Mark = Pick(spec.AccentBlock, allowed, Pave);
            Deck = Pick(spec.WallBlock, allowed, Pave);
            Girder = Pick(spec.RoofBlock, allowed, Deck);
            Pier = Pick(spec.BaseBlock, allowed, Deck);
            Curb = Pick(spec.TowerBlock, allowed, Deck);
            Walk = Pick(spec.VerandaBlock, allowed, Pave);
            Rail = Pick(spec.ParapetBlock, allowed, Curb);
            Light = Pick(spec.SeatBlock, allowed, Rail);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 吊り橋・アーチ橋・跳開橋はここに case を足す。
        switch (KindOf(spec.StructureType))
        {
            case "girder":
            default:
                BuildGirder(cells, spec, p);
                break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
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
