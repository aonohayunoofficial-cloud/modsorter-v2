using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 港湾（structure_type="harbor:<種類>"）のプロパティ。StructureSpec の partial。
// 岸壁・桟橋・防波堤・ドライドック・灯台・クレーン・係船柱で共用する。
public sealed partial class StructureSpec
{
    // ===== 港湾（structure_type="harbor:<種類>"）=====
    // 断面は「海側が z=0、陸側が z の増加方向」で組み、facade_face が海側の向きを表す。
    // 1マス=1m。水位は y=harbor_depth の面（それより下が水面下）。

    // 計画水深（海底から水面まで）。未指定なら岸壁10・桟橋8・防波堤10。
    [JsonPropertyName("harbor_depth")] public int? HarborDepth { get; set; }

    // 天端高（水面から天端まで）。岸壁は朔望平均満潮位 +0.5〜1.5m 相当、防波堤は高め。
    [JsonPropertyName("harbor_crown")] public int? HarborCrown { get; set; }

    // 堤体幅。岸壁・防波堤ではケーソンの幅、桟橋では上部工の幅（z 方向）。
    [JsonPropertyName("harbor_body")] public int? HarborBody { get; set; }

    // エプロン幅（岸壁の背後の荷役面）。水深別に 10〜20m、コンテナ荷役では 30m 級。
    [JsonPropertyName("harbor_apron")] public int? HarborApron { get; set; }

    // 基礎マウンド（捨石）の高さ。斜面は 1:2 で外側へ広がる。0 でマウンドなし。
    [JsonPropertyName("harbor_mound")] public int? HarborMound { get; set; }

    // 消波ブロックの被覆幅（防波堤の海側）。0/未指定で消波工なし。
    [JsonPropertyName("harbor_armor")] public int? HarborArmor { get; set; }

    // 杭間隔（桟橋）。鋼管杭を格子に打つ間隔。実物は 4〜6m。
    [JsonPropertyName("harbor_pile_step")] public int? HarborPileStep { get; set; }

    // 上部工厚（桟橋）。受梁と床版を合わせた厚み。実物は 1.5〜2m。
    [JsonPropertyName("harbor_slab")] public int? HarborSlab { get; set; }

    // 渡橋の長さ（桟橋を陸側へつなぐ取付部）。0/未指定で渡橋なし。幅は 8m 前後で自動。
    [JsonPropertyName("harbor_approach")] public int? HarborApproach { get; set; }

    // クレーンレールの軌間（岸壁）。0/未指定でレールなし。30 でおよそ 100ft（30.48m）。
    [JsonPropertyName("harbor_gauge")] public int? HarborGauge { get; set; }

    // 係船柱の間隔。0/未指定で係船柱なし。曲柱の最大間隔は船型別に 10〜45m。
    [JsonPropertyName("harbor_bollard_step")] public int? HarborBollardStep { get; set; }

    // 防舷材を前面に付けるか。既定 false。
    [JsonPropertyName("harbor_fender")] public bool HarborFender { get; set; }

    // 作業段（アルター）の段数（ドライドック）。側壁を段状に下げる段の数。0 で垂直の側壁。
    // 実物のドライドックは側壁が階段状に絞られ、盤木の据付と作業の足場を兼ねる。
    [JsonPropertyName("harbor_altar_steps")] public int? HarborAltarSteps { get; set; }

    // 盤木（キールブロック）の間隔（ドライドック）。0/未指定で盤木なし。実物は 1.2〜2m。
    [JsonPropertyName("harbor_keel_step")] public int? HarborKeelStep { get; set; }

    // ゲート（ケーソンゲート）の厚み（ドライドック）。0/未指定でゲートなし＝開口のまま。
    [JsonPropertyName("harbor_gate")] public int? HarborGate { get; set; }

    // 塔身の下部直径（灯台）。上へ向かって harbor_taper に従って絞る。
    [JsonPropertyName("harbor_shaft")] public int? HarborShaft { get; set; }

    // 塔身のテーパー（灯台）。何マス上がるごとに直径を 1 絞るか。0 で絞らない（円筒）。
    [JsonPropertyName("harbor_taper")] public int? HarborTaper { get; set; }

    // 回廊（バルコニー）の張り出し（灯台）。0 で回廊なし。
    [JsonPropertyName("harbor_gallery")] public int? HarborGallery { get; set; }

    // 灯室の高さ（灯台）。回廊の上に載るガラス張りの部分。0 で灯室なし。
    [JsonPropertyName("harbor_lantern")] public int? HarborLantern { get; set; }

    // 脚の高さ（クレーン）。レール面から横行桁の下端まで。実物のコンテナクレーンは
    // 船を跨ぐため 30〜40m、荷役ヤードの橋形クレーンは 15〜18m。
    [JsonPropertyName("harbor_leg_height")] public int? HarborLegHeight { get; set; }

    // 脚の太さ（クレーン）。門形の柱1本の一辺。実物は 2〜3m 角の箱断面。
    [JsonPropertyName("harbor_leg_size")] public int? HarborLegSize { get; set; }

    // 走行方向の脚間隔（クレーン）。海側脚と陸側脚それぞれの前後スパン。
    [JsonPropertyName("harbor_leg_base")] public int? HarborLegBase { get; set; }

    // アウトリーチ（クレーン）。海側レールから海側へ張り出す桁の長さ。
    // コンテナクレーンは 38〜60m、橋形クレーンのカンチレバーは 5〜15m。
    [JsonPropertyName("harbor_outreach")] public int? HarborOutreach { get; set; }

    // バックリーチ（クレーン）。陸側レールから陸側へ張り出す桁の長さ。実物は 8〜28m。
    [JsonPropertyName("harbor_backreach")] public int? HarborBackreach { get; set; }

    // 機械室・運転室の有無（クレーン）。true で陸側脚の上に機械室、桁下に運転室を付ける。
    [JsonPropertyName("harbor_machinery")] public bool HarborMachinery { get; set; }

    // ブームの起伏（クレーン）。0 で水平、1 以上で海側の桁を跳ね上げる（何マスにつき1上げるか）。
    [JsonPropertyName("harbor_boom_raise")] public int? HarborBoomRaise { get; set; }

    // 係船柱の形（"bollard"=直柱 / "bitt"=曲柱）。単体生成のときだけ使う。
    [JsonPropertyName("harbor_bollard_type")] public string? HarborBollardType { get; set; }

    // 係船柱の柱径と高さ（単体生成）。実物は径 0.3〜0.6m・高さ 0.5〜1m。
    [JsonPropertyName("harbor_bollard_size")] public int? HarborBollardSize { get; set; }
    [JsonPropertyName("harbor_bollard_height")] public int? HarborBollardHeight { get; set; }

    // 台座の一辺（係船柱の単体生成）。0 で台座なし。
    [JsonPropertyName("harbor_pedestal")] public int? HarborPedestal { get; set; }
}
