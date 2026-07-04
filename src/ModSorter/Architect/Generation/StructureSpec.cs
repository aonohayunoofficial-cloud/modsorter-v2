using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// モデルが吐く中間表現。座標ではなく「設計意図」だけを持つ。
// 座標への展開は StructureExpander が確定的に行う。
public sealed class StructureSpec
{
    [JsonPropertyName("width")] public int Width { get; set; }  // x方向 W
    [JsonPropertyName("depth")] public int Depth { get; set; }  // z方向 D
    [JsonPropertyName("height")] public int Height { get; set; }  // y方向 H

    // 各面の素材（許可ブロックIDのいずれか）。未指定時は wall_block を流用。
    [JsonPropertyName("floor_block")] public string? FloorBlock { get; set; }
    [JsonPropertyName("roof_block")] public string? RoofBlock { get; set; }
    [JsonPropertyName("wall_block")] public string? WallBlock { get; set; }

    // 屋根の形: "flat"（平屋根・既定） または "gable"（切妻・三角）
    [JsonPropertyName("roof_type")] public string? RoofType { get; set; }

    // gable のときの棟の向き: "x"（棟がx軸に平行・z方向に傾斜） または "z"
    [JsonPropertyName("ridge_axis")] public string? RidgeAxis { get; set; }

    // 中間床を入れる高さ(y)のリスト。例: [3] なら y=3 に2階の床。複数指定で3階建て以上。
    // 1階の床(y=0)と屋根は別管理なので、ここには中間の階の床だけを入れる。
    [JsonPropertyName("floor_levels")] public List<int> FloorLevels { get; set; } = new();

    // 柱型リズム（pilaster）用のアクセント材。未指定なら wall_block と同じ＝柱が目立たない。
    // 例: 壁が oak_planks のとき accent_block を oak_log にすると柱だけ丸太になる。
    [JsonPropertyName("accent_block")] public string? AccentBlock { get; set; }

    // 柱を立てる間隔。2以上で有効、未指定/1以下なら柱なし（角だけは accent になる）。
    // 例: 3 なら外周に沿って3マスごとに柱を立てる。
    [JsonPropertyName("pilaster_step")] public int? PilasterStep { get; set; }

    // 土台段（base course）を作るか。true で y=0 の外周一周を base_block に差し替える。
    // 未指定(false)なら土台なし＝従来の見た目。座標系は変えない（張り出しはしない）。
    [JsonPropertyName("has_base")] public bool HasBase { get; set; }

    // 土台段の素材。未指定なら floor_block と同じ＝差し替えても見た目が変わらない。
    // 例: 床が oak_planks のとき base_block を cobblestone にすると足元だけ石の基礎になる。
    [JsonPropertyName("base_block")] public string? BaseBlock { get; set; }

    // ドーム屋根(roof_type="dome")の高さ。未指定なら水平半径から自動。
    [JsonPropertyName("dome_height")] public int? DomeHeight { get; set; }

    // 建物の様式: "walled"（既定・壁のある建物） または "colonnade"（壁のない開放型・列柱）
    [JsonPropertyName("building_style")] public string? BuildingStyle { get; set; }

    // ファサード型(temple)の正面の向き。柱廊をどの面に置くか。
    // "north" | "south" | "east" | "west"。未指定なら "south"。
    [JsonPropertyName("facade_face")] public string? FacadeFace { get; set; }

    // 全体の構造タイプ。"building"（既定・通常の建物。床/壁/屋根/開口部のロジックを通す）
    // または特殊形状。特殊形状は床/壁/屋根/開口部を一切作らず、専用ビルダーが座標を作る。
    // "building"（既定） | "ramp"（スロープ） | "bridge"（橋） | "ship"（船）。今後 pool 等を追加予定。
    // "ship" のときは ShipExpander が船体・甲板・上部構造物・出入口を確定的に作る。
    // 通常の開口部/入口保証は通さない（出入口は船種ごとに ShipExpander が自動配置する）。
    [JsonPropertyName("structure_type")] public string? StructureType { get; set; }

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

    // 開口部（窓・ドア）。面と面内の相対位置で指定する。
    [JsonPropertyName("openings")] public List<Opening> Openings { get; set; } = new();

    // ===== 平面形状（フットプリント）=====
    // 建物の平面(X-Z)を矩形以外にするための指定。未指定なら従来どおり width×depth の矩形。
    // 展開は StructureExpander.BuildFootprint が確定的に行い、床・土台・壁・平屋根は
    // このマスクの範囲だけに作られる。非矩形のときは屋根が自動で "flat" に、
    // 様式が "walled" 相当にフォールバックする（gable/dome/pyramid/colonnade/temple は
    // 矩形前提のため。将来のフェーズで対応予定）。
    //
    // 形状の決め方（後勝ちではなく集合演算）:
    //   1. footprint_shape のプリセットで大枠を作る（"rect" 既定 / "l" / "u" / "t" / "plus"）。
    //   2. footprint_add の矩形をすべて OR で足す。
    //   3. footprint_sub の矩形をすべて削る（最後に一括で引く）。
    // add をすべて足してから sub をすべて引くため、add 同士・sub 同士の順序は結果に影響しない。
    //
    // プリセット "l"（L字）: 右下(x大・z大)の一角を削った形。削る大きさは footprint_params
    //   の cut_w / cut_d（未指定なら幅・奥行のおよそ半分）。
    // プリセット "u"（コの字）: 手前(z大側)の中央を削り込む。開口幅は cut_w、深さは cut_d。
    // プリセット "t"（T字）: 縦棒＋横棒。横棒は z 小側、縦棒は中央。太さは cut_w / cut_d。
    // プリセット "plus"（十字）: 中央の縦帯＋横帯。帯の太さは cut_w / cut_d。
    [JsonPropertyName("footprint_shape")] public string? FootprintShape { get; set; }

    // プリセットの寸法パラメータ（省略可）。cut_w は x 方向、cut_d は z 方向の切り欠き/帯幅。
    [JsonPropertyName("footprint_params")] public FootprintParams? FootprintParams { get; set; }

    // 追加する矩形（プリセットに OR で足す）。座標は 0..width-1 / 0..depth-1 の範囲で解釈。
    [JsonPropertyName("footprint_add")] public List<Rect> FootprintAdd { get; set; } = new();

    // 削る矩形（すべての add を足した後に一括で引く）。窓や中庭ではなく平面の欠けを作る用途。
    [JsonPropertyName("footprint_sub")] public List<Rect> FootprintSub { get; set; } = new();

    // ===== 複数ボリューム合成（フェーズ2）=====
    // 双胴船のように「離れた複数の塊」を1つの構造として合成するための指定。
    // 空（既定）なら従来どおり単一の箱として展開する（後方互換）。
    // 各 VolumePart は完全な StructureSpec を内包し、オフセット分だけ平行移動して重ねる。
    // 重なったセルは後勝ち（リストで後ろの Part が上書き）。
    [JsonPropertyName("volumes")] public List<VolumePart> Volumes { get; set; } = new();
}

// 複数ボリューム合成の1要素（フェーズ2）。
// Part（完全な StructureSpec）を Offset だけ平行移動して合成する。
// Offset は全体原点からの絶対配置。負値は Expander 側で 0 にクランプされる。
public sealed class VolumePart
{
    [JsonPropertyName("offset_x")] public int OffsetX { get; set; }  // x方向のずらし
    [JsonPropertyName("offset_y")] public int OffsetY { get; set; }  // y方向のずらし（浮上可）
    [JsonPropertyName("offset_z")] public int OffsetZ { get; set; }  // z方向のずらし

    // この要素の中身。単一の箱として展開される（内部の volumes は無視＝再帰1段まで）。
    [JsonPropertyName("part")] public StructureSpec? Part { get; set; }
}

// フットプリントのプリセット寸法。指定がなければ Expander 側で妥当な既定を計算する。
public sealed class FootprintParams
{
    // x 方向の切り欠き幅／帯幅。0 以下なら未指定扱い（自動）。
    [JsonPropertyName("cut_w")] public int CutW { get; set; }

    // z 方向の切り欠き奥行／帯幅。0 以下なら未指定扱い（自動）。
    [JsonPropertyName("cut_d")] public int CutD { get; set; }
}

// 平面上の矩形領域。X,Z は左手前の角（0 起点）、W,D はその大きさ（マス数）。
// 範囲外にはみ出す指定は Expander 側で width/depth にクランプされる。
public sealed class Rect
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("z")] public int Z { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("d")] public int D { get; set; }
}

// 開口部1つ。座標ではなく「どの面の、どのあたりか」で表す。
public sealed class Opening
{
    // "north" | "south" | "east" | "west"
    [JsonPropertyName("face")] public string Face { get; set; } = "";

    // "window" | "door"
    [JsonPropertyName("kind")] public string Kind { get; set; } = "window";

    // 面に沿った位置（端=0 から数えた何番目か）。中央寄りなら W/2 や D/2 あたり。
    [JsonPropertyName("offset")] public int Offset { get; set; }

    // 下から何段目か（床のすぐ上=1）。door は通常1、window は1〜2あたり。
    [JsonPropertyName("level")] public int Level { get; set; } = 1;

    // 窓に使うブロック（kind=window のとき）。未指定なら glass。
    [JsonPropertyName("block")] public string? Block { get; set; }
}
