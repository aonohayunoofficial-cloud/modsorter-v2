using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 橋梁（structure_type="bridge:<種類>"）のプロパティ。StructureSpec の partial。
// 桁橋の共通部と、吊り橋・アーチ橋・跳開橋の形式別パラメータを持つ。
public sealed partial class StructureSpec
{
    // ===== 橋梁（structure_type="bridge:<種類>"）=====
    // 1マス=1m。鉄道・空港の建物系と同じ縮尺なので並べても寸法が食い違わない。
    // 橋長と全幅は下の bridge_* から従属して決まるので width / depth / height は
    // BridgeExpander では参照しない（UI 側は表示のために同じ値を入れておく）。

    // 支間数（径間の数）。橋脚の本数は支間数-1。
    [JsonPropertyName("bridge_spans")] public int? BridgeSpans { get; set; }

    // 1径間の支間長（マス＝m）。桁橋の一般的な適用支間は25〜150m。
    [JsonPropertyName("bridge_span")] public int? BridgeSpan { get; set; }

    // 連続桁にするか。false なら単純桁で、支点ごとに主桁を切って遊間を入れる。
    [JsonPropertyName("bridge_continuous")] public bool BridgeContinuous { get; set; }

    // 連続桁の側径間比（%）。3径間以上のとき両端の径間をこの割合まで詰める。
    // 80（既定）で側径間:中央径間＝1:1.25。鋼重が最も軽くなる比率。
    [JsonPropertyName("bridge_side_ratio")] public int? BridgeSideRatio { get; set; }

    // 桁高比。支間長の 1/n を主桁の高さにする。20（既定）が標準的な桁橋の桁高。
    // 連続桁では中間支点で曲げを分担できるぶん、展開側でさらに 8 割へ落とす。
    [JsonPropertyName("bridge_depth_ratio")] public int? BridgeDepthRatio { get; set; }

    // 主桁の本数。0/未指定なら全幅からおよそ3m間隔で自動決定（2主桁〜多主桁）。
    [JsonPropertyName("bridge_girders")] public int? BridgeGirders { get; set; }

    // 横桁（対傾構）の間隔（マス）。0 で横桁なし。
    [JsonPropertyName("bridge_cross_step")] public int? BridgeCrossStep { get; set; }

    // 車線数と1車線の幅（マス）。実物の車線幅は 3.25〜3.5m。
    [JsonPropertyName("bridge_lanes")] public int? BridgeLanes { get; set; }
    [JsonPropertyName("bridge_lane_width")] public int? BridgeLaneWidth { get; set; }

    // 中央分離帯の幅（マス）。0 で分離帯なし。1 以上で上下線を分ける。
    [JsonPropertyName("bridge_median")] public int? BridgeMedian { get; set; }

    // 片側の歩道幅（マス）。0 で歩道なし（地覆だけ）。実物の歩道は 2.0m 級。
    [JsonPropertyName("bridge_sidewalk")] public int? BridgeSidewalk { get; set; }

    // 高欄・防護柵の高さ（マス）。0 でなし。実物は 1.1m なので 1 が実寸相当。
    [JsonPropertyName("bridge_railing")] public int? BridgeRailing { get; set; }

    // 区画線（外側線・車線境界線）を描くか。実物の線幅 0.15m に対し 1 マスを充てる。
    [JsonPropertyName("bridge_lane_mark")] public bool BridgeLaneMark { get; set; }

    // 橋脚の形式。"t"（既定・張出式＝T型）| "wall"（壁式）| "frame"（ラーメン・2本柱）。
    [JsonPropertyName("bridge_pier_type")] public string? BridgePierType { get; set; }

    // 橋脚の高さ（マス）。桁下端までの高さ。地面から桁下までの空間になる。
    [JsonPropertyName("bridge_pier_height")] public int? BridgePierHeight { get; set; }

    // 両端に橋台と取付部を作るか。true で橋の前後へ 3 マスの取付道路が伸びる。
    [JsonPropertyName("bridge_abutment")] public bool BridgeAbutment { get; set; }

    // 照明の間隔（マス）。0 で照明なし。道路照明の灯具間隔は 30m 級。
    [JsonPropertyName("bridge_light_step")] public int? BridgeLightStep { get; set; }

    // ===== 橋梁: 吊り橋（bridge:suspension_bridge）=====
    // sag_ratio … サグ比 1/n。実橋は 1/10 前後（安芸灘大橋 74.0m/750m ＝ 1/10）。
    [JsonPropertyName("bridge_sag_ratio")] public int? BridgeSagRatio { get; set; }
    // tower_height … 床版から主塔頂までの高さ。0 で自動（サグ＋余裕）。
    [JsonPropertyName("bridge_tower_height")] public int? BridgeTowerHeight { get; set; }
    // tower_type … "portal"（門型）/"h"（H型）/"truss"（トラス塔）
    [JsonPropertyName("bridge_tower_type")] public string? BridgeTowerType { get; set; }
    // hanger_step … ハンガー間隔。実橋は10〜20m級（明石海峡大橋14m）。
    [JsonPropertyName("bridge_hanger_step")] public int? BridgeHangerStep { get; set; }
    // stiffen_depth … 補剛桁高。実橋は支間の1/100級だが1マス=1mでは潰れるので最小2。
    [JsonPropertyName("bridge_stiffen_depth")] public int? BridgeStiffenDepth { get; set; }
    // anchorage … アンカレイジ（ケーブル定着体）を作るか。
    [JsonPropertyName("bridge_anchorage")] public bool BridgeAnchorage { get; set; }

    // ===== 橋梁: アーチ橋（bridge:arch_bridge）=====
    // arch_type … "deck"（上路式）/"through"（下路式）/"half"（中路式）
    [JsonPropertyName("bridge_arch_type")] public string? BridgeArchType { get; set; }
    // rise_ratio … ライズ比 1/n。実橋は 1/5〜1/10（日本大百科全書）。
    [JsonPropertyName("bridge_rise_ratio")] public int? BridgeRiseRatio { get; set; }
    [JsonPropertyName("bridge_arch_ribs")] public int? BridgeArchRibs { get; set; }
    // vertical_step … 鉛直材（上路式の支柱）・吊材（下路式）の間隔。
    [JsonPropertyName("bridge_vertical_step")] public int? BridgeVerticalStep { get; set; }
    // tie … タイドアーチのタイ材（水平反力を橋自身で受ける）。
    [JsonPropertyName("bridge_tie")] public bool BridgeTie { get; set; }
    // bracing … アーチリブ間の横構。
    [JsonPropertyName("bridge_bracing")] public bool BridgeBracing { get; set; }

    // ===== 橋梁: 跳開橋（bridge:bascule_bridge）=====
    // leaves … 1=単葉 / 2=双葉。
    [JsonPropertyName("bridge_leaves")] public int? BridgeLeaves { get; set; }
    [JsonPropertyName("bridge_leaf_span")] public int? BridgeLeafSpan { get; set; }
    // open_angle … 跳開角。勝鬨橋は70秒で70度まで開く設計（土木学会）。
    [JsonPropertyName("bridge_open_angle")] public int? BridgeOpenAngle { get; set; }
    // counterweight … 釣合い錘（トラニオン後方・橋脚内のピットに降りる）。
    [JsonPropertyName("bridge_counterweight")] public bool BridgeCounterweight { get; set; }
    [JsonPropertyName("bridge_machine_house")] public bool BridgeMachineHouse { get; set; }
}
