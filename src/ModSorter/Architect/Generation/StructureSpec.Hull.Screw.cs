using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 推進器（プロペラ・軸・舵）のプロパティ。StructureSpec の partial。
// 既存の StructureSpec.Hull.cs は5,220バイトあり、機走船の船種が増えると
// ここが伸びるので最初から別ファイルへ分けた。
//
// 径・軸の通り・舵の寸法はすべて主要目（喫水・船尾の立ち上がり・船尾の幅）から
// 出すので、増えるプロパティは軸数の1つだけ。値を持たせるほど
// 「径が喫水より大きくて空転する」「舵が船体から離れて浮く」といった
// 破綻をUIから作れてしまうため、寸法は受け取らない。
public sealed partial class StructureSpec
{
    // スクリューの軸数。0でなし、1で中心線に1軸、2で左右に2軸。
    // 機走船だけが持つ。帆船・端艇は0のまま。
    // 3軸以上（近代軍艦の4軸など）は軸の振り分けを決めてから足す。
    [JsonPropertyName("hull_screws")] public int? HullScrews { get; set; }
}
