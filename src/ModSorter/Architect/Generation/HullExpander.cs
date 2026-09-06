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
//   HullExpander.cs        … 入口・素材・組み立ての呼び出し
//   HullExpander.Extent.cs … 外寸（UI と展開側で式を二重に持たないための1か所）
//   HullExpander.Rotate.cs … 船首の向きの回転と負座標の正規化
//   HullExpander.Form.cs   … 断面生成器（主要目から各station の船底線・甲板高さ・半幅を出す）
//   HullExpander.Shell.cs  … 竜骨・フレーム・外板・甲板・ブルワークの組み立て
//   HullExpander.Thwart.cs … 開放艇の床板と漕ぎ座
// このファイルが12,628バイトになったので、外寸と回転・正規化を上の2枚へ移した。
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

    // ===== 共通の小物 =====
    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string Pick(string? want, IReadOnlyList<string> allowed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(want) &&
            allowed.Any(a => string.Equals(a, want, StringComparison.OrdinalIgnoreCase)))
            return want!;
        return fallback;
    }
}
