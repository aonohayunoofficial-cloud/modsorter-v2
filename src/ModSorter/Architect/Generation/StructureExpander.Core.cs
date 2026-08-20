using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 単一の箱の生成順序。ここが「どの順で何を上書きするか」の唯一の定義。
// 各段は partial の別ファイルにあるヘルパーへ委譲する。段の入れ替えは不具合に直結するので
// 変更するときはコメントの理由（軒は塔より先、開口は中間床より後 など）を必ず確認する。
public static partial class StructureExpander
{
    // 単一の箱を確定的に座標へ展開する。
    // このメソッドは spec.Volumes を一切参照しない。ゆえに Part 内に volumes があっても
    // 展開されず、フェーズ2の再帰は1段で止まる。
    private static List<GeneratedBlock> ExpandCore(StructureSpec spec, IReadOnlyList<string> allowedBlocks)
    {
        // 寸法の健全化（最低 2x2x2、極端な値は抑える）
        int w = Clamp(spec.Width, 2, 64);
        int d = Clamp(spec.Depth, 2, 64);
        int h = Clamp(spec.Height, 2, 64);

        // 素材決定（許可リスト外なら先頭ブロックにフォールバック）
        string fallback = allowedBlocks.Count > 0 ? allowedBlocks[0] : "minecraft:oak_planks";

        // 全体形状モード。"building"（既定）以外は床/壁/屋根/開口部を一切通さず、
        // 専用ビルダーが座標を確定する。非 null が返れば早期リターンで通常ロジックを
        // 完全にバイパスする（振り分けの中身は StructureExpander.Dispatch.cs）。
        string structureType = (spec.StructureType ?? "building").Trim().ToLowerInvariant();
        var special = TryBuildSpecial(spec, structureType, w, d, h, allowedBlocks, fallback);
        if (special != null) return special;

        string wall = Pick(spec.WallBlock, allowedBlocks, fallback);
        string floor = Pick(spec.FloorBlock ?? spec.WallBlock, allowedBlocks, wall);
        string roof = Pick(spec.RoofBlock ?? spec.WallBlock, allowedBlocks, wall);
        // 採光面（鋸屋根・モニター屋根の垂直窓）。未指定ならガラス。
        string glazing = Pick(spec.GlazingBlock ?? "minecraft:glass", allowedBlocks, "minecraft:glass");

        // 平面形状（フットプリント）。矩形以外を許すためのマスク。
        // w×d 確定後に一度だけ集約して作る（プリセット→add→sub の順、順序非依存）。
        // 未指定なら全面 true＝従来の矩形と完全一致（後方互換）。
        HashSet<(int x, int z)> foot = BuildFootprint(spec, w, d);
        // マスクが矩形一杯（全 w*d セル）かどうか。非矩形なら屋根・様式を安全側へ寄せる。
        bool rectangular = IsRectangular(foot, w, d);

        // 座標 -> ブロックID。後勝ち（開口部で上書きするため）。
        var cells = new Dictionary<(int x, int y, int z), string>();

        // 床（y=0、マスク内のみ）
        foreach (var (x, z) in foot)
            cells[(x, 0, z)] = floor;

        // 土台段（base course）: y=0 のマスク縁一周を土台材に差し替える。
        // 未指定なら floor と同じ＝従来の見た目（差し替えても影響なし）。座標系は変えない。
        string baseBlock = Pick(spec.BaseBlock, allowedBlocks, floor);
        if (spec.HasBase)
        {
            foreach (var (x, z) in foot)
                if (IsEdge(foot, x, z))
                    cells[(x, 0, z)] = baseBlock;
        }

        // 屋根。形状の決定（非矩形での flat フォールバックを含む）と振り分けは
        // StructureExpander.Roof.Select.cs。以降の段は確定した roofType を参照する。
        string roofType = SelectRoofType(spec, rectangular);
        BuildRoofByType(cells, foot, spec, w, d, h, roof, wall, glazing, roofType);

        // パラペットと塔屋（どちらも平屋根専用・StructureExpander.Rooftop.cs）。
        // 塔屋はパラペットの環を切らないよう parapet を見て内側へ寄せるので、
        // 段の順序と parapet の受け渡しを変えないこと。
        int parapet = Clamp(spec.ParapetHeight ?? 0, 0, 4);
        BuildParapet(cells, foot, spec, h, allowedBlocks, wall, roofType, parapet);
        BuildPenthouse(cells, foot, spec, w, d, h, allowedBlocks, wall, roof, roofType, parapet);

        // 煙突。屋根生成の後に呼ぶ（各列の屋根の実際の最高yを見て、そこから上へ積むため）。
        // 本数0なら何もしない。素材は chimney_block → roof → wall の順で流用。
        if (spec.ChimneyCount > 0)
        {
            string chimney = Pick(spec.ChimneyBlock, allowedBlocks, roof);
            BuildChimney(cells, spec, w, d, h, chimney);
        }

        // 建物様式。colonnade/temple は矩形前提（柱の等間隔配置・柱廊）なので、
        // 非矩形フットプリントのときは walled（壁のリング）へフォールバックする。
        string buildingStyle = (spec.BuildingStyle ?? "walled").Trim().ToLowerInvariant();
        if (!rectangular)
            buildingStyle = "walled";

        // 壁（様式ごと）と中間床。中間床は壁の後（StructureExpander.Shell.cs）。
        BuildShell(cells, foot, spec, w, d, h, allowedBlocks, wall, floor, rectangular, buildingStyle);

        // 開口部の適用（中間床より後。床に窓・ドアが指定されても壁セルのみ作用するので安全）
        // colonnade（開放型）は壁がないので開口部は適用しない。
        ApplyOpeningsAndEntrance(cells, spec, w, d, h, allowedBlocks, buildingStyle);

        // 軒・縁側・塔（StructureExpander.Trim.cs）。塔は必ず軒の後に立てる。
        BuildExteriorTrim(cells, foot, spec, w, d, h, allowedBlocks, wall, roof, roofType, rectangular);

        return Normalize(cells);
    }
}
