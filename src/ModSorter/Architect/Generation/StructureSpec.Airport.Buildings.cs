using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 空港の建物（管制塔・旅客ターミナル・貨物ターミナル・格納庫）のプロパティ。
// StructureSpec の partial。地上側（平面土木・進入灯・ヘリポート）は
// StructureSpec.Airport.cs にある。
public sealed partial class StructureSpec
{
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
}
