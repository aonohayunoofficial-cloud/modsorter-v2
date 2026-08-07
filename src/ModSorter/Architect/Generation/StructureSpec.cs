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

    // 屋根勾配（gable/gable_stairs のとき有効）。run 何マス進むごとに rise 1マス上げるか。
    // 1 = 1マスにつき1段＝45°（既定・従来と同じ後方互換）。
    // 2 ≒ 26.6°（6:12相当の標準的な緩勾配）、3 ≒ 18.4°（4:12相当）、と大きいほど緩い。
    // null/0/1 はすべて 1（45°）として扱う。
    [JsonPropertyName("roof_pitch")] public int? RoofPitch { get; set; }

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

    // 塔屋（屋上の機械室・階段室）の平面寸法。3未満なら塔屋を作らない。
    [JsonPropertyName("penthouse_width")] public int? PenthouseWidth { get; set; }
    [JsonPropertyName("penthouse_depth")] public int? PenthouseDepth { get; set; }

    // 塔屋の高さ（屋根面から上へ何マス）。0/未指定で塔屋なし。
    // 対応する屋根形状は flat のみ（勾配屋根では無視される）。
    [JsonPropertyName("penthouse_height")] public int? PenthouseHeight { get; set; }

    // 塔屋の壁材。未指定なら wall_block を流用。天面は roof_block。
    [JsonPropertyName("penthouse_block")] public string? PenthouseBlock { get; set; }

    // 塔屋の寄せ方向。"center"（既定）| "north" | "south" | "east" | "west" |
    // "northeast" | "northwest" | "southeast" | "southwest"（4隅寄せ）。
    // north が z の小さい側、south が z の大きい側、west が x の小さい側、east が x の大きい側。
    // 展開側は文字列に north/south/east/west が含まれるかで x・z の寄せを独立に決めるため、
    // "north_east" のような区切り付きの表記でも同じ結果になる。
    [JsonPropertyName("penthouse_align")] public string? PenthouseAlign { get; set; }

    // 建物の様式: "walled"（既定・壁のある建物） または "colonnade"（壁のない開放型・列柱）
    [JsonPropertyName("building_style")] public string? BuildingStyle { get; set; }

    // ファサード型(temple)の正面の向き。柱廊をどの面に置くか。
    // "north" | "south" | "east" | "west"。未指定なら "south"。
    [JsonPropertyName("facade_face")] public string? FacadeFace { get; set; }

    // ===== 塔（鐘塔・尖塔・ミナレット）=====
    // 建物の平面内に立てる正方形の塔の一辺（マス）。3未満/未指定なら塔なし。
    [JsonPropertyName("tower_width")] public int? TowerWidth { get; set; }

    // 塔の高さ（壁の上端 y=height-1 から塔の壁の上端まで何マス）。0/未指定なら塔なし。
    // penthouse と違い屋根形状を問わず作る。棟より低いと屋根に埋まる。
    [JsonPropertyName("tower_height")] public int? TowerHeight { get; set; }

    // 塔の位置。"front"（既定・正面の中央） | "front_corners"（正面の両角） |
    // "four_corners"（四隅） | "center"（平面の中央） | "rear"（背面の中央）。
    // 「正面」は facade_face で決まる。
    [JsonPropertyName("tower_align")] public string? TowerAlign { get; set; }

    // 塔の頂部の形。"spire"（既定・尖塔） | "dome"（丸屋根） | "flat"（陸屋根）。
    [JsonPropertyName("tower_roof")] public string? TowerRoof { get; set; }

    // 塔の壁材。未指定なら wall_block を流用。
    [JsonPropertyName("tower_block")] public string? TowerBlock { get; set; }

    // 塔の頂部の素材。未指定なら roof_block を流用。
    [JsonPropertyName("tower_roof_block")] public string? TowerRoofBlock { get; set; }

    // 鐘楼の開口を作るか。true で塔の上端付近の四面中央を抜く（tower_height が4以上のとき）。
    [JsonPropertyName("tower_belfry")] public bool TowerBelfry { get; set; }

    // ===== 縁側／基壇の縁 =====
    // 平面の外側へこの幅だけ、y=0 に床を敷き足す（マス）。0/未指定なら無し。
    // 寺社の縁（深い軒の下の回り縁）、神殿の基壇の縁石に使う。
    // 軒と同じく一時的に負座標を作るが、ExpandCore 末尾の一括シフトで 0 以上へ寄る。
    [JsonPropertyName("veranda_width")] public int? VerandaWidth { get; set; }

    // 縁側に使う素材。未指定なら base_block → floor_block を流用。
    [JsonPropertyName("veranda_block")] public string? VerandaBlock { get; set; }

    // 全体の構造タイプ。"building"（既定・通常の建物。床/壁/屋根/開口部のロジックを通す）
    // または特殊形状。特殊形状は床/壁/屋根/開口部を一切作らず、専用ビルダーが座標を作る。
    // "building"（既定） | "ramp"（スロープ） | "bridge"（橋） | "ship"（船） | "venue"（屋外会場）。
    // "ship" のときは ShipExpander が船体・甲板・上部構造物・出入口を確定的に作る。
    // "venue" のときは VenueExpander が観客席・フィールド・シェル・テントを確定的に作る。
    // 通常の開口部/入口保証は通さない（出入口はそれぞれのビルダーが自動配置する）。
    [JsonPropertyName("structure_type")] public string? StructureType { get; set; }

    // ===== 屋外イベント会場（structure_type="venue"）=====
    // 会場の種類。"arena"（円形闘技場・コロッセウム式） | "stadium"（競技場） |
    // "bandshell"（野外音楽堂） | "stage"（ステージ） | "tents"（テント広場）。
    // 未指定なら "arena"。正面の向きは facade_face を使う（既定 south）。
    [JsonPropertyName("venue_kind")] public string? VenueKind { get; set; }

    // 客席の座面材。未指定なら accent_block → wall_block を流用。
    [JsonPropertyName("seat_block")] public string? SeatBlock { get; set; }

    // 客席の段数・踏面（1段の奥行）・蹴上（1段の高さ）。
    // コロッセウムの客席勾配は37度。踏面3・蹴上2 でおよそ34度になる。
    [JsonPropertyName("venue_rows")] public int? VenueRows { get; set; }
    [JsonPropertyName("venue_run")] public int? VenueRun { get; set; }
    [JsonPropertyName("venue_rise")] public int? VenueRise { get; set; }

    // ポディウム壁の高さ。競技面から最前列までの立ち上がり（コロッセウムは5m）。
    [JsonPropertyName("venue_podium")] public int? VenuePodium { get; set; }

    // 外周壁の立ち上がり。最上段からさらに上へ何マス積むか。
    // stage では背面の幕の高さ、片面スタンドでは背面壁の高さとして使う。
    [JsonPropertyName("venue_wall")] public int? VenueWall { get; set; }

    // 屋根（arena では日除け=velarium 相当）を張るか。既定 false。
    // コロッセウムに屋根は無いので、既定は必ず屋根なしにする。
    [JsonPropertyName("venue_roof")] public bool VenueRoof { get; set; }

    // 屋根の持ち上げ量。stage では柱の高さとして使う。
    [JsonPropertyName("venue_roof_height")] public int? VenueRoofHeight { get; set; }

    // 入場路（vomitoria）を客席の下に通すか。既定 false。
    [JsonPropertyName("venue_gates")] public bool VenueGates { get; set; }

    // 競技場の形式。"bowl"（既定・四周連続のボウル） | "single"（片面スタンド単体）。
    [JsonPropertyName("venue_sides")] public string? VenueSides { get; set; }

    // 野外音楽堂: オルケストラ（円形の平場）の半径。エピダウロスは径24.65m。
    [JsonPropertyName("venue_orchestra")] public int? VenueOrchestra { get; set; }

    // 野外音楽堂: シェルの平面半径と全高。ハリウッドボウルはシェル高45ft。
    [JsonPropertyName("venue_shell_radius")] public int? VenueShellRadius { get; set; }
    [JsonPropertyName("venue_shell_height")] public int? VenueShellHeight { get; set; }

    // 舞台の高さ。bandshell では演台、stage では床までの立ち上がり。
    [JsonPropertyName("venue_stage")] public int? VenueStage { get; set; }

    // テント広場: 張数・1張の間口と奥行・軒の高さ・テント間隔・列数・列間の通路幅。
    [JsonPropertyName("venue_tent_count")] public int? VenueTentCount { get; set; }
    [JsonPropertyName("venue_tent_w")] public int? VenueTentWidth { get; set; }
    [JsonPropertyName("venue_tent_d")] public int? VenueTentDepth { get; set; }
    [JsonPropertyName("venue_tent_eave")] public int? VenueTentEave { get; set; }
    [JsonPropertyName("venue_tent_gap")] public int? VenueTentGap { get; set; }
    [JsonPropertyName("venue_tent_rows")] public int? VenueTentRows { get; set; }
    [JsonPropertyName("venue_tent_aisle")] public int? VenueTentAisle { get; set; }

    // テントの側面を壁で塞ぐか。既定 false（柱だけの開放）。
    [JsonPropertyName("venue_tent_closed")] public bool VenueTentClosed { get; set; }

    // テントの下に地面を敷くか。既定 false（敷かないので床が二重にならない）。
    [JsonPropertyName("venue_tent_pave")] public bool VenueTentPave { get; set; }

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

    // 入口の自動生成を止めるか。true で「door が1つも無ければ正面中央に1つ開ける」保証を
    // 通さない。記念碑・オベリスク・台座のように穴を開けてはいけない塊のための指定。
    // openings に明示したドア・アーチ・大開口は true でもそのまま適用される。
    [JsonPropertyName("no_entrance")] public bool NoEntrance { get; set; }

    // 開口部（窓・ドア）。面と面内の相対位置で指定する。
    [JsonPropertyName("openings")] public List<Opening> Openings { get; set; } = new();

    // ===== 平面形状（フットプリント）=====
    // 建物の平面(X-Z)を矩形以外にするための指定。未指定なら従来どおり width×depth の矩形。
    // 展開は StructureExpander.BuildFootprint が確定的に行い、床・土台・壁・平屋根は
    // このマスクの範囲だけに作られる。非矩形のときは様式が "walled" 相当にフォールバックし、
    // 棟や軒が矩形前提の屋根（gable/gable_stairs/shed/sawtooth/monitor）は "flat" に寄る。
    // 頂冠形（dome/pyramid/spire）はマスクに沿って絞れるので、平面が "circle"（かつ
    // footprint_add/footprint_sub が空）のときだけ非矩形でもそのまま使える。
    //
    // 形状の決め方（後勝ちではなく集合演算）:
    //   1. footprint_shape のプリセットで大枠を作る
    //      （"rect" 既定 / "l" / "u" / "t" / "plus" / "circle"）。
    //   2. footprint_add の矩形をすべて OR で足す。
    //   3. footprint_sub の矩形をすべて削る（最後に一括で引く）。
    // add をすべて足してから sub をすべて引くため、add 同士・sub 同士の順序は結果に影響しない。
    //
    // プリセット "l"（L字）: 右下(x大・z大)の一角を削った形。削る大きさは footprint_params
    //   の cut_w / cut_d（未指定なら幅・奥行のおよそ半分）。
    // プリセット "u"（コの字）: 手前(z大側)の中央を削り込む。開口幅は cut_w、深さは cut_d。
    // プリセット "t"（T字）: 縦棒＋横棒。横棒は z 小側、縦棒は中央。太さは cut_w / cut_d。
    // プリセット "plus"（十字）: 中央の縦帯＋横帯。帯の太さは cut_w / cut_d。
    // プリセット "circle"（円形）: width×depth を直径とする楕円。cut_w / cut_d は使わない。
    //   壁は円周1マス厚のリングになる。記念柱・円形霊廟・灯台・サイロに使う。
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

    // 開口の横幅（マス）。kind="gate"（大型シャッター/搬入口）のとき有効。0以下なら3。
    [JsonPropertyName("width")] public int Width { get; set; }

    // 開口の縦高さ（マス）。kind="gate" のとき有効。0以下なら3。
    [JsonPropertyName("height")] public int Height { get; set; }

    // 窓に使うブロック（kind=window のとき）。未指定なら glass。
    [JsonPropertyName("block")] public string? Block { get; set; }
}