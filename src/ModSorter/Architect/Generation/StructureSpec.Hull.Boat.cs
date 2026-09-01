using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 手動生成の船体のうち開放艇（甲板を張らない小型艇）のプロパティ。StructureSpec の partial。
// 主要目・船型は StructureSpec.Hull.cs、上部構造は StructureSpec.Hull.Top.cs にある。
//
// 素材スロットはここでは宣言しない（同じ partial クラスなので二重宣言はできない）。
// 割り当ては次のとおり。
//   deck_block   … 舷縁（ガンネル）。開放艇では甲板ではなく舷側の最上列だけに使う
//   accent_block … 床板（フロアボード）。フレーム（肋骨）・フロア材と同じ材
//   seat_block   … 漕ぎ座（スオート）
public sealed partial class StructureSpec
{
    // 開放艇。station の最上段を甲板で塞がず、舷側の1列だけを残して内部を空ける。
    // 実艇の端艇（32ft カッター・27ft ホエラー）は甲板を持たず、床板と漕ぎ座だけを持つ。
    // false のときは従来どおり最上段を甲板で塞ぐので、既存15船種の生成物は変わらない。
    [JsonPropertyName("hull_open_boat")] public bool? HullOpenBoat { get; set; }

    // 漕ぎ座（スオート）の間隔（マス）。0でなし。1は2へ丸める。
    // 実艇の座の間隔は0.9〜1.1m級だが、1マス=1mで隣接させると座が連なって甲板と
    // 見分けが付かないため、フレームと同じ丸めを使う。
    // 開放艇でないときは0として扱う（甲板が塞がっていれば座る場所がない）。
    [JsonPropertyName("hull_thwart_step")] public int? HullThwartStep { get; set; }
}
