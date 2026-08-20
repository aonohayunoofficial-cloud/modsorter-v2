using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSorter.Architect.Generation;

// 建物の躯体（様式ごとの壁・中間床）と開口部の適用。
// buildingStyle は ExpandCore で正規化済み（非矩形なら必ず "walled"）のものを受け取る。
public static partial class StructureExpander
{
    private static void BuildShell(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot,
        StructureSpec spec, int w, int d, int h,
        IReadOnlyList<string> allowedBlocks, string wall, string floor,
        bool rectangular, string buildingStyle)
    {
        if (buildingStyle == "colonnade")
        {
            // 開放型: 壁を立てず、外周の角＋等間隔の位置に円柱を立てる（神殿風）。
            BuildColonnade(cells, w, d, h, wall);
        }
        else if (buildingStyle == "temple")
        {
            // ファサード型: 指定面に柱廊、奥に壁の部屋。柱は範囲内に収める。
            string accentT = Pick(spec.AccentBlock, allowedBlocks, wall);
            BuildTemple(cells, w, d, h, wall, accentT, spec.FacadeFace ?? "south");
        }
        else
        {
            // アクセント材（柱型リズム用）。未指定なら wall と同じ＝従来の見た目。
            string accent = Pick(spec.AccentBlock, allowedBlocks, wall);
            // 柱なし(0)はそのまま尊重。柱ありの場合は最低4間隔を強制して密集を防ぐ。
            int pilasterStep = 0;
            if (spec.PilasterStep.HasValue && spec.PilasterStep.Value >= 2)
                pilasterStep = Math.Max(4, spec.PilasterStep.Value);

            // 壁（中間層 y=1..h-2 の外周リングのみ）。
            // マスクの縁(IsEdge)にだけ立てるので、L字・コの字でも内側角まで正しく回る。
            for (int y = 1; y <= h - 2; y++)
                foreach (var (x, z) in foot)
                {
                    if (!IsEdge(foot, x, z)) continue;

                    // 角判定・柱リズムは矩形のときだけ従来どおり適用する。
                    // 非矩形では角の定義が曖昧なので、縁は一律 wall（アクセントなし）にする。
                    bool useAccent = false;
                    if (rectangular)
                    {
                        bool isCorner = (x == 0 || x == w - 1) && (z == 0 || z == d - 1);
                        bool isPilaster = pilasterStep > 0 &&
                            ((x == 0 || x == w - 1) ? (z % pilasterStep == 0)
                                                    : (x % pilasterStep == 0));
                        useAccent = isCorner || isPilaster;
                    }
                    cells[(x, y, z)] = useAccent ? accent : wall;
                }
        }

        // 中間床（複数階）。指定された各 y にマスク内の全面の床を敷く。
        foreach (int fy in (spec.FloorLevels ?? new List<int>()).Distinct())
        {
            // 1階の床(0)・屋根の領域(h-1以上)とぶつかる指定は無視
            if (fy <= 0 || fy >= h - 1) continue;
            foreach (var (x, z) in foot)
                cells[(x, fy, z)] = floor;
        }
    }

    // 開口部の適用と入口の保証。必ず中間床より後に呼ぶ
    //（床に窓・ドアが指定されても壁セルのみ作用するので安全）。
    // colonnade（開放型）は壁がないので開口部は適用しない。
    // 注意: 現状の ApplyOpening は矩形外周（x=0/w-1, z=0/d-1）を前提とするため、
    //       非矩形フットプリントでは開口が壁セルに当たらず無視されることがある。
    //       非矩形向けの開口スナップは次フェーズで対応する。
    private static void ApplyOpeningsAndEntrance(
        Dictionary<(int x, int y, int z), string> cells,
        StructureSpec spec, int w, int d, int h,
        IReadOnlyList<string> allowedBlocks, string buildingStyle)
    {
        if (buildingStyle == "colonnade") return;

        var ops = spec.Openings ?? new List<Opening>();
        foreach (var op in ops)
            ApplyOpening(cells, op, w, d, h, allowedBlocks);

        // 入口の保証: door が1つも指定されていない場合、正面(facade_face、既定 south)の
        // 中央に自動でドアを1つ開ける。LLM がドアを出さなくても必ず入口ができる。
        // no_entrance=true のときは通さない。記念碑・オベリスク・台座のように
        // 穴を開けてはいけない塊で、勝手に壁が抜けるのを防ぐ。
        bool hasDoor = ops.Any(o =>
            string.Equals((o.Kind ?? "").Trim(), "door", StringComparison.OrdinalIgnoreCase));
        if (!hasDoor && !spec.NoEntrance)
        {
            string doorFace = (spec.FacadeFace ?? "south").Trim().ToLowerInvariant();
            if (doorFace != "north" && doorFace != "south" &&
                doorFace != "east" && doorFace != "west")
                doorFace = "south";
            // 面の中央を offset にする。south/north は x 方向、east/west は z 方向。
            int centerOffset = (doorFace == "south" || doorFace == "north")
                ? w / 2 : d / 2;
            ApplyOpening(cells,
                new Opening { Face = doorFace, Kind = "door", Offset = centerOffset, Level = 1 },
                w, d, h, allowedBlocks);
        }
    }
}
