using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// StructureSpec を確定的に座標へ展開する。
// 壁の外周リングは必ずここで生成するため、塊化や壁抜けは原理的に起きない。
//
// このファイルには公開エントリ(Expand)だけを置く。生成順序(ExpandCore)は
// StructureExpander.Core.cs にある。
// 「どの順で何を上書きするか」が過去の不具合（越屋根の隙間・塔と開口の衝突・
// 軒の高さ）の主因だったため、順序は Core.cs の1メソッドに集約して見通しを保つ。
// 個々の部品は partial の別ファイルに分けてある。
//   StructureExpander.Core.cs            生成順序（ExpandCore）
//   StructureExpander.Dispatch.cs        特殊形状ビルダーへの振り分け
//   StructureExpander.Roof.Select.cs     屋根形状の決定と振り分け
//   StructureExpander.Rooftop.cs         パラペット・塔屋
//   StructureExpander.Shell.cs           壁・様式・中間床・開口部
//   StructureExpander.Trim.cs            軒・縁側・塔・座標正規化
//   StructureExpander.Roof.Basic.cs      平屋根・切妻・階段切妻・片流れ
//   StructureExpander.Roof.Industrial.cs 鋸屋根・越屋根
//   StructureExpander.Roof.Cap.cs        ドーム・四角錐・尖塔
//   StructureExpander.Openings.cs        開口部の適用とスナップ
//   StructureExpander.Parts.cs           軒・縁側・煙突・塔・柱・柱廊・神殿
//   StructureExpander.Footprint.cs       平面マスクと共通小物(Clamp/Pick)
//   StructureExpander.Civil.cs           スロープ・橋（座標を直接返す別系統）
public static partial class StructureExpander
{
    // 公開エントリ。volumes が指定されていれば各 Part を個別展開してオフセット合成する。
    // 空なら従来どおり単一の箱として ExpandCore に委譲する（後方互換）。
    public static List<GeneratedBlock> Expand(StructureSpec spec, IReadOnlyList<string> allowedBlocks)
    {
        // ===== 複数ボリューム合成（フェーズ2）=====
        if (spec.Volumes != null && spec.Volumes.Count > 0)
        {
            var merged = new Dictionary<(int x, int y, int z), string>();
            foreach (var vol in spec.Volumes)
            {
                if (vol?.Part == null) continue;

                // Part は単一の箱として展開する。Part 内にさらに volumes があっても
                // ExpandCore は volumes を参照しないので、再帰は1段で止まる（無限再帰防止）。
                var partBlocks = ExpandCore(vol.Part, allowedBlocks);

                // オフセットは絶対配置。負値は 0 にクランプ（宙抜け・負座標を防ぐ）。
                int ox = Math.Max(0, vol.OffsetX);
                int oy = Math.Max(0, vol.OffsetY);
                int oz = Math.Max(0, vol.OffsetZ);

                // 重なりは後勝ち（リストで後ろの Part が上書きする）。
                foreach (var b in partBlocks)
                    merged[(b.X + ox, b.Y + oy, b.Z + oz)] = b.Id;
            }

            return merged
                .OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.z).ThenBy(kv => kv.Key.x)
                .Select(kv => new GeneratedBlock
                {
                    X = kv.Key.x,
                    Y = kv.Key.y,
                    Z = kv.Key.z,
                    Id = kv.Value
                })
                .ToList();
        }

        return ExpandCore(spec, allowedBlocks);
    }
}
