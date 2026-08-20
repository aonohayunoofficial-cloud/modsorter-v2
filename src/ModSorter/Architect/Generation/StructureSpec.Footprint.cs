using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// StructureSpec の平面まわり。フットプリント（非矩形平面）と複数ボリューム合成。
// 平面を表す補助型（FootprintParams / Rect / VolumePart）もここに置く。
public sealed partial class StructureSpec
{
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
