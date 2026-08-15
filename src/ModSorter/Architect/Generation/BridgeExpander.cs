using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 橋梁（structure_type="bridge:<種類>"）の座標生成。
// harbor / airport / railway と同じ早期リターン方式なので、ExpandCore の床・壁・屋根・
// 開口部・入口保証・フットプリントマスクは一切通らない。既存の小分類には影響しない。
// 座標ヘルパー（Fill/Carve/Rotate/Normalize/Pick）は他の Expander でもそれぞれ private に
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
//   桁高       … 支間長の 1/20 級。連続桁は 8 割へ落とす。
//   支間割     … 3径間連続の側径間:中央径間＝1:1.25（土木学会・鋼連続合成桁）。
//   車線幅     … 3.25〜3.5m／歩道 2.0m 級／高欄 1.1m／照明の灯具間隔 30m 級。
//   サグ比     … 1/10 前後（安芸灘大橋 サグ 74.0m・中央支間 750m）。
//   ハンガー   … 間隔 10〜20m 級（明石海峡大橋 14m）。
//   ライズ比   … 1/5〜1/10（日本大百科全書「アーチ橋」）。
//   アーチ支間 … タイドアーチの一般的な適用支間 50〜170m（JFE）。
//   跳開角     … 勝鬨橋は 70 秒で 70 度（土木学会・双葉跳開橋/勝鬨橋の現状と今後）。
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
//   bridge_sag_ratio / bridge_tower_* / bridge_hanger_step / bridge_anchorage … 吊り橋
//   bridge_arch_type / bridge_rise_ratio / bridge_vertical_step / bridge_tie … アーチ橋
//   bridge_leaves / bridge_leaf_span / bridge_open_angle / bridge_counterweight … 跳開橋
//   floor_block=車道舗装 / accent_block=区画線 / wall_block=床版 / roof_block=主桁・横桁
//   base_block=橋脚・橋台・主塔 / tower_block=地覆・中央分離帯 / veranda_block=歩道舗装
//   parapet_block=高欄・照明柱 / seat_block=照明
//   tower_roof_block=主ケーブル・アーチリブ / glazing_block=ハンガー・鉛直材
//
// 部品は partial の別ファイルに分けてある。
//   BridgeExpander.Deck.cs        横断面と付帯設備の共通部品
//   BridgeExpander.Girder.cs      桁橋
//   BridgeExpander.Suspension.cs  吊り橋
//   BridgeExpander.Arch.cs        アーチ橋
//   BridgeExpander.Bascule.cs     跳開橋
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
            case "suspension_bridge":
            case "suspension": return "suspension";
            case "arch_bridge":
            case "arch": return "arch";
            case "bascule_bridge":
            case "bascule": return "bascule";
            case "girder_bridge":
            case "girder":
            default: return "girder";
        }
    }

    private sealed class Palette
    {
        public readonly string Pave, Mark, Deck, Girder, Pier, Curb, Walk, Rail, Light, Cable, Hanger;

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
            Cable = Pick(spec.TowerRoofBlock, allowed, Girder);   // 主ケーブル・アーチリブ
            Hanger = Pick(spec.GlazingBlock, allowed, Rail);      // ハンガー・鉛直材
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        switch (KindOf(spec.StructureType))
        {
            case "suspension": BuildSuspension(cells, spec, p); break;
            case "arch": BuildArch(cells, spec, p); break;
            case "bascule": BuildBascule(cells, spec, p); break;
            case "girder":
            default: BuildGirder(cells, spec, p); break;
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

    // 置いたものを抜く。釣合い錘のピットや機械室の内部に使う。
    private static void Carve(Dictionary<(int x, int y, int z), string> cells,
        int x0, int x1, int y0, int y1, int z0, int z1)
    {
        if (x1 < x0 || y1 < y0 || z1 < z0) return;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    cells.Remove((x, y, z));
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
