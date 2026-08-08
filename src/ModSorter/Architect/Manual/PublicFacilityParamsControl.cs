using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 公共施設。体育館・病棟・消防署・庁舎を1つの中分類にまとめ、種類の選択で
// パラメータ群を切り替える。座標生成は PublicFacilityExpander が受け持つので、
// ここは structure_type="civic:<種類>" と寸法・素材を渡すだけ。
//
// 既定値の根拠（実寸）:
//   体育館 … バスケットボール競技コート 28×15m、安全域を含む最小アリーナ 32×19m、
//             公式大会用 34×22m。バレーボールの有効天井高 12.5m。梁間スパン 6.4m。
//             ステージは間口 10〜12m・奥行 5〜6m・高さ 0.9m。
//   病棟   … 一般病床は 1 床あたり 6.4m² 以上、中廊下 2.7m 以上（医療法施行規則）。
//             既定は中廊下 3m・病室 6×7m＝42m²（4 床室で 10.5m²/床）、階高 4m。
//   消防署 … はしご車 全長 12m・全高 3.5m。車庫は奥行 12m・有効高 4.5m、1 台の間口 5m、
//             シャッター 4×4m。ホース乾燥塔は 16m・平面 4×4m。
//   庁舎   … 執務室は職員 1 人あたり 4.5〜6m²、柱スパン 6.4m、階高 3.9〜4.0m。
//             1〜2 階が窓口のある基壇、上層はセットバックした執務室棟。
public sealed class PublicFacilityParamsControl : UserControl, IManualParamControl
{
    private const int FloorHeight = 4;   // 病棟・消防署・庁舎の階高（展開側と同じ値）

    private readonly ParamPanelBuilder _ui;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public PublicFacilityParamsControl()
    {
        _ui = new ParamPanelBuilder(this, Raise);

        _ui.Heading("種類")
           .Choice("kind", "施設", new[]
           {
               ("体育館 (gym)", "gym"),
               ("病棟 (hospital)", "hospital"),
               ("消防署 (fire)", "fire"),
               ("庁舎 (hall)", "hall"),
           }, "gym")
           .Choice("front", "正面", new[]
           {
               ("南面", "south"), ("北面", "north"), ("東面", "east"), ("西面", "west"),
           }, "south");

        // ===== 体育館 =====
        _ui.BeginChoiceGroup("kind", "gym")
           .Heading("規模")
           .Note("競技コート28×15mに安全域を足した34×22mが公式大会用の目安。")
           .IntSlider("gW", "幅", 20, 63, 34)
           .IntSlider("gD", "奥行", 16, 63, 22)
           .IntSlider("gH", "軒高", 8, 24, 13, "屋根はこの上に載る。バレーボールは12.5m必要")
           .IntSlider("gSpan", "柱の間隔", 3, 12, 6, "6で6.4mスパン相当")
           .Heading("屋根")
           .Choice("gRoof", "屋根の形", new[]
           {
               ("円弧 (vault)", "vault"),
               ("切妻 (gable)", "gable"),
               ("陸屋根 (flat)", "flat"),
           }, "vault")
           .Choice("gRidge", "棟の向き", new[] { ("X軸", "x"), ("Z軸", "z") }, "x")
           .BeginChoiceGroup("gRoof", "vault")
           .IntSlider("gRise", "円弧のライズ", 2, 20, 5)
           .EndGroup()
           .BeginChoiceGroup("gRoof", "gable")
           .IntSlider("gPitch", "勾配", 1, 4, 2, "1=急(45°) / 4=緩やか")
           .EndGroup()
           .BeginChoiceGroup("gRoof", "flat")
           .IntSlider("gParapet", "パラペット", 0, 4, 1)
           .EndGroup()
           .Heading("ギャラリー")
           .Note("2階の回廊（ランニングコース）。幅3マス、内側に手すりが付く。")
           .Toggle("gGallery", "ギャラリーあり", "ギャラリーなし", true)
           .BeginGroup("gGallery")
           .IntSlider("gGalleryY", "床の高さ", 4, 12, 5)
           .EndGroup()
           .EndGroup();

        // ===== 病棟 =====
        _ui.BeginChoiceGroup("kind", "hospital")
           .Heading("規模")
           .Note("中廊下3m・病室6×7m＝42m²（4床室で10.5m²/床）・階高4mで組む。")
           .IntSlider("hW", "幅", 20, 63, 37, "6マスごとに病室が並ぶ")
           .IntSlider("hD", "奥行", 17, 63, 17, "廊下3＋南北の病室7＋7")
           .IntSlider("hFloors", "階数", 1, 12, 4)
           .IntSlider("hParapet", "パラペット", 0, 4, 1)
           .EndGroup();

        // ===== 消防署 =====
        _ui.BeginChoiceGroup("kind", "fire")
           .Heading("規模")
           .Note("車庫は奥行12m・有効高4.5m。1台あたり間口5mでシャッターを割り付ける。")
           .IntSlider("fW", "幅", 12, 63, 20, "5マスごとに1台ぶんの間口")
           .IntSlider("fD", "奥行", 18, 63, 24, "うち12マスが車庫")
           .IntSlider("fFloors", "事務棟の階数", 2, 6, 2)
           .IntSlider("fParapet", "パラペット", 0, 4, 1)
           .Heading("ホース乾燥塔")
           .Toggle("fTower", "乾燥塔あり", "乾燥塔なし", true)
           .BeginGroup("fTower")
           .IntSlider("fTowerW", "一辺", 3, 8, 4)
           .IntSlider("fTowerH", "高さ", 8, 32, 16, "4〜5階相当の16mが標準")
           .Choice("fTowerRoof", "頂部", new[] { ("陸屋根", "flat"), ("四角錐", "spire") }, "flat")
           .EndGroup()
           .EndGroup();

        // ===== 庁舎 =====
        _ui.BeginChoiceGroup("kind", "hall")
           .Heading("規模")
           .Note("下2階が窓口のある基壇、上層は3マスセットバックした執務室棟。階高4m。")
           .IntSlider("cW", "幅", 20, 63, 38)
           .IntSlider("cD", "奥行", 16, 63, 26)
           .IntSlider("cFloors", "階数", 2, 14, 5)
           .IntSlider("cSpan", "柱の間隔", 3, 12, 6, "6で6.4mスパン相当")
           .IntSlider("cParapet", "パラペット", 0, 4, 1)
           .Heading("塔屋")
           .Toggle("cPh", "塔屋あり", "塔屋なし", true)
           .BeginGroup("cPh")
           .IntSlider("cPhW", "幅", 3, 20, 8)
           .IntSlider("cPhD", "奥行", 3, 20, 6)
           .IntSlider("cPhH", "高さ", 2, 8, 3)
           .EndGroup()
           .EndGroup();

        _ui.Heading("使用ブロック")
           .BlockPick("wall", "壁", "minecraft:smooth_stone")
           .BlockPick("accent", "柱・見切り", "minecraft:polished_andesite")
           .BlockPick("floor", "床", "minecraft:smooth_stone")
           .BlockPick("roofBlock", "屋根", "minecraft:light_gray_concrete")
           .BlockPick("glass", "窓", "minecraft:glass")
           .BlockPick("line", "コートライン", "minecraft:yellow_terracotta")
           .BlockPick("rail", "手すり・カウンター", "minecraft:oak_planks")
           .BlockPick("parapetBlock", "パラペット", "minecraft:smooth_stone")
           .BlockPick("towerBlock", "乾燥塔", "minecraft:light_gray_concrete")
           .BlockPick("phBlock", "塔屋", "minecraft:light_gray_concrete");

        Content = _ui.Root;
    }

    // 階高ごとの中間床。展開側は FloorLevels.Count + 1 を階数として読む。
    private static List<int> Levels(int floors)
    {
        var levels = new List<int>();
        for (int f = 1; f < floors; f++) levels.Add(f * FloorHeight);
        return levels;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string wall = _ui.GetBlock("wall", "minecraft:smooth_stone");
        string accent = _ui.GetBlock("accent", "minecraft:polished_andesite");
        string floor = _ui.GetBlock("floor", "minecraft:smooth_stone");
        string roof = _ui.GetBlock("roofBlock", "minecraft:light_gray_concrete");
        string glass = _ui.GetBlock("glass", "minecraft:glass");
        string line = _ui.GetBlock("line", "minecraft:yellow_terracotta");
        string rail = _ui.GetBlock("rail", "minecraft:oak_planks");
        string parapet = _ui.GetBlock("parapetBlock", "minecraft:smooth_stone");
        string tower = _ui.GetBlock("towerBlock", "minecraft:light_gray_concrete");
        string ph = _ui.GetBlock("phBlock", "minecraft:light_gray_concrete");

        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(wall);

        string kind = _ui.GetChoice("kind", "gym");

        var spec = new StructureSpec
        {
            StructureType = "civic:" + kind,
            FacadeFace = _ui.GetChoice("front", "south"),
            WallBlock = wall,
            AccentBlock = accent,
            FloorBlock = floor,
            RoofBlock = roof,
            GlazingBlock = glass,
            BaseBlock = line,
            SeatBlock = rail,
            ParapetBlock = parapet,
            TowerBlock = tower,
            PenthouseBlock = ph
        };

        switch (kind)
        {
            case "hospital": summary = FillHospital(spec); break;
            case "fire": summary = FillFire(spec); break;
            case "hall": summary = FillHall(spec); break;
            default: summary = FillGym(spec); break;
        }
        return spec;
    }

    private string FillGym(StructureSpec spec)
    {
        int w = _ui.GetInt("gW");
        int d = _ui.GetInt("gD");
        int h = _ui.GetInt("gH");
        string shape = _ui.GetChoice("gRoof", "vault");

        spec.Width = w;
        spec.Depth = d;
        spec.Height = h;
        spec.PilasterStep = _ui.GetInt("gSpan");
        spec.RoofType = shape;
        spec.RidgeAxis = _ui.GetChoice("gRidge", "x");
        spec.RoofPitch = shape == "gable" ? _ui.GetInt("gPitch") : 2;
        spec.DomeHeight = shape == "vault" ? _ui.GetInt("gRise") : 0;
        spec.ParapetHeight = shape == "flat" ? _ui.GetInt("gParapet") : 0;

        // 展開側は FloorLevels[0] をギャラリー床の高さとして読む。
        // 0 を渡すと有効範囲（4以上）から外れるのでギャラリーは作られない。
        spec.FloorLevels = new List<int>
        {
            _ui.GetBool("gGallery") ? _ui.GetInt("gGalleryY") : 0
        };

        string roofText = shape switch
        {
            "gable" => $"切妻(勾配{spec.RoofPitch})",
            "flat" => $"陸屋根(パラペット{spec.ParapetHeight})",
            _ => $"円弧(ライズ{spec.DomeHeight})"
        };
        string gallery = _ui.GetBool("gGallery") ? $"ギャラリーy={_ui.GetInt("gGalleryY")}" : "ギャラリーなし";
        return $"体育館 {w}×{d} / 軒高{h} / {roofText} / スパン{spec.PilasterStep} / {gallery}";
    }

    private string FillHospital(StructureSpec spec)
    {
        int w = _ui.GetInt("hW");
        int d = _ui.GetInt("hD");
        int floors = _ui.GetInt("hFloors");

        spec.Width = w;
        spec.Depth = d;
        spec.Height = floors * FloorHeight + 1;
        spec.FloorLevels = Levels(floors);
        spec.RoofType = "flat";
        spec.ParapetHeight = _ui.GetInt("hParapet");

        int rooms = Math.Max(1, (w - 1) / 6) * 2;   // 南北2列
        return $"病棟 {w}×{d} / {floors}階(階高{FloorHeight}) / 病室{rooms}室 / 中廊下3 / パラペット{spec.ParapetHeight}";
    }

    private string FillFire(StructureSpec spec)
    {
        int w = _ui.GetInt("fW");
        int d = _ui.GetInt("fD");
        int floors = _ui.GetInt("fFloors");
        bool towerOn = _ui.GetBool("fTower");

        spec.Width = w;
        spec.Depth = d;
        spec.Height = floors * FloorHeight + 1;
        spec.FloorLevels = Levels(floors);
        spec.RoofType = "flat";
        spec.ParapetHeight = _ui.GetInt("fParapet");
        spec.TowerWidth = towerOn ? _ui.GetInt("fTowerW") : 0;
        spec.TowerHeight = towerOn ? _ui.GetInt("fTowerH") : 0;
        spec.TowerRoof = _ui.GetChoice("fTowerRoof", "flat");

        int bays = Math.Clamp((w - 1) / 5, 1, 8);
        string towerText = towerOn
            ? $"乾燥塔{spec.TowerWidth}角×{spec.TowerHeight}"
            : "乾燥塔なし";
        return $"消防署 {w}×{d} / 車庫奥行12・{bays}台(シャッター4×4) / 事務棟{floors}階 / {towerText}";
    }

    private string FillHall(StructureSpec spec)
    {
        int w = _ui.GetInt("cW");
        int d = _ui.GetInt("cD");
        int floors = _ui.GetInt("cFloors");
        bool phOn = _ui.GetBool("cPh");

        spec.Width = w;
        spec.Depth = d;
        spec.Height = floors * FloorHeight + 1;
        spec.FloorLevels = Levels(floors);
        spec.RoofType = "flat";
        spec.PilasterStep = _ui.GetInt("cSpan");
        spec.ParapetHeight = _ui.GetInt("cParapet");
        spec.PenthouseWidth = phOn ? _ui.GetInt("cPhW") : 0;
        spec.PenthouseDepth = phOn ? _ui.GetInt("cPhD") : 0;
        spec.PenthouseHeight = phOn ? _ui.GetInt("cPhH") : 0;

        int podium = Math.Min(2, floors - 1);
        string phText = phOn
            ? $"塔屋{spec.PenthouseWidth}×{spec.PenthouseDepth}×{spec.PenthouseHeight}"
            : "塔屋なし";
        return $"庁舎 {w}×{d} / {floors}階(基壇{podium}階＋執務棟) / セットバック3 / スパン{spec.PilasterStep} / {phText}";
    }
}
