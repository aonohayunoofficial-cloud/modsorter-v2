using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// 屋根形状の決定と振り分け。実際の生成は Roof.Basic / Roof.Industrial / Roof.Cap にある。
public static partial class StructureExpander
{
    // roof_type を正規化し、非矩形フットプリントで破綻する形状を flat へ寄せる。
    // 非矩形では棟・軒が矩形前提の屋根（gable / gable_stairs / shed / sawtooth / monitor）が
    // 崩れるので flat へ寄せる。頂冠形（dome / pyramid / spire）はマスクに沿って絞れるため、
    // 円形平面のときだけ許す。
    // 円形と認めるのは footprint_shape="circle" かつ add/sub 無指定のときに限る。
    // 欠けた円は輪郭が読めず、ドームが平面の外へ張り出して宙に浮くおそれがある。
    private static string SelectRoofType(StructureSpec spec, bool rectangular)
    {
        string footShape = (spec.FootprintShape ?? "rect").Trim().ToLowerInvariant();
        bool roundPlan = footShape == "circle"
            && (spec.FootprintAdd == null || spec.FootprintAdd.Count == 0)
            && (spec.FootprintSub == null || spec.FootprintSub.Count == 0);

        string roofType = (spec.RoofType ?? "flat").Trim().ToLowerInvariant();
        bool capRoof = roofType == "dome" || roofType == "pyramid" || roofType == "spire";
        if (!rectangular && !(roundPlan && capRoof))
            roofType = "flat";
        return roofType;
    }

    // 確定した roofType で各ビルダーへ振り分ける。分岐の順序は元の if 連鎖のまま。
    private static void BuildRoofByType(
        Dictionary<(int x, int y, int z), string> cells,
        HashSet<(int x, int z)> foot,
        StructureSpec spec, int w, int d, int h,
        string roof, string wall, string glazing, string roofType)
    {
        if (roofType == "gable")
            BuildGableRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "gable_stairs")
            BuildGableStairsRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "shed")
            BuildShedRoof(cells, spec, w, d, h, roof, wall);
        else if (roofType == "sawtooth")
            BuildSawtoothRoof(cells, spec, w, d, h, roof, wall, glazing);
        else if (roofType == "monitor")
            BuildMonitorRoof(cells, spec, w, d, h, roof, wall, glazing);
        else if (roofType == "dome")
            BuildDomeRoof(cells, foot, spec, w, d, h, roof);
        else if (roofType == "pyramid")
            BuildPyramidRoof(cells, foot, h, roof);
        else if (roofType == "spire")
            BuildSpireRoof(cells, foot, spec, h, roof);
        else
            BuildFlatRoof(cells, foot, h, roof);
    }
}
