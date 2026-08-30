using System;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 船体の要約文。UI の組み立てと分けて、部品が増えても本体が膨らまないようにする。
public sealed partial class HullParamsControl
{
    private string BuildSummary(StructureSpec spec, string face, int depth, int draft)
    {
        string transomNote = spec.HullTransom > 0
            ? $"トランサム{spec.HullTransom}%"
            : "ダブルエンダー";
        string frameNote = spec.HullFrameStep > 0
            ? $"フレーム{Math.Max(2, spec.HullFrameStep!.Value)}間隔"
            : "フレームなし";
        string bulwarkNote = spec.HullBulwark > 0 ? $"舷墻{spec.HullBulwark}" : "舷墻なし";
        string beamNote = spec.HullBeamStep > 0
            ? $"貫通横梁{Math.Max(2, spec.HullBeamStep!.Value)}間隔"
            : "貫通横梁なし";
        string mastNote = spec.HullMastCount > 0
            ? $"マスト{spec.HullMastCount}本・高さ{spec.HullMastHeight}"
            : "マストなし";
        string sailNote = spec.HullSail switch
        {
            "set" => $"横帆 {spec.HullSailWidth}×{spec.HullSailHeight}",
            "fore" => $"縦帆 ブーム{spec.HullSailWidth}×丈{spec.HullSailHeight}",
            "furled" => $"帆を畳む（幅{spec.HullSailWidth}）",
            _ => "帆なし",
        };
        string gunNote = spec.HullGunRows > 0 && spec.HullGunStep >= 2
            ? $"砲門{spec.HullGunRows}段・{spec.HullGunStep}間隔・喫水線上{spec.HullGunBase}"
            : "砲門なし";
        string oarNote = spec.HullOarPerSide > 0
            ? $"櫂 片舷{spec.HullOarPerSide}挺・計{spec.HullOarPerSide * 2}挺"
            : "櫂なし";
        string houseNote = spec.HullHouseDecks > 0
            ? $"デッキハウス{spec.HullHouseDecks}層・前後長{spec.HullHouseLength}%"
              + (spec.HullFunnel > 0 ? $"・煙突{spec.HullFunnel}" : "・煙突なし")
            : "デッキハウスなし";
        string holdNote = spec.HullHolds > 0
            ? $"貨物艙口{spec.HullHolds}" + (spec.HullDerrick == true ? "・デリックあり" : "")
            : "貨物艙口なし";
        string shieldNote = spec.HullShieldPerSide > 0
            ? $"盾 片舷{spec.HullShieldPerSide}・計{spec.HullShieldPerSide * 2}枚（2種を交互）"
            : "盾掛けなし";
        string rudderNote = (spec.HullSteeringOar == true, spec.HullSternRudder == true) switch
        {
            (true, true) => "側舵＋中心線舵",
            (true, false) => "側舵",
            (false, true) => "中心線舵",
            _ => "舵なし",
        };
        string castleNote = (spec.HullCastleAft > 0, spec.HullCastleFore > 0) switch
        {
            (true, true) =>
                $"船尾楼{spec.HullCastleAft}・船首楼{spec.HullCastleFore}（長さ{spec.HullCastleLength}%）",
            (true, false) => $"船尾楼{spec.HullCastleAft}（長さ{spec.HullCastleLength}%）",
            (false, true) => $"船首楼{spec.HullCastleFore}（長さ{spec.HullCastleLength}%）",
            _ => "船楼なし",
        };
        string headNote = spec.HullStemHead switch
        {
            "spiral" => "渦巻き飾り",
            "dragon" => "竜頭",
            _ => "飾りなし",
        };

        return $"{_p.Jp} 全長{spec.HullLength}×型幅{spec.HullBeam}×深さ{depth} / 喫水{draft} / " +
               $"断面{spec.HullSection} / 入角{spec.HullEntryAngle}度 / {transomNote} / " +
               $"船首材{spec.HullStemRake}度 / シア{spec.HullSheer}% / {frameNote} / " +
               $"竜骨{spec.HullKeelDepth} / {bulwarkNote} / {beamNote} / {mastNote} / {sailNote} / " +
               $"{gunNote} / {oarNote} / {houseNote} / {holdNote} / {shieldNote} / {rudderNote} / " +
               $"{castleNote} / {headNote} / " +
               $"船首{FaceJp(face)} / 外寸 {spec.Width}×{spec.Depth}×{spec.Height}";
    }

    private static string FaceJp(string v) => v switch
    {
        "north" => "北",
        "east" => "東",
        "west" => "西",
        _ => "南",
    };
}
