using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// プラットフォーム。1マス=1m で組むので、縮尺1の空港建物系と寸法が合う。
//
// 既定値の根拠（実寸）:
//   ホーム高さ   … レール面上 1100mm（電車専用）/ 920mm（共用）/ 1250mm（新幹線）。
//                  1マス=1m なのでどれも 1 マス。
//   ホーム縁端   … 軌道中心から 1475mm（JR 在来線）。中心から 2 マス目＝1.5m 相当。
//   ホーム幅     … 島式 3.0m 以上・相対式 2.0m 以上が下限。通勤駅の実際は 5〜10m 級。
//   点状ブロック … 縁端警告は縁端から 80cm 以上離す。1 マス内側で約 1m。
//   ホーム長     … 20m 車 × 両数。10 両で 200m 強。
//   軌道中心間隔 … 在来線 4.0m、新幹線 4.3m。
//   ホームドア   … 20m 車 4 扉＝およそ 5m 間隔。腰高タイプは 1.3m 級。
//   高架橋       … ラーメン高架橋の柱スパンは 8〜10m 級。
public sealed class PlatformParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    // ホーム長の上限。これを超えるぶんは切り詰める。
    private const int MaxLen = 256;

    public PlatformParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "線路の向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("形式")
           .Choice("type", "ホームの形式", new[]
           {
               ("島式 1面2線", "island"),
               ("相対式 2面2線", "opposed"),
               ("単式 1面1線", "side"),
           }, "island")
           .Note("縁端は軌道中心から2マス目（実物1475mm＝1.5m相当）。線路との間に必ず1マス空く。");

        _ui.Heading("長さ")
           .IntSlider("cars", "停車両数", 1, 16, 10)
           .IntSlider("carlen", "1両の長さ(m)", 15, 26, 20, "在来線20m・地下鉄18m・新幹線25m")
           .IntSlider("spare", "前後の余裕(m)", 0, 20, 4, "停止位置のずれを吸収する伸び")
           .Note("ホーム長＝両数×車長＋余裕。256マスを超えるぶんは切り詰める。");

        _ui.Heading("断面")
           .IntSlider("width", "ホーム幅", 2, 24, 8, "下限は島式3.0m・相対式2.0m。通勤駅は5〜10m級")
           .IntSlider("height", "ホーム高さ", 1, 4, 1, "レール面上1100mm（電車専用）でおよそ1マス")
           .IntSlider("pitch", "軌道中心間隔（相対式の2線）", 4, 12, 4, "在来線4.0m・新幹線4.3m")
           .IntSlider("margin", "ホーム端から先の道床", 0, 32, 8)
           .Note("軌道は道床だけで表す。レールは隣接から形が決まる機能ブロックで斜めに描かれるため置かない。");

        _ui.Heading("ホーム上")
           .Toggle("tactile", "点状ブロックを敷く", "縁端から1マス内側（実物は80cm以上）", true)
           .Toggle("ramp", "ホーム端を勾配で落とす", "実物のホーム端は斜路で下がる", true)
           .IntSlider("door", "ホームドアの高さ", 0, 3, 0, "0でなし。腰高タイプは1.3m級。5mごとに開口");

        _ui.Heading("高架")
           .IntSlider("viaduct", "路盤面の高さ", 0, 32, 0, "0で地上。3以上で床版と橋脚が入る")
           .IntSlider("pier", "橋脚の間隔", 4, 24, 10, "ラーメン高架橋の柱スパンは8〜10m級");

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "ホーム天端", "minecraft:light_gray_concrete")
           .BlockPick("edge", "縁端の白線", "minecraft:white_concrete")
           .BlockPick("body", "躯体・橋脚", "minecraft:stone_bricks")
           .BlockPick("tactile", "点状ブロック", "minecraft:yellow_terracotta")
           .BlockPick("ballast", "道床", "minecraft:gravel")
           .BlockPick("fence", "柵・ホームドア", "minecraft:iron_bars")
           .BlockPick("girder", "高架の床版", "minecraft:gray_concrete");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string pave = _ui.GetBlock("pave", "minecraft:light_gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(pave);

        string type = _ui.GetChoice("type", "island");
        int cars = _ui.GetInt("cars");
        int carLen = _ui.GetInt("carlen");
        int spare = _ui.GetInt("spare");
        int len = cars * carLen + spare;
        bool trimmed = len > MaxLen;
        if (trimmed) len = MaxLen;

        int width = _ui.GetInt("width");
        int height = _ui.GetInt("height");
        int pitch = _ui.GetInt("pitch");
        int margin = _ui.GetInt("margin");
        int door = _ui.GetInt("door");
        int pier = _ui.GetInt("pier");
        bool tactile = _ui.GetBool("tactile");
        bool ramp = _ui.GetBool("ramp");

        int viaduct = _ui.GetInt("viaduct");
        if (viaduct > 0 && viaduct < 3) viaduct = 3;   // 床版と柱が入る最小の高さ

        var spec = new StructureSpec
        {
            StructureType = "railway:platform",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = width,
            Depth = len,
            Height = height,
            RailPlatformType = type,
            RailTrackPitch = pitch,
            RailTrackMargin = margin,
            RailPlatformDoor = door,
            RailTactile = tactile,
            RailEndRamp = ramp,
            RailViaduct = viaduct,
            RailPierStep = pier,
            FloorBlock = pave,
            AccentBlock = _ui.GetBlock("edge", "minecraft:white_concrete"),
            BaseBlock = _ui.GetBlock("body", "minecraft:stone_bricks"),
            WallBlock = _ui.GetBlock("tactile", "minecraft:yellow_terracotta"),
            TowerBlock = _ui.GetBlock("ballast", "minecraft:gravel"),
            ParapetBlock = _ui.GetBlock("fence", "minecraft:iron_bars"),
            RoofBlock = _ui.GetBlock("girder", "minecraft:gray_concrete")
        };

        string typeNote = type switch
        {
            "opposed" => "相対式2面2線",
            "side" => "単式1面1線",
            _ => "島式1面2線",
        };
        int span = type switch
        {
            "opposed" => width * 2 + pitch + 3,
            "side" => width + 3,
            _ => width + 6,
        };
        string lenNote = trimmed ? $"→上限{MaxLen}に切り詰め" : "";
        string doorNote = door > 0 ? $"ホームドア高さ{door}" : "ホームドアなし";
        string viaNote = viaduct > 0 ? $"高架{viaduct}（橋脚{pier}間隔）" : "地上";

        summary = $"プラットフォーム {typeNote} / ホーム長{len}（{cars}両×{carLen}m＋余裕{spare}）{lenNote} / " +
                  $"幅{width}×天端高さ{height} / 全幅{span} / " +
                  $"{(tactile ? "点状ブロックあり" : "点状ブロックなし")} / {doorNote} / {viaNote}";
        return spec;
    }
}
