using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 屋外イベント会場（structure_type="venue"）の座標生成。
// ExpandCore の床/壁/屋根/開口部・入口保証・フットプリントマスクは一切通らない。
// ship と同じ早期リターン方式なので、既存の中分類には影響しない。
//
//   arena    … コロッセウム。外形188×156m / アリーナ87×55m（長径の46%・短径の35%）/
//               高さ48m / アリーナと最前列の間に5mのポディウム壁 / 外周は約6.8m間隔の
//               アーチ列を3層 / 屋根は無く可動日除け(velarium)のみ。
//   stadium  … 近代スタジアム。矩形ピッチを角丸の連続ボウルが四周囲む。
//               片面スタンド単体（背面棟＋妻壁＋持ち出し屋根）も同じ経路で作る。
//   bandshell… エピダウロス劇場（円形オルケストラ＋210°の扇形カヴェア＋ディアゾマ）に
//               ハリウッドボウルの同心円シェル（半ドーム）を合わせたもの。
//   stage    … 櫓ステージ。屋根は4隅の柱と桁で支え、妻面も塞ぐ。
//   tents    … 切妻テントの列。地面は既定で敷かない（床とテント床の二重を作らない）。
//
// すべて「正面が南（+z 側）」で組み、最後に Rotate で向きを回す。
public static partial class VenueExpander
{
    private sealed class Palette
    {
        public readonly string Structure, Seat, Field, Roof, Accent;

        public Palette(StructureSpec spec, IReadOnlyList<string> allowed, string fallback)
        {
            Structure = Pick(spec.WallBlock, allowed, fallback);
            Seat = Pick(spec.SeatBlock ?? spec.AccentBlock, allowed, Structure);
            Field = Pick(spec.FloorBlock, allowed, Structure);
            Roof = Pick(spec.RoofBlock, allowed, Structure);
            Accent = Pick(spec.AccentBlock, allowed, Structure);
        }
    }

    public static List<GeneratedBlock> Build(
        StructureSpec spec, IReadOnlyList<string> allowedBlocks, string fallback)
    {
        var p = new Palette(spec, allowedBlocks, fallback);
        var cells = new Dictionary<(int x, int y, int z), string>();

        string kind = (spec.VenueKind ?? "arena").Trim().ToLowerInvariant();
        switch (kind)
        {
            case "stadium": BuildStadium(cells, spec, p); break;
            case "bandshell": BuildBandshell(cells, spec, p); break;
            case "stage": BuildStage(cells, spec, p); break;
            case "tents": BuildTents(cells, spec, p); break;
            default: BuildArena(cells, spec, p); break;
        }

        cells = Rotate(cells, Face(spec.FacadeFace));
        return Normalize(cells);
    }
}
