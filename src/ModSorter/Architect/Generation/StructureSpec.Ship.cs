using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 船（structure_type="ship"）のプロパティ。StructureSpec の partial。
// AI 生成側の資産である ShipExpander が参照する。手動生成の船体（hull:*）を足すときは
// このファイルではなく StructureSpec.Hull.cs を新設し、ship_* とは名前空間を分ける。
public sealed partial class StructureSpec
{
    // ===== 船（structure_type="ship"）=====
    // 船種。未指定なら width×depth×height のサイズ帯から候補を絞って自動選択（ランダム性あり）。
    //  "rowboat"   … 手漕ぎボート/ディンギー（最小・上部構造物なし）
    //  "motorboat" … モーターボート/クルーザー（低船体＋小さな操縦席と風防）
    //  "trawler"   … トロール/カニ漁船（船首寄りに高い操舵室＋船尾に開放作業甲板＋マスト）
    //  "caravel"   … 小型帆船（船尾楼＋2〜3本マスト）
    //  "galleon"   … ガレオン（高い船首楼・船尾楼＋3〜4本マスト＋砲門列）
    //  "liner"     … 大型客船/オーシャンライナー（多層の上部構造物＋煙突＋舷側の窓列）
    //  "cargo"     … 貨物/コンテナ/タンカー（船尾寄りに高いブリッジ＋長い平甲板）
    //  "destroyer" … 駆逐艦（細身・鋭い船首・中央前寄りブリッジ）
    //  "battleship"… 戦艦（幅広・重厚な上部構造物＋主砲塔）
    //  "carrier"   … 空母（全通平甲板＋右舷アイランド）
    //  "submarine" … 潜水艦（葉巻型＋司令塔）
    [JsonPropertyName("ship_class")] public string? ShipClass { get; set; }

    // 船首の向き（尖る側）。z軸に沿って前後を決める。
    // "north"（z=0 側が船首・既定） | "south"（z=depth-1 側が船首）。
    [JsonPropertyName("bow_face")] public string? BowFace { get; set; }

    // 船体（水線下・船体本体）の素材。未指定なら wall_block を流用。
    [JsonPropertyName("hull_block")] public string? HullBlock { get; set; }

    // 甲板の素材。未指定なら floor_block → wall_block の順で流用。
    [JsonPropertyName("deck_block")] public string? DeckBlock { get; set; }

    // 上部構造物（ブリッジ・船室・島・船楼）の素材。未指定なら wall_block を流用。
    [JsonPropertyName("superstructure_block")] public string? SuperstructureBlock { get; set; }

    // 船種の自動選択に使う乱数シード（任意）。0（既定）なら width+depth+height から
    // 確定的に導く＝同じ寸法なら毎回同じ船種。値を変えると同寸法でも別の船種になる。
    [JsonPropertyName("ship_seed")] public int ShipSeed { get; set; }
}
