using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// モデルが吐く中間表現。座標ではなく「設計意図」だけを持つ。
// 座標への展開は StructureExpander が確定的に行う。
//
// partial。分野ごとのプロパティ群は StructureSpec.<分野>.cs へ分ける。
// System.Text.Json はプロパティ名で解決するので、宣言がどのファイルにあっても
// 直列化・逆直列化の結果は変わらない。
//
// 共通（複数分野で使う）プロパティは大きくなりすぎたので機能別に分けた。
//   StructureSpec.cs           基本（寸法・素材・階・柱型・土台・座面・縁側・様式）
//   StructureSpec.Roof.cs      屋根まわり（屋根形状・勾配・パラペット・軒・煙突）
//   StructureSpec.Tower.cs     屋根上の突出物（塔屋・塔）
//   StructureSpec.Footprint.cs 平面形状（フットプリント・複数ボリューム合成）
//   StructureSpec.Openings.cs  構造タイプ・入口保証・開口部
//
// 分野別（その分野でしか使わない）プロパティは従来どおり分野名のファイルへ。
//   StructureSpec.Harbor.cs   港湾  structure_type="harbor:<種類>"
//   StructureSpec.Airport.cs  空港  structure_type="airport:<種類>"
//   StructureSpec.Rail.cs     鉄道  structure_type="railway:<種類>"
//   StructureSpec.Bridge.cs   橋梁  structure_type="bridge:<種類>"
//   StructureSpec.Industry.cs 産業  structure_type="industry:<種類>"
//   StructureSpec.Venue.cs    屋外イベント会場  structure_type="venue"
//   StructureSpec.Ship.cs     船    structure_type="ship"
//
// 複数分野で共用する素材スロット（wall / floor / roof / base / accent / parapet /
// glazing / seat）はこの共通ファイル側に置く。分野別ファイルはその分野だけで使う
// プロパティを持つ。
public sealed partial class StructureSpec
{
    [JsonPropertyName("width")] public int Width { get; set; }  // x方向 W
    [JsonPropertyName("depth")] public int Depth { get; set; }  // z方向 D
    [JsonPropertyName("height")] public int Height { get; set; }  // y方向 H

    // 各面の素材（許可ブロックIDのいずれか）。未指定時は wall_block を流用。
    [JsonPropertyName("floor_block")] public string? FloorBlock { get; set; }
    [JsonPropertyName("roof_block")] public string? RoofBlock { get; set; }
    [JsonPropertyName("wall_block")] public string? WallBlock { get; set; }

    // 中間床を入れる高さ(y)のリスト。例: [3] なら y=3 に2階の床。複数指定で3階建て以上。
    // 1階の床(y=0)と屋根は別管理なので、ここには中間の階の床だけを入れる。
    [JsonPropertyName("floor_levels")] public List<int> FloorLevels { get; set; } = new();

    // 柱型リズム（pilaster）用のアクセント材。未指定なら wall_block と同じ＝柱が目立たない。
    // 例: 壁が oak_planks のとき accent_block を oak_log にすると柱だけ丸太になる。
    [JsonPropertyName("accent_block")] public string? AccentBlock { get; set; }

    // 柱を立てる間隔。2以上で有効、未指定/1以下なら柱なし（角だけは accent になる）。
    // 例: 3 なら外周に沿って3マスごとに柱を立てる。
    [JsonPropertyName("pilaster_step")] public int? PilasterStep { get; set; }

    // 土台段（base course）を作るか。true で y=0 の外周一周を base_block に差し替える。
    // 未指定(false)なら土台なし＝従来の見た目。座標系は変えない（張り出しはしない）。
    [JsonPropertyName("has_base")] public bool HasBase { get; set; }

    // 土台段の素材。未指定なら floor_block と同じ＝差し替えても見た目が変わらない。
    // 例: 床が oak_planks のとき base_block を cobblestone にすると足元だけ石の基礎になる。
    [JsonPropertyName("base_block")] public string? BaseBlock { get; set; }

    // 座面・小物の素材。複数分野で共用する。
    //   会場（venue）… 客席の座面。未指定なら accent_block → wall_block を流用。
    //   産業（industry）… 灯火。
    [JsonPropertyName("seat_block")] public string? SeatBlock { get; set; }

    // ===== 縁側／基壇の縁 =====
    // 平面の外側へこの幅だけ、y=0 に床を敷き足す（マス）。0/未指定なら無し。
    // 寺社の縁（深い軒の下の回り縁）、神殿の基壇の縁石に使う。
    // 軒と同じく一時的に負座標を作るが、ExpandCore 末尾の一括シフトで 0 以上へ寄る。
    [JsonPropertyName("veranda_width")] public int? VerandaWidth { get; set; }

    // 縁側に使う素材。未指定なら base_block → floor_block を流用。
    [JsonPropertyName("veranda_block")] public string? VerandaBlock { get; set; }

    // 建物の様式: "walled"（既定・壁のある建物） または "colonnade"（壁のない開放型・列柱）
    [JsonPropertyName("building_style")] public string? BuildingStyle { get; set; }

    // ファサード型(temple)の正面の向き。柱廊をどの面に置くか。
    // "north" | "south" | "east" | "west"。未指定なら "south"。
    [JsonPropertyName("facade_face")] public string? FacadeFace { get; set; }
}
