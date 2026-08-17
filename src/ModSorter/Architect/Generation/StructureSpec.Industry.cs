using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 産業インフラ（structure_type="industry:<種類>"）のプロパティ。StructureSpec の partial。
// 分野ごとにファイルを割る最初の1枚。以降 Ship / Harbor / Airport / Rail / Bridge も
// 同じ形で切り出す。
public sealed partial class StructureSpec
{
    // ===== 縦型容器 共通（サイロ・給水塔・タンク）=====
    // 円筒の直径（マス）。1マス=1m。
    [JsonPropertyName("industry_diameter")] public int? IndustryDiameter { get; set; }

    // 胴（円筒）の高さ。給水塔では水槽の有効水深として使う。
    [JsonPropertyName("industry_body_height")] public int? IndustryBodyHeight { get; set; }

    // 屋根の形。"dome"（既定）| "cone"（円錐）| "flat"（陸屋根）。
    [JsonPropertyName("industry_roof")] public string? IndustryRoof { get; set; }

    // 屋根の 1/n。円錐屋根では勾配（run n マスにつき rise 1マス）、
    // ドーム屋根では高さ＝直径の 1/n。
    [JsonPropertyName("industry_roof_pitch")] public int? IndustryRoofPitch { get; set; }

    // 外部ラダーを付けるか。
    [JsonPropertyName("industry_ladder")] public bool IndustryLadder { get; set; }

    // 点検口（サイロは頂部ハッチ、タンクは側板マンホール、給水塔は塔身の出入口、
    // 風車は塔の出入口）。
    [JsonPropertyName("industry_manhole")] public bool IndustryManhole { get; set; }

    // 外部ラダーを置く方角。"north" | "south" | "east" | "west"。未指定は south。
    [JsonPropertyName("industry_ladder_face")] public string? IndustryLadderFace { get; set; }

    // 開口部（サイロの払い出し口・給水塔の塔身出入口・タンクの側板マンホール）の方角。
    // 未指定は north。梯子と同じ方角にすると梯子が開口を塞ぐ。
    [JsonPropertyName("industry_opening_face")] public string? IndustryOpeningFace { get; set; }

    // ===== サイロ =====
    // スカート支持の高さ。0で直置き。
    [JsonPropertyName("industry_skirt")] public int? IndustrySkirt { get; set; }

    // 下部ホッパー（上へ広がる漏斗）。スカートが2マス以上のときだけ入る。
    [JsonPropertyName("industry_hopper")] public bool IndustryHopper { get; set; }

    // 頂部の投入シュート。
    [JsonPropertyName("industry_chute")] public bool IndustryChute { get; set; }

    // ===== 給水塔 =====
    // 塔身（昇降路シャフト）の直径。
    [JsonPropertyName("industry_shaft_width")] public int? IndustryShaftWidth { get; set; }

    // 塔身の高さ。水槽の底までの高さ。
    [JsonPropertyName("industry_shaft_height")] public int? IndustryShaftHeight { get; set; }

    // 水槽の外周を回る点検デッキ。風車（オランダ型）では外周ギャラリーとして使う。
    [JsonPropertyName("industry_balcony")] public bool IndustryBalcony { get; set; }

    // ===== タンク =====
    // 側板の外を回るらせん階段。
    [JsonPropertyName("industry_stair")] public bool IndustryStair { get; set; }

    // 風止めリングの間隔（マス）。0でなし。
    [JsonPropertyName("industry_wind_girder")] public int? IndustryWindGirder { get; set; }

    // 防油堤の高さ（マス）。0でなし。実物は0.5m以上なので1マスが実寸相当。
    // 側板から堤までの距離は展開側で自動（直径15m未満でタンク高さの1/3、15m以上で1/2）。
    [JsonPropertyName("industry_dike")] public int? IndustryDike { get; set; }

    // ===== 風車・水車 共通（回転体）=====
    // ローター（風車）・水輪（水車）の直径。
    [JsonPropertyName("industry_rotor_diameter")] public int? IndustryRotorDiameter { get; set; }

    // 回転面の厚み。風車では翼の厚み、水車では水輪の幅。
    [JsonPropertyName("industry_rotor_width")] public int? IndustryRotorWidth { get; set; }

    // 翼・羽根の枚数。
    [JsonPropertyName("industry_blade_count")] public int? IndustryBladeCount { get; set; }

    // 回転角（度）。0で1枚目が +x を指す。姿勢を変えて並べるために持つ。
    [JsonPropertyName("industry_rotor_angle")] public int? IndustryRotorAngle { get; set; }

    // ===== 風車 =====
    // 形式。"modern"（近代・水平軸）| "dutch"（オランダ型）。未指定は modern。
    [JsonPropertyName("industry_mill_type")] public string? IndustryMillType { get; set; }

    // 塔（タワー・塔身）の高さ。
    [JsonPropertyName("industry_tower_height")] public int? IndustryTowerHeight { get; set; }

    // 塔の基部の直径。頂部は近代型で基部-2、オランダ型で基部-4まで細る。
    [JsonPropertyName("industry_tower_base")] public int? IndustryTowerBase { get; set; }

    // ナセル（近代型の機械室）を付けるか。
    [JsonPropertyName("industry_nacelle")] public bool IndustryNacelle { get; set; }

    // ===== 水車 =====
    // 水の掛け方。"overshot"（上掛け）| "breast"（胸掛け）| "undershot"（下掛け）。
    [JsonPropertyName("industry_wheel_type")] public string? IndustryWheelType { get; set; }

    // 水車小屋を付けるか。軸が小屋の壁を貫いて中へ入る。
    [JsonPropertyName("industry_mill_house")] public bool IndustryMillHouse { get; set; }

    // 導水路（上掛け・胸掛けで水輪へ水を運ぶ樋）を付けるか。
    [JsonPropertyName("industry_flume")] public bool IndustryFlume { get; set; }
}
