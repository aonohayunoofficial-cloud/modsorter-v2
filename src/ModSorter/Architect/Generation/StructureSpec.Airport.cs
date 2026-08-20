using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 空港（structure_type="airport:<種類>"）のプロパティ。StructureSpec の partial。
// 大きくなったので地上側と建物側で分けた。
//   StructureSpec.Airport.cs           平面土木（滑走路・誘導路・エプロン）・進入灯・ヘリポート
//   StructureSpec.Airport.Buildings.cs 管制塔・旅客ターミナル・貨物ターミナル・格納庫
public sealed partial class StructureSpec
{
    // ===== 空港の平面土木施設（structure_type="airport:"）=====
    // ショルダー幅（片側）。滑走路・誘導路の舗装の外側に付く路肩。
    // 誘導路のショルダーは実物で 9.5m 級。エプロンでは走行路（タキシレーン）の幅として使う。
    [JsonPropertyName("airport_shoulder")] public int? AirportShoulder { get; set; }

    // 標識（マーキング）を描くか。false で舗装面だけになる。
    [JsonPropertyName("airport_marking")] public bool AirportMarking { get; set; }

    // 中心線標識の周期（滑走路）。0 で実線。実物は長 30m・間隔 20m の破線。
    [JsonPropertyName("airport_center_step")] public int? AirportCenterStep { get; set; }

    // 進入端標識の縦縞の本数。0 で無し。実物は幅 45m の滑走路で 8 本。
    // 縦縞の寸法は幅 30m 以上の滑走路とそれ未満とで別に定められている。
    [JsonPropertyName("airport_threshold")] public int? AirportThreshold { get; set; }

    // 接地帯標識の対の数。0 で無し。実物は進入端から一定間隔で並ぶ。
    [JsonPropertyName("airport_touchdown")] public int? AirportTouchdown { get; set; }

    // 縁灯の間隔。0 で灯火なし。舗装の両縁に沿って y=1 に並ぶ。
    [JsonPropertyName("airport_edge_light")] public int? AirportEdgeLight { get; set; }

    // エプロンのスポット（駐機場）数と 1 スポットの幅。
    [JsonPropertyName("airport_spots")] public int? AirportSpots { get; set; }
    [JsonPropertyName("airport_spot_width")] public int? AirportSpotWidth { get; set; }

    // 空港の平面土木施設で 1 マスが表す実寸(m)。既定 1（1マス=1m）。
    // 標識の寸法は ICAO の規定どおり m で持ち、この値で割ってマスへ落とす。
    // 10 にすると滑走路 2500m 級の全体像を 64 マスに収められる。
    [JsonPropertyName("airport_scale")] public int? AirportScale { get; set; }

    // ===== 進入灯（structure_type="airport:approach_light"）=====
    // 平面土木と同じく airport_scale で m からマスへ落とす。
    // depth は進入端から手前へ伸ばす長さ（マス）。width は滑走路の幅。

    // 灯火システムの種類。"cat1"（既定） | "cat2" | "simple"。
    [JsonPropertyName("airport_als_type")] public string? AirportAlsType { get; set; }

    // バレット（短い灯列）を使うか。true だとクロスバーが減る（Annex 14 の規定）。
    [JsonPropertyName("airport_als_barrette")] public bool AirportAlsBarrette { get; set; }

    // 進入灯橋（架台）の高さ（マス）。0 で地面置き。海上・傾斜地の進入灯に使う。
    [JsonPropertyName("airport_als_trestle")] public int? AirportAlsTrestle { get; set; }

    // 進入路指示灯（PAPI）を置くか。滑走路の左側、進入端から 300m の位置。
    [JsonPropertyName("airport_papi")] public bool AirportPapi { get; set; }

    // ===== ヘリポート（structure_type="airport:helipad"）=====

    // D 値（設計ヘリコプターの全長）の実寸(m)。すべての寸法がここから決まる。
    [JsonPropertyName("airport_heli_d")] public int? AirportHeliD { get; set; }

    // 進入区域の識別のため FATO を 1D にするか。false だと 0.83D（限定用途）。
    [JsonPropertyName("airport_heli_full_fato")] public bool AirportHeliFullFato { get; set; }

    // 高架式にするときの高さ（マス）。0 で地上式。
    [JsonPropertyName("airport_heli_elevated")] public int? AirportHeliElevated { get; set; }
}
