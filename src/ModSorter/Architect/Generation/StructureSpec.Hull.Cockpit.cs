using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 手動生成の船体（structure_type="hull:<船種>"）のコックピットのプロパティ。
// StructureSpec の partial。組み立ては HullExpander.Cockpit.cs。
//
// 素材スロット:
//   hull_cockpit_seat_block … 座席（下付きの半ブロックを想定）
//   hull_cushion_block      … サンパッド（カーペットを想定）
//   glazing_block           … 風防（デッキハウスの窓と共用）
//   accent_block            … コックピットの床（開放艇の床板と共用）
public sealed partial class StructureSpec
{
    // コックピットの前後長（全長に対する%）。0でなし。
    [JsonPropertyName("hull_cockpit_length")] public int? HullCockpitLength { get; set; }

    // コックピットの前後の位置のずらし（マス）。+で船首側。0で船体中央。
    [JsonPropertyName("hull_cockpit_shift")] public int? HullCockpitShift { get; set; }

    // 座席の列数。後端から2マスおき（座面＋足元）に置く。
    [JsonPropertyName("hull_cockpit_seats")] public int? HullCockpitSeats { get; set; }

    // 風防。コックピットの前の station に横一列、最前列の両舷へ回り込みを1マスずつ立てる。
    [JsonPropertyName("hull_windscreen")] public bool? HullWindscreen { get; set; }

    // サンパッドの前後長（マス）。コックピットの後ろの甲板へ敷く。0でなし。
    [JsonPropertyName("hull_sun_pad")] public int? HullSunPad { get; set; }

    // 座席の素材。未指定なら accent_block。
    [JsonPropertyName("hull_cockpit_seat_block")] public string? HullCockpitSeatBlock { get; set; }

    // サンパッドの素材。未指定なら座席と同じ。
    [JsonPropertyName("hull_cushion_block")] public string? HullCushionBlock { get; set; }
}
