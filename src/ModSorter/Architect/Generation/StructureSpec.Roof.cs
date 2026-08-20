using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// StructureSpec の屋根まわり。屋根形状・採光・パラペット・軒・煙突。
public sealed partial class StructureSpec
{
    // 屋根の形: "flat"（平屋根・既定） または "gable"（切妻・三角）
    [JsonPropertyName("roof_type")] public string? RoofType { get; set; }

    // 屋根勾配（gable/gable_stairs のとき有効）。run 何マス進むごとに rise 1マス上げるか。
    // 1 = 1マスにつき1段＝45°（既定・従来と同じ後方互換）。
    // 2 ≒ 26.6°（6:12相当の標準的な緩勾配）、3 ≒ 18.4°（4:12相当）、と大きいほど緩い。
    // null/0/1 はすべて 1（45°）として扱う。
    [JsonPropertyName("roof_pitch")] public int? RoofPitch { get; set; }

    // gable のときの棟の向き: "x"（棟がx軸に平行・z方向に傾斜） または "z"
    [JsonPropertyName("ridge_axis")] public string? RidgeAxis { get; set; }

    // ドーム屋根(roof_type="dome")の高さ。未指定なら水平半径から自動。
    [JsonPropertyName("dome_height")] public int? DomeHeight { get; set; }

    // 鋸屋根(roof_type="sawtooth")の山の数。0/未指定なら長さから自動（1山およそ6マス）。
    [JsonPropertyName("sawtooth_bays")] public int? SawtoothBays { get; set; }

    // モニター屋根(roof_type="monitor")の越し屋根の幅（傾斜方向のマス数）。
    // 0/未指定なら傾斜方向のおよそ1/3。
    [JsonPropertyName("monitor_width")] public int? MonitorWidth { get; set; }

    // モニター屋根の立ち上がり高さ（棟から天面までのマス数）。0/未指定なら2。
    [JsonPropertyName("monitor_height")] public int? MonitorHeight { get; set; }

    // 鋸屋根・モニター屋根の垂直採光面に使うブロック。未指定ならガラス。
    [JsonPropertyName("glazing_block")] public string? GlazingBlock { get; set; }

    // パラペット（陸屋根の立ち上がり）。屋根面の外周を屋根の上へ何マス立ち上げるか。
    // 0（既定）でパラペットなし。対応する屋根形状は flat のみ（勾配屋根では無視される）。
    [JsonPropertyName("parapet_height")] public int? ParapetHeight { get; set; }

    // パラペットの素材。未指定なら wall_block を流用。
    [JsonPropertyName("parapet_block")] public string? ParapetBlock { get; set; }

    // パラペットの最上段に狭間（クレネル）を抜くか。true で矢壁と狭間の凹凸になる。
    // 抜くのは最上段だけなので、下の段が環として残り屋根面は隠れたまま。
    // parapet_height が0のときは無視される（flat 以外でも無視）。
    [JsonPropertyName("parapet_crenel")] public bool ParapetCrenel { get; set; }

    // 狭間の周期（マス）。3（既定）で「矢壁2マス＋狭間1マス」。2〜6にクランプ。
    // 縁が x 方向に走る位置は x、z 方向に走る位置は z を周期で判定するので、
    // 向かい合う壁の狭間が揃う。角（両方向の縁が交わる位置）は必ず矢壁を残す。
    [JsonPropertyName("parapet_crenel_step")] public int? ParapetCrenelStep { get; set; }

    // ===== 煙突 =====
    // 本数。0（既定）なら煙突なし。1以上で屋根の上に自動で等間隔に立てる。
    [JsonPropertyName("chimney_count")] public int ChimneyCount { get; set; }

    // 建物内部を貫くか。true=床(y=1)から屋根を貫いて上端まで通す（暖炉風）。
    // false=屋根の上に出る部分だけ（見た目だけの煙突）。
    [JsonPropertyName("chimney_pierce")] public bool ChimneyPierce { get; set; }

    // 寄せ方向。"center"（既定・中心線上） | "north" | "south" | "east" | "west"。
    // 寄せた方向へ列全体が寄り、それと直交する軸に沿って本数ぶん等間隔に並ぶ。
    [JsonPropertyName("chimney_align")] public string? ChimneyAlign { get; set; }

    // 屋根の上に出す高さ（マス）。未指定/0以下なら既定 2。
    [JsonPropertyName("chimney_height")] public int? ChimneyHeight { get; set; }

    // 煙突の素材。未指定なら roof_block → wall_block の順で流用。
    [JsonPropertyName("chimney_block")] public string? ChimneyBlock { get; set; }

    // 煙突の太さ。"thin"（既定・中実1マス柱） | "medium"（プラス型・中空） | "thick"（4×4外周・中空2×2）。
    // medium/thick の中空は全高（貫通ONなら床から屋根上まで）にわたって適用する。
    [JsonPropertyName("chimney_thickness")] public string? ChimneyThickness { get; set; }

    // 軒の出（eave overhang）。屋根を壁より外側へ何マス張り出すか。0（既定）で軒なし。
    // 対応する屋根形状は flat / gable / shed のみ。pyramid/dome/gable_stairs では無視される。
    // 実装は屋根の軒先を水平に外へ伸ばし、最後に建物全体を +eave シフトして負座標を出さない。
    [JsonPropertyName("eave_overhang")] public int? EaveOverhang { get; set; }

    // 軒をどの面に出すか（面の外側へ張り出す）。EaveOverhang>0 のとき有効。
    // north=z<0側 / south=z>=d側 / west=x<0側 / east=x>=w側。既定は全 false（＝軒なし）。
    [JsonPropertyName("eave_north")] public bool EaveNorth { get; set; }
    [JsonPropertyName("eave_south")] public bool EaveSouth { get; set; }
    [JsonPropertyName("eave_east")] public bool EaveEast { get; set; }
    [JsonPropertyName("eave_west")] public bool EaveWest { get; set; }
}
