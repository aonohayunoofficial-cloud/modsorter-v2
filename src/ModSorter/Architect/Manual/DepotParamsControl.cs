using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 車庫（検車庫）。1マス=1m。
//
// 既定値の根拠（実寸）:
//   線路長   … 8両編成対応で留置線・検車ピット線とも180m。臨修線は台車1台分、
//              改造工事線は4両分。試運転線は700m。
//   ピット   … 検車エリア全体の床を線路より1段低くする。深さ1.2m級。
//   有効高さ … 電車線高さ標準5.00m＋懸吊装置500mm＋余裕200mm＝限界5.70m。
//              屋根上作業を見込むと庫内は8m級。
//   屋上点検 … 車両屋根上（およそ3.6m）の高さに点検ホームを回す。
//   線路間隔 … 車両基地は作業通路を取るため5m級（本線の4.0mより広い）。
public sealed class DepotParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    private const int MaxLen = 256;

    public DepotParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("向き")
           .Choice("face", "線路の向き", new[]
           {
               ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west"),
           }, "south");

        _ui.Heading("線路")
           .IntSlider("tracks", "線数", 1, 8, 2, "検車ピット線は2線が標準")
           .IntSlider("pitch", "線路間隔", 4, 12, 5, "車両基地は作業通路のため5m級")
           .IntSlider("cars", "収容両数", 1, 12, 8, "8両編成で線路長180m")
           .IntSlider("carlen", "1両の長さ(m)", 15, 26, 20)
           .IntSlider("spare", "前後の余裕(m)", 0, 40, 20, "8両180mに対し車体160m＋余裕");

        _ui.Heading("断面")
           .IntSlider("height", "庫内の有効高さ", 6, 20, 8, "架線限界5.70m。屋根上作業込みで8m級")
           .IntSlider("pit", "検車ピットの深さ", 0, 3, 1, "0でピットなし。実物は1.2m級")
           .Toggle("walk", "屋上点検ホームを付ける", "車両屋根上（およそ3.6m）に通路を回す", true)
           .Toggle("shutter", "扉を閉めた状態で描く", "オフなら開口のまま", false);

        _ui.Heading("屋根")
           .Choice("roof", "屋根の形", new[]
           {
               ("切妻", "gable"), ("アーチ", "arch"), ("片流れ", "shed"), ("陸屋根", "flat"),
           }, "gable")
           .IntSlider("pitch2", "屋根勾配", 1, 8, 4)
           .IntSlider("light", "照明の間隔", 0, 32, 12, "0で照明なし");

        _ui.Heading("事務所棟")
           .IntSlider("annex", "事務所棟の奥行き", 0, 16, 0, "0でなし。4以上で2階建（階高3）が付く")
           .Note("事務所棟を付けると庫の側壁に連絡口が空く。");

        _ui.Heading("使用ブロック")
           .BlockPick("pave", "土間・通路", "minecraft:smooth_stone")
           .BlockPick("wall", "壁", "minecraft:light_gray_concrete")
           .BlockPick("body", "柱・ピット躯体", "minecraft:stone_bricks")
           .BlockPick("roofblk", "屋根", "minecraft:gray_concrete")
           .BlockPick("glass", "採光帯・シャッター", "minecraft:glass")
           .BlockPick("ballast", "道床", "minecraft:gravel")
           .BlockPick("fence", "点検ホームの手すり", "minecraft:iron_bars")
           .BlockPick("trim", "照明", "minecraft:sea_lantern");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string pave = _ui.GetBlock("pave", "minecraft:smooth_stone");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(pave);

        int tracks = _ui.GetInt("tracks");
        int pitch = _ui.GetInt("pitch");
        int cars = _ui.GetInt("cars");
        int carLen = _ui.GetInt("carlen");
        int spare = _ui.GetInt("spare");
        int len = cars * carLen + spare;
        bool trimmed = len > MaxLen;
        if (trimmed) len = MaxLen;

        int height = _ui.GetInt("height");
        int pit = _ui.GetInt("pit");
        int annex = _ui.GetInt("annex");
        int light = _ui.GetInt("light");
        bool walk = _ui.GetBool("walk");
        bool shutter = _ui.GetBool("shutter");
        string roof = _ui.GetChoice("roof", "gable");

        var spec = new StructureSpec
        {
            StructureType = "railway:depot",
            FacadeFace = _ui.GetChoice("face", "south"),
            Width = 2 * 3 + (tracks - 1) * pitch + 1,
            Depth = len,
            Height = height,
            RailTracks = tracks,
            RailTrackPitch = pitch,
            RailPit = pit,
            RailRoofWalk = walk,
            RailShutter = shutter,
            RailAnnex = annex,
            RailCanopyRoof = roof,
            RailRoofPitch = _ui.GetInt("pitch2"),
            RailLightStep = light,
            FloorBlock = pave,
            WallBlock = _ui.GetBlock("wall", "minecraft:light_gray_concrete"),
            BaseBlock = _ui.GetBlock("body", "minecraft:stone_bricks"),
            RoofBlock = _ui.GetBlock("roofblk", "minecraft:gray_concrete"),
            VerandaBlock = _ui.GetBlock("glass", "minecraft:glass"),
            TowerBlock = _ui.GetBlock("ballast", "minecraft:gravel"),
            ParapetBlock = _ui.GetBlock("fence", "minecraft:iron_bars"),
            SeatBlock = _ui.GetBlock("trim", "minecraft:sea_lantern")
        };

        int span = 2 * 3 + (tracks - 1) * pitch;
        string roofNote = roof switch
        {
            "arch" => "アーチ",
            "shed" => "片流れ",
            "flat" => "陸屋根",
            _ => "切妻",
        };
        string pitNote = pit > 0 ? $"ピット深さ{pit}" : "ピットなし";
        string annexNote = annex >= 4 ? $"事務所棟{annex}（2階建）" : "事務所棟なし";
        string lenNote = trimmed ? $"→上限{MaxLen}に切り詰め" : "";

        summary = $"車庫 {tracks}線・間隔{pitch} / 全幅{span}×線路長{len}（{cars}両×{carLen}m＋余裕{spare}）{lenNote} / " +
                  $"有効高さ{height}（架線限界5.7m）/ {pitNote} / " +
                  $"{(walk ? "屋上点検ホームあり" : "屋上点検ホームなし")} / " +
                  $"{(shutter ? "扉閉" : "開口")} / {roofNote} / {annexNote}";
        return spec;
    }
}
