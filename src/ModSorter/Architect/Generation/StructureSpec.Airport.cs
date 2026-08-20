using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 空港（structure_type="airport:<種類>"）のプロパティ。StructureSpec の partial。
// 平面土木（滑走路・誘導路・エプロン）と建物（管制塔・ターミナル・格納庫）、
// 灯火（進入灯）・ヘリポートで共用する。
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

    // ===== 管制塔（structure_type="airport:control_tower"）=====
    // 1マス=1m。height を「管制室の床の高さ」、width/depth を庁舎の平面寸法として使う。

    // 管制室（キャブ）の対辺幅。偶数は奇数へ丸める。FAA 標準型の床面積は
    // 234/350/625/850 sq ft ＝ 22/33/58/79 ㎡、羽田の新管制塔は約130㎡。
    [JsonPropertyName("airport_cab_width")] public int? AirportCabWidth { get; set; }

    // 管制室の窓の高さ（床から屋根まで）。最下段は腰壁（コンソールの高さ）になる。
    [JsonPropertyName("airport_cab_height")] public int? AirportCabHeight { get; set; }

    // 管制室の平面形。"octagon"（既定）| "square" | "round"。実物は八角形が最多。
    [JsonPropertyName("airport_cab_shape")] public string? AirportCabShape { get; set; }

    // 窓の傾き。何段で 1 マス外へ出すか。0 で垂直。
    // 実物は鉛直から外へ 15 度。4段＝14.0度が最も近い。
    [JsonPropertyName("airport_cab_tilt")] public int? AirportCabTilt { get; set; }

    // 管制室の外周に回すキャットウォークの張り出し。0 でなし。実物は窓清掃用に幅1m級。
    [JsonPropertyName("airport_catwalk")] public int? AirportCatwalk { get; set; }

    // シャフト（エレベーター・階段・ケーブル）の外寸。実物は 6〜10m 級。
    [JsonPropertyName("airport_shaft_width")] public int? AirportShaftWidth { get; set; }

    // シャフト内の中間床の間隔。0 で中間床なし。
    [JsonPropertyName("airport_floor_step")] public int? AirportFloorStep { get; set; }

    // 庁舎（基部の建物）の高さ。0 で庁舎なし＝塔だけ。平面は width×depth。
    [JsonPropertyName("airport_base_height")] public int? AirportBaseHeight { get; set; }

    // 屋根の上のアンテナ柱の高さ。0 でなし。
    [JsonPropertyName("airport_mast")] public int? AirportMast { get; set; }

    // ===== 旅客ターミナル（structure_type="airport:terminal"）=====
    // 1マス=1m。桁行きは「ゲート数 × ゲート間隔」の従属値なので width は使わない。
    // depth を建物の奥行き（エプロン側が z=0）として使う。

    // ゲート（搭乗口）の数。桁行きはこの数ぶん横に伸びる。
    [JsonPropertyName("airport_gates")] public int? AirportGates { get; set; }

    // ゲート1つあたりの桁行き（マス）。実物は平均 33〜40m。
    // エプロンの airport_spot_width と同じ値にすると駐機スポットと中心が揃う。
    [JsonPropertyName("airport_gate_spacing")] public int? AirportGateSpacing { get; set; }

    // 階数。実物の旅客ターミナルは出発が上階・到着が下階の2層が基本。
    [JsonPropertyName("airport_levels")] public int? AirportLevels { get; set; }

    // 階高（マス）。4〜8。搭乗橋のロタンダは実物で 5m 級・最大 8m なのでここに合わせる。
    [JsonPropertyName("airport_level_height")] public int? AirportLevelHeight { get; set; }

    // 搭乗橋（PBB）の伸長（マス）。0 でなし。実物は 15〜45m。
    [JsonPropertyName("airport_bridge")] public int? AirportBridge { get; set; }

    // 車寄せの庇の張り出し（マス）。0 でなし。
    [JsonPropertyName("airport_canopy")] public int? AirportCanopy { get; set; }

    // ターミナルの屋根。"flat"（既定・パラペット付き） | "vault"（かまぼこ屋根）。
    [JsonPropertyName("airport_terminal_roof")] public string? AirportTerminalRoof { get; set; }

    // ===== 貨物ターミナル（structure_type="airport:cargo_terminal"）=====
    // 1マス=1m。桁行きは「ドック数 × ドック間隔」の従属値なので width は使わない。
    // depth を建物の奥行き（エプロン側が z=0）、height を庫内の有効高さとして使う。

    // トラックドックの数。桁行きはこの数ぶん横に伸びる。
    // 実物の計画値は建物床面積 1,000 sq ft あたり 0.6 台＝約155㎡に1台。
    [JsonPropertyName("airport_docks")] public int? AirportDocks { get; set; }

    // ドック1台あたりの桁行き（マス）。扉の幅は 9ft（約2.7m）。
    [JsonPropertyName("airport_dock_pitch")] public int? AirportDockPitch { get; set; }

    // エアサイドの大型扉の数。0 でなし。
    [JsonPropertyName("airport_airside_doors")] public int? AirportAirsideDoors { get; set; }

    // エアサイドの大型扉の幅（マス）。偶数は奇数へ丸める。
    [JsonPropertyName("airport_door_width")] public int? AirportDoorWidth { get; set; }

    // 事務所棟の桁行き（マス）。0 でなし。倉庫の妻側に付く2層の別棟。
    [JsonPropertyName("airport_office")] public int? AirportOffice { get; set; }

    // ===== 格納庫（structure_type="airport:hangar"）=====
    // 1マス=1m。width を扉の開口幅、depth を奥行き、height を庫内の有効高さとして使う。

    // 扉の高さ（マス）。NFPA 409 は 28ft（8.5m）超で Group I 扱い。
    // 2026 年の改訂でこの境が 35ft（10.7m）へ上がった。
    [JsonPropertyName("airport_door_height")] public int? AirportDoorHeight { get; set; }

    // 収める機体の数。扉の開口が機体数ぶん横に伸びる。
    [JsonPropertyName("airport_bays")] public int? AirportBays { get; set; }

    // 屋根の形。"arch"（既定・アーチトラス） | "flat"（陸屋根） | "shed"（片流れ）。
    [JsonPropertyName("airport_hangar_roof")] public string? AirportHangarRoof { get; set; }

    // 扉の形式。"slide"（既定・引き分け戸） | "fold"（折り戸） | "open"（扉なし）。
    [JsonPropertyName("airport_door_type")] public string? AirportDoorType { get; set; }

    // 側面に付く附属棟の奥行き（マス）。0 でなし。工場・部品庫・事務所が入る。
    [JsonPropertyName("airport_annex")] public int? AirportAnnex { get; set; }

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
