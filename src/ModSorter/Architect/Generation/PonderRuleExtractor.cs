using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// Ponder の構造データ群(StructureNbtReader で読んだもの)を解析して、
// 「どのブロックの、どの方向に、何のブロックがどの向きで隣接していたか」の
// 生の統計を作る。これが後で AI へ渡す「Create ルール集」の素になる。
//
// この段階ではルール文は生成せず、生の隣接統計を出すだけ。
// まず統計を目で見て、意味のあるルールが取れているかを確認するのが目的。
public static class PonderRuleExtractor
{
    // 方向。座標差分から決める。名前は Minecraft の facing に合わせる。
    // +X=east, -X=west, +Y=up, -Y=down, +Z=south, -Z=north
    private static readonly (int dx, int dy, int dz, string name)[] Directions =
    {
        ( 1,  0,  0, "east"),
        (-1,  0,  0, "west"),
        ( 0,  1,  0, "up"),
        ( 0, -1,  0, "down"),
        ( 0,  0,  1, "south"),
        ( 0,  0, -1, "north"),
    };

    // 集計キーから除外するプロパティ(機能と無関係なノイズ)。後でデータを見て足す。
    private static readonly HashSet<string> IgnoredProps = new(StringComparer.Ordinal)
    {
        "waterlogged",
    };

    // 隣接相手としてカウントしない装飾・土台ブロック。後でデータを見て足す。
    // air は別途完全除外する。
    private static bool IsDecoration(string id)
    {
        if (id == "minecraft:air") return true;
        if (id.EndsWith("_concrete", StringComparison.Ordinal)) return true;
        if (id.EndsWith("_concrete_powder", StringComparison.Ordinal)) return true;
        if (id.EndsWith("_wool", StringComparison.Ordinal)) return true;
        if (id == "minecraft:snow_block") return true;
        return false;
    }

    // ---- 集計結果の型 ----

    // 1ブロックについての統計。
    public sealed class BlockStat
    {
        // 自分自身が取った向き(状態キー)と、その出現回数。
        // 例: "axis=y" -> 12, "(none)" -> 3
        public Dictionary<string, int> OwnStates = new(StringComparer.Ordinal);

        // 自状態キー -> 方向 -> (隣接ブロックの状態キー -> 出現回数)
        // 例: "facing=west" -> { "east" -> { "create:shaft|axis=x" -> 5 } }
        // 主役の向きごとに隣接を分けることで、向きの相関を保持する。
        public Dictionary<string, Dictionary<string, Dictionary<string, int>>> NeighborsByState =
            new(StringComparer.Ordinal);

        // このブロックが登場した Ponder シーン名の集合。
        public HashSet<string> AppearedIn = new(StringComparer.Ordinal);
    }

    // ---- 公開: 構造群を解析して統計を返す ----
    // structures: (シーン名, 構造) のリスト。
    public static Dictionary<string, BlockStat> Analyze(
        IEnumerable<(string SceneName, StructureNbtReader.Structure Structure)> structures)
    {
        var result = new Dictionary<string, BlockStat>(StringComparer.Ordinal);

        foreach (var (sceneName, structure) in structures)
        {
            // 座標 -> ブロック の索引を作る(隣接探索を速くするため)。
            var byPos = new Dictionary<(int, int, int), StructureNbtReader.Block>();
            foreach (var b in structure.Blocks)
                byPos[(b.X, b.Y, b.Z)] = b;

            foreach (var b in structure.Blocks)
            {
                // 主役が装飾/air なら統計を作らない。
                if (IsDecoration(b.Name)) continue;

                var stat = GetOrCreate(result, b.Name);
                stat.AppearedIn.Add(sceneName);

                // 自分の向き(状態キー)を数える。
                string ownState = StateKey(b);
                stat.OwnStates[ownState] = stat.OwnStates.GetValueOrDefault(ownState) + 1;

                // 主役の状態ごとに隣接を分けて集計する。
                // これにより「facing=west の水車の east には axis=x の shaft」という
                // 向きの相関が保持される。
                if (!stat.NeighborsByState.TryGetValue(ownState, out var stateMap))
                {
                    stateMap = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
                    stat.NeighborsByState[ownState] = stateMap;
                }

                // 6方向の隣を見る。
                foreach (var (dx, dy, dz, dirName) in Directions)
                {
                    var npos = (b.X + dx, b.Y + dy, b.Z + dz);
                    if (!byPos.TryGetValue(npos, out var nb)) continue;
                    if (IsDecoration(nb.Name)) continue; // 隣が装飾ならカウントしない

                    string nKey = FullKey(nb); // 例 "create:shaft|axis=x"

                    if (!stateMap.TryGetValue(dirName, out var dirMap))
                    {
                        dirMap = new Dictionary<string, int>(StringComparer.Ordinal);
                        stateMap[dirName] = dirMap;
                    }
                    dirMap[nKey] = dirMap.GetValueOrDefault(nKey) + 1;
                }
            }
        }

        return result;
    }

    // 隣接統計を、LLM に渡せる英語ルール行へ変換する。
    // allowedIds: 許可リストにあるブロックIDの集合。これに該当する主役のみ出力する。
    // perBlockDirs: 1ブロックあたり何方向まで出すか。topPerDir: 各方向の上位何件まで。
    // minCount: この回数以上観測された隣接だけ採用(ノイズ除去)。
    public static string ToRuleText(
        Dictionary<string, BlockStat> stats,
        IEnumerable<string> allowedIds,
        int perBlockDirs = 6,
        int topPerDir = 2,
        int minCount = 1)
    {
        var allow = new HashSet<string>(allowedIds, StringComparer.Ordinal);
        var sb = new System.Text.StringBuilder();

        // 出力順を安定させるため、登場シーン数の多い順→ID順で並べる。
        var ordered = stats
            .Where(kv => allow.Contains(kv.Key))
            .OrderByDescending(kv => kv.Value.AppearedIn.Count)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal);

        foreach (var (id, stat) in ordered)
        {
            // 主役の状態ごとに、観測回数の多い順で出力する。
            // 状態の重み = その状態で観測された隣接カウントの合計。
            var statesOrdered = stat.NeighborsByState
                .OrderByDescending(s => s.Value.Sum(d => d.Value.Values.Sum()))
                .ThenBy(s => s.Key, StringComparer.Ordinal);

            bool wroteHeader = false;

            foreach (var (ownState, stateMap) in statesOrdered)
            {
                var dirLines = new List<string>();
                int dirsUsed = 0;
                // 方向は up/down/east/west/south/north の順で安定化。
                foreach (var dirName in new[] { "up", "down", "east", "west", "south", "north" })
                {
                    if (dirsUsed >= perBlockDirs) break;
                    if (!stateMap.TryGetValue(dirName, out var dirMap)) continue;

                    var top = dirMap
                        .Where(kv => kv.Value >= minCount)
                        .OrderByDescending(kv => kv.Value)
                        .Take(topPerDir)
                        .Select(kv => $"{kv.Key} (x{kv.Value})")
                        .ToList();
                    if (top.Count == 0) continue;

                    dirLines.Add($"      {dirName}: {string.Join(", ", top)}");
                    dirsUsed++;
                }

                if (dirLines.Count == 0) continue;

                if (!wroteHeader)
                {
                    sb.Append("- ").Append(id).Append('\n');
                    wroteHeader = true;
                }

                // 状態見出し。"(none)" のときは状態無しブロックなので簡略表記。
                string stateLabel = ownState == "(none)" ? "(any orientation)" : ownState;
                sb.Append("  when ").Append(stateLabel).Append(":\n");
                foreach (var dl in dirLines)
                    sb.Append(dl).Append('\n');
            }
        }

        return sb.ToString();
    }

    private static BlockStat GetOrCreate(Dictionary<string, BlockStat> map, string id)
    {
        if (!map.TryGetValue(id, out var s))
        {
            s = new BlockStat();
            map[id] = s;
        }
        return s;
    }

    // 自分の状態キー。ノイズプロパティを除いた "axis=y" のような文字列。無ければ "(none)"。
    private static string StateKey(StructureNbtReader.Block b)
    {
        var parts = b.Properties
            .Where(kv => !IgnoredProps.Contains(kv.Key))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToList();
        return parts.Count == 0 ? "(none)" : string.Join(",", parts);
    }

    // 隣接相手用の完全キー。"create:cogwheel|axis=y" の形。
    private static string FullKey(StructureNbtReader.Block b)
    {
        string state = StateKey(b);
        return state == "(none)" ? b.Name : $"{b.Name}|{state}";
    }
}
