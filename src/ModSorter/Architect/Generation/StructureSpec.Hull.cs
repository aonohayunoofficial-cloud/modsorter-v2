using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 手動生成モードの船体（structure_type="hull:<船種>"）のプロパティ。StructureSpec の partial。
// AI生成側の資産 ShipExpander が使う ship_* とは名前空間を分ける（StructureSpec.Ship.cs）。
//
// 素材スロットはここでは宣言しない。同じ partial クラスなので二重宣言はできず、
// hull_block / deck_block / superstructure_block は StructureSpec.Ship.cs に既にある。
// 割り当ては次のとおり。
//   hull_block   … 外板（未指定なら wall_block）
//   deck_block   … 甲板（未指定なら floor_block → 外板）
//   base_block   … 竜骨・船首材・船尾材
//   accent_block … フレーム（肋骨）・フロア材
//   parapet_block… ブルワーク（舷墻）・手すり
//   superstructure_block / glazing_block … 上部構造と窓（フェーズ6で使う）
//
// 単位は 1マス=1m。他の土木・建築系と同じ縮尺なので、岸壁やドライドックへ横付けしても
// 寸法が食い違わない。
public sealed partial class StructureSpec
{
    // ===== 主要目 =====
    // 全長 LOA。
    [JsonPropertyName("hull_length")] public int? HullLength { get; set; }

    // 型幅。水線での最大幅を指す。フレアを付けると甲板の幅はこれより広くなる。
    [JsonPropertyName("hull_beam")] public int? HullBeam { get; set; }

    // 深さ。基線（船底外板の下端）から船体中央の甲板まで。船首・船尾はシアぶん上がる。
    [JsonPropertyName("hull_depth")] public int? HullDepth { get; set; }

    // 設計喫水。この高さまでが水面下の断面で、これより上へフレア／タンブルホームが効く。
    [JsonPropertyName("hull_draft")] public int? HullDraft { get; set; }

    // ===== 横断面の形 =====
    // 断面のふくらみ 0〜100。断面は超楕円 (|x|/b)^k + ((wl-y)/(wl-底))^k = 1 で、
    // 0 が k=1 の直線V（デッドライズの深い滑走艇）、40 付近が k=2 の円弧（丸ビルジの
    // 排水量型）、100 が k=8 のほぼ矩形（中央横断面係数 Cm≈0.98 のタンカー）。
    [JsonPropertyName("hull_section")] public int? HullSection { get; set; }

    // ===== 水線の平面形 =====
    // 入角（水線の半角・度）。実船で8〜20度、肥えた船で30度超。
    // 船首テーパーの長さ＝最大半幅÷tan(入角) で決まる。
    [JsonPropertyName("hull_entry_angle")] public int? HullEntryAngle { get; set; }

    // 船首水線の肥え 0〜100。0で凹んだ鋭い水線（快速帆船）、100で膨らんだ丸い船首（コグ船）。
    [JsonPropertyName("hull_bow_fullness")] public int? HullBowFullness { get; set; }

    // 船尾の絞りの長さ（全長に対する%）。
    [JsonPropertyName("hull_run_ratio")] public int? HullRunRatio { get; set; }

    // 船尾水線の肥え 0〜100。船首と同じ意味。
    [JsonPropertyName("hull_stern_fullness")] public int? HullSternFullness { get; set; }

    // トランサム（船尾の切り落とし）の幅（最大幅に対する%）。0でダブルエンダー（尖り船尾）。
    [JsonPropertyName("hull_transom")] public int? HullTransom { get; set; }

    // ===== 前後の立ち上がり =====
    // 船首材の傾斜（鉛直からの角度）。現代の直立船首は0〜10度、快速帆船は45度超。
    // 水平方向の走り＝深さ×tan(傾斜) だけ、船底の前端が甲板の前端より後ろへ下がる。
    [JsonPropertyName("hull_stem_rake")] public int? HullStemRake { get; set; }

    // 船尾での船底の立ち上がり（マス）。船尾の絞り長のあいだに基線からこの高さまで上がる。
    [JsonPropertyName("hull_stern_rise")] public int? HullSternRise { get; set; }

    // ===== 喫水線より上 =====
    // 船首フレア角（度）。舷側が外へ開く角。船体中央より前で強く効く。
    [JsonPropertyName("hull_flare")] public int? HullFlare { get; set; }

    // タンブルホーム角（度）。舷側が内へ絞る角。船体中央から船尾側で効く。
    [JsonPropertyName("hull_tumblehome")] public int? HullTumblehome { get; set; }

    // シア倍率（%）。100 で ICLL 1966 の標準シア。ロングシップのように反りの強い船は
    // 200〜300 を指定する。0 で平らな甲板。
    [JsonPropertyName("hull_sheer")] public int? HullSheer { get; set; }

    // ===== 構造・付帯 =====
    // フレーム（肋骨）の間隔（マス）。0でフレームなし。1は指定しても2へ丸める。
    [JsonPropertyName("hull_frame_step")] public int? HullFrameStep { get; set; }

    // 竜骨の張り出し（マス）。基線より下へこの深さだけ出す。0で面一。
    [JsonPropertyName("hull_keel_depth")] public int? HullKeelDepth { get; set; }

    // ブルワーク（舷墻）の高さ（マス）。0でなし。実船は満載喫水線規則で1m以上。
    [JsonPropertyName("hull_bulwark")] public int? HullBulwark { get; set; }
}
