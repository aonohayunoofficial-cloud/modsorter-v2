using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 鉄道（structure_type="railway:<種類>"）のプロパティ。StructureSpec の partial。
// プラットフォーム・上屋・跨線橋・車庫・駅舎で共用する。
public sealed partial class StructureSpec
{
    // ===== 鉄道（structure_type="railway:<種類>"）=====
    // 1マス=1m。空港の建物系と同じ縮尺なので並べても寸法が食い違わない。
    // プラットフォームでは width=ホーム幅、depth=ホーム長、
    // height=レール面からホーム天端までの高さとして使う。

    // ホームの形式。"island"（島式1面2線・既定）| "side"（単式1面1線）|
    // "opposed"（相対式2面2線）。
    [JsonPropertyName("rail_platform_type")] public string? RailPlatformType { get; set; }

    // 相対式のときの2線の軌道中心間隔（マス）。在来線 4.0m、新幹線 4.3m。
    [JsonPropertyName("rail_track_pitch")] public int? RailTrackPitch { get; set; }

    // ホーム端から先へ道床を伸ばす長さ（マス）。0 でホーム長ちょうど。
    // レール（機能ブロック）は置かないので、伸びるのは道床だけ。
    [JsonPropertyName("rail_track_margin")] public int? RailTrackMargin { get; set; }

    // ホームドア（可動式ホーム柵）の高さ（マス）。0 でなし。腰高タイプは 1.3m 級。
    [JsonPropertyName("rail_platform_door")] public int? RailPlatformDoor { get; set; }

    // 縁端警告の点状ブロックを敷くか。縁端から 80cm 以上離す規定なので 1 マス内側に置く。
    [JsonPropertyName("rail_tactile")] public bool RailTactile { get; set; }

    // ホーム端を勾配で落とすか。実物のホーム端は斜路で下がる。
    [JsonPropertyName("rail_end_ramp")] public bool RailEndRamp { get; set; }

    // 高架の路盤面の高さ（マス）。0 で地上。3 以上で床版と橋脚が入る。
    [JsonPropertyName("rail_viaduct")] public int? RailViaduct { get; set; }

    // 橋脚の間隔（マス）。ラーメン高架橋の柱スパンは 8〜10m 級。
    [JsonPropertyName("rail_pier_step")] public int? RailPierStep { get; set; }

    // 屋根の形。上屋・跨線橋・車庫で共通。
    // "gable"（切妻）| "shed"（片流れ）| "flat"（陸屋根）| "arch"（アーチ）。
    [JsonPropertyName("rail_canopy_roof")] public string? RailCanopyRoof { get; set; }

    // 軒の出（マス）。0 でホーム幅ちょうど。1 以上にすると軌道の上へ張り出すので、
    // 建築限界（軌道中心±1.9m・高さ5.7m）を避けるため軒高を 6 以上へ引き上げる。
    [JsonPropertyName("rail_eave")] public int? RailEave { get; set; }

    // 柱の列数。1（中央1列・Y型）または 2（両側2列）。
    [JsonPropertyName("rail_column_rows")] public int? RailColumnRows { get; set; }

    // 柱の間隔（マス）。古レール上屋は約4.5m、現代は5m級。
    [JsonPropertyName("rail_column_step")] public int? RailColumnStep { get; set; }

    // 屋根勾配。何マス進んで1マス上がるか。3 ≒ 18度、4 ≒ 14度。
    [JsonPropertyName("rail_roof_pitch")] public int? RailRoofPitch { get; set; }

    // 雨といを付けるか。軒先の1マス下に通す。
    [JsonPropertyName("rail_gutter")] public bool RailGutter { get; set; }

    // 照明の間隔（マス）。0 で照明なし。
    [JsonPropertyName("rail_light_step")] public int? RailLightStep { get; set; }

    // 跨線橋が跨ぐ長さ（マス）。通路の走る方向の全長。
    [JsonPropertyName("rail_span")] public int? RailSpan { get; set; }

    // 階段の付き方。"both"（両端・既定）| "one"（片側だけ）| "none"（階段なし）。
    [JsonPropertyName("rail_stair")] public string? RailStair { get; set; }

    // 階段の幅（マス）。バリアフリー誘導基準は 140cm 以上。
    [JsonPropertyName("rail_stair_width")] public int? RailStairWidth { get; set; }

    // 階段の踏面（マス）。1マス上がるごとに何マス進むか。
    // 2 ≒ 26.6度で、実物の蹴上げ0.16m・踏面0.30m（約28度）に最も近い。
    [JsonPropertyName("rail_stair_run")] public int? RailStairRun { get; set; }

    // 腰壁・手すりの高さ（マス）。
    [JsonPropertyName("rail_wall_height")] public int? RailWallHeight { get; set; }

    // 跨線橋に屋根を付けるか。
    [JsonPropertyName("rail_covered")] public bool RailCovered { get; set; }

    // 車庫の線数。
    [JsonPropertyName("rail_tracks")] public int? RailTracks { get; set; }

    // 検車ピットの深さ（マス）。0 でピットなし。実物は 1.2m 級。
    [JsonPropertyName("rail_pit")] public int? RailPit { get; set; }

    // 屋上点検ホームを付けるか。車両屋根上（およそ3.6m）の高さに通路を回す。
    [JsonPropertyName("rail_roof_walk")] public bool RailRoofWalk { get; set; }

    // 車庫の扉を閉めた状態で描くか。false なら開口のまま。
    [JsonPropertyName("rail_shutter")] public bool RailShutter { get; set; }

    // 事務所棟の奥行き（マス）。0 で事務所棟なし。
    [JsonPropertyName("rail_annex")] public int? RailAnnex { get; set; }

    // ===== 駅舎（structure_type="railway:station_building"）=====
    // 地平・高架下は width=奥行き（線路と直交）/ depth=桁行き（線路方向）。
    // 橋上は rail_span=線路を横切る長さ / depth=桁行き / height=桁下高さ。

    // 駅舎の形式。"ground"（地平・既定）| "bridge"（橋上）| "elevated"（高架下）。
    [JsonPropertyName("rail_station_type")] public string? RailStationType { get; set; }

    // コンコースの天井高（マス）。みなし規定で3.0m以上、橋上の実施例は5.5〜6.8m。
    [JsonPropertyName("rail_concourse")] public int? RailConcourse { get; set; }

    // 改札通路の数。1通路あたり「機械2マス（通行方向の長さ）＋通路幅」で並ぶ。
    [JsonPropertyName("rail_gates")] public int? RailGates { get; set; }

    // 幅広通路にするか。実物900mm。移動等円滑化基準の有効幅90cm以上を満たす。
    [JsonPropertyName("rail_gate_wide")] public bool RailGateWide { get; set; }

    // 券売機の台数（マス）。0 でなし。改札外の壁沿いに並ぶ。
    [JsonPropertyName("rail_ticket")] public int? RailTicket { get; set; }

    // 待合室の桁行き（マス）。3 未満でなし。改札内に置く。
    [JsonPropertyName("rail_waiting")] public int? RailWaiting { get; set; }

    // 駅務室の桁行き（マス）。3 未満でなし。改札ラッチの外側に置く。
    [JsonPropertyName("rail_office")] public int? RailOffice { get; set; }

    // 便所を付けるか。改札外の妻側に置く。
    [JsonPropertyName("rail_toilet")] public bool RailToilet { get; set; }

    // エレベーターを付けるか。かご内法 幅140cm以上×奥行135cm以上（11人乗り以上）に
    // 壁を足して3マス角のシャフトになる。
    [JsonPropertyName("rail_elevator")] public bool RailElevator { get; set; }

    // 出入口・ホーム連絡口の有効幅（マス）。自由通路の実施例は幅員4m。
    [JsonPropertyName("rail_passage")] public int? RailPassage { get; set; }

    // 車寄せの庇の張り出し（マス）。0 でなし。地平・高架下でだけ使う。
    [JsonPropertyName("rail_entrance_canopy")] public int? RailEntranceCanopy { get; set; }
}
