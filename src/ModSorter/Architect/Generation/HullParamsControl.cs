using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Manual;

// 船体（structure_type="hull:<船種>"）のパラメータUI。船種は kind 引数で切り替える。
// 座標生成は HullExpander が受け持つので、ここは hull_* を渡すだけ。
// 外寸は HullExpander.Extent（展開側と同じ Form を通る）から取るので、
// スライダーの表示値と生成物の外寸が食い違わない。
//
// 既定値の根拠（実寸）:
//   ロングシップ … ゴクスタ船は全長23.24m・型幅5.20m・深さ2.02m・喫水0.85m級。
//   外板はクリンカー張りの16列、肋骨の間隔は約0.96m（1マス=1mでは表現できないので
//   見える最小の2マスへ丸める）。竜骨は外板より下へ張り出す。船首材・船尾材が高く
//   立ち上がるダブルエンダー（トランサムなし）で、舷側の反り（シア）が強い。
//   櫂穴は片舷16。舷墻は持たず最上列の外板（シアストレーク）が舷縁になる。
//   マスト・帆・シールドラックは上部構造なのでフェーズ6で載せる。
public sealed class HullParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;
    private readonly string _kind;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public HullParamsControl(string kind)
    {
        _kind = (kind ?? "longship").Trim().ToLowerInvariant();
        _ui = new ParamPanelBuilder(this, Raise);

        var faces = new[] { ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west") };

        _ui.Heading("向き")
           .Choice("bow", "船首の向き", faces, "south")
           .Note("船首が向く方角。東・西では幅と奥行きが入れ替わる。");

        _ui.Heading("主要目")
           .Note("ゴクスタ船は全長23.24m・型幅5.20m・深さ2.02m・喫水0.85m。1マス=1m。")
           .IntSlider("len", "全長", 4, 64, 23, "LOA。ゴクスタ船は23.24m")
           .IntSlider("beam", "型幅", 2, 24, 5, "水線での最大幅。ゴクスタ船は5.20m")
           .IntSlider("depth", "深さ", 2, 16, 2, "基線から船体中央の甲板まで。ゴクスタ船は2.02m")
           .IntSlider("draft", "喫水", 1, 12, 1,
               "設計喫水。ゴクスタ船は0.85mで1マス相当。深さを超える値は深さへ丸める");

        _ui.Heading("横断面")
           .IntSlider("section", "断面のふくらみ", 0, 100, 50,
               "0で直線V、40付近で丸ビルジ、100でほぼ矩形。バイキング船は丸みのある浅い船底")
           .Note("超楕円の指数で作る。0がk=1、100がk=8。");

        _ui.Heading("水線の平面形")
           .IntSlider("entry", "入角", 5, 60, 15, "水線の半角（度）。実船で8〜20度、肥えた船で30度超")
           .IntSlider("bowfull", "船首の肥え", 0, 100, 30, "0で凹んだ鋭い水線、100で丸い船首")
           .IntSlider("run", "船尾の絞り長", 5, 60, 40, "全長に対する%。ダブルエンダーは長く取る")
           .IntSlider("sternfull", "船尾の肥え", 0, 100, 30, "船首と同じ意味")
           .IntSlider("transom", "トランサム幅", 0, 90, 0,
               "船尾の切り落としの幅（最大幅に対する%）。0でダブルエンダー");

        _ui.Heading("前後の立ち上がり")
           .IntSlider("rake", "船首材の傾斜", 0, 70, 45,
               "鉛直からの角度。現代の直立船首は0〜10度、バイキング船・快速帆船は45度超")
           .IntSlider("rise", "船尾の立ち上がり", 0, 20, 1,
               "船尾で船底が基線から上がる高さ。深さ-1を超える値は丸める");

        _ui.Heading("喫水線より上")
           .IntSlider("flare", "船首フレア角", 0, 45, 8, "舷側が外へ開く角。船体中央より前で効く")
           .IntSlider("tumble", "タンブルホーム角", 0, 30, 0, "舷側が内へ絞る角。中央から船尾で効く")
           .IntSlider("sheer", "シア倍率", 0, 400, 250,
               "100でICLL 1966の標準シア。反りの強いロングシップは200〜300。0で平らな甲板");

        _ui.Heading("構造・付帯")
           .IntSlider("frame", "フレーム間隔", 0, 32, 2,
               "肋骨の間隔。0でフレームなし。ゴクスタ船は約0.96mだが1は2へ丸める")
           .IntSlider("keel", "竜骨の張り出し", 0, 4, 1, "基線より下へ出す深さ。0で面一")
           .IntSlider("bulwark", "舷墻の高さ", 0, 6, 0,
               "0でなし。バイキング船は最上列の外板が舷縁を兼ねるので0が実物");

        _ui.Heading("マスト・帆")
           .IntSlider("masts", "マストの本数", 0, 3, 1, "ロングシップは1本。0でマストなし")
           .IntSlider("mast_h", "マストの高さ", 2, 64, 11,
               "甲板から上へのマス数。ゴクスタ船のマストは11〜13mと推定")
           .Choice("sail", "帆", new[]
           {
               ("張る", "set"), ("畳む", "furled"), ("なし", "none"),
           }, "set")
           .BeginChoiceGroup("sail", "set", "furled")
           .IntSlider("sail_w", "帆の幅", 2, 64, 11, "帆桁の長さを兼ねる。ゴクスタ船の帆は110m²級")
           .IntSlider("sail_h", "帆の丈", 1, 64, 10, "畳んだときは1列だけになる")
           .EndGroup()
           .Note("横帆1枚。帆桁は横に寝た丸太なので axis を持ち、船首の向きに追従する。");

        _ui.Heading("盾掛け・舵・飾り")
           .IntSlider("shields", "盾の枚数（片舷）", 0, 32, 16,
               "ゴクスタ船は片舷16・計32枚。舷縁の外へ1マス出るので幅が左右2マス増える")
           .Note("盾は直径94cm・厚さ2〜3cmの板。トラップドアを選ぶと開いた薄板として" +
                 "舷側の面へ張り付く。1枚おきに2枚目の素材を使うので黄と黒の交互になる。")
           .Toggle("rudder", "舵あり", "舵なし", true)
           .Note("舵は船尾の片舷に吊るクォーターラダー。舵柄が舷内へ伸びる。")
           .Choice("head", "船首材・船尾材の飾り", new[]
           {
               ("渦巻き", "spiral"), ("竜頭", "dragon"), ("なし", "none"),
           }, "spiral")
           .Note("渦巻きで高さ3、竜頭で高さ5。1マス未満の彫刻は表さない。");

        _ui.Heading("使用ブロック")
           .BlockPick("shell", "外板", "minecraft:oak_planks")
           .BlockPick("deck", "甲板・舷縁", "minecraft:spruce_planks")
           .BlockPick("keelb", "竜骨・船首材・船尾材", "minecraft:stripped_oak_log")
           .BlockPick("frameb", "フレーム・フロア材", "minecraft:oak_log")
           .BlockPick("railb", "舷墻・手すり", "minecraft:spruce_planks")
           .BlockPick("mastb", "マスト・帆桁", "minecraft:spruce_log")
           .BlockPick("sailb", "帆", "minecraft:white_wool")
           .BlockPick("shieldb", "盾（1枚目）", "minecraft:birch_trapdoor")
           .BlockPick("shieldb2", "盾（2枚目）", "minecraft:dark_oak_trapdoor")
           .BlockPick("fitb", "舵・舵柄・飾り", "minecraft:stripped_spruce_log")
           .Note("ゴクスタ船は船体がオーク、甲板が松。舷墻の高さが0のときは舷墻の" +
                 "ブロックを使わない。");

        Content = _ui.Root;
    }

    private string KindJp() => _kind switch
    {
        "longship" => "ロングシップ",
        _ => _kind,
    };

    private static string FaceJp(string v) => v switch
    {
        "north" => "北",
        "east" => "東",
        "west" => "西",
        _ => "南",
    };

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string shell = _ui.GetBlock("shell", "minecraft:oak_planks");
        allowed = _ui.BlockIds();
        if (allowed.Count == 0) allowed.Add(shell);

        string face = _ui.GetChoice("bow", "south");
        int depth = _ui.GetInt("depth");
        int draft = Math.Min(_ui.GetInt("draft"), depth);

        var spec = new StructureSpec
        {
            StructureType = "hull:" + _kind,
            FacadeFace = face,
            HullLength = _ui.GetInt("len"),
            HullBeam = _ui.GetInt("beam"),
            HullDepth = depth,
            HullDraft = draft,
            HullSection = _ui.GetInt("section"),
            HullEntryAngle = _ui.GetInt("entry"),
            HullBowFullness = _ui.GetInt("bowfull"),
            HullRunRatio = _ui.GetInt("run"),
            HullSternFullness = _ui.GetInt("sternfull"),
            HullTransom = _ui.GetInt("transom"),
            HullStemRake = _ui.GetInt("rake"),
            HullSternRise = _ui.GetInt("rise"),
            HullFlare = _ui.GetInt("flare"),
            HullTumblehome = _ui.GetInt("tumble"),
            HullSheer = _ui.GetInt("sheer"),
            HullFrameStep = _ui.GetInt("frame"),
            HullKeelDepth = _ui.GetInt("keel"),
            HullBulwark = _ui.GetInt("bulwark"),
            HullMastCount = _ui.GetInt("masts"),
            HullMastHeight = _ui.GetInt("mast_h"),
            HullSail = _ui.GetChoice("sail", "none"),
            HullSailWidth = _ui.GetInt("sail_w"),
            HullSailHeight = _ui.GetInt("sail_h"),
            HullShieldPerSide = _ui.GetInt("shields"),
            HullSteeringOar = _ui.GetBool("rudder"),
            HullStemHead = _ui.GetChoice("head", "none"),
            HullBlock = shell,
            DeckBlock = _ui.GetBlock("deck", "minecraft:spruce_planks"),
            BaseBlock = _ui.GetBlock("keelb", "minecraft:stripped_oak_log"),
            AccentBlock = _ui.GetBlock("frameb", "minecraft:oak_log"),
            ParapetBlock = _ui.GetBlock("railb", "minecraft:spruce_planks"),
            SuperstructureBlock = _ui.GetBlock("mastb", "minecraft:spruce_log"),
            RoofBlock = _ui.GetBlock("sailb", "minecraft:white_wool"),
            TowerBlock = _ui.GetBlock("shieldb", "minecraft:birch_trapdoor"),
            HullShieldBlockAlt = _ui.GetBlock("shieldb2", "minecraft:dark_oak_trapdoor"),
            SeatBlock = _ui.GetBlock("fitb", "minecraft:stripped_spruce_log")
        };

        // 外寸は展開側の Form と Top から取る。UI と生成側で式を二重に持たない。
        // Extent は canonical（船首 +z）なので、東西向きでは幅と奥行きを入れ替える。
        var ext = HullExpander.Extent(spec);
        bool swap = face is "east" or "west";
        spec.Width = swap ? ext.Depth : ext.Width;
        spec.Depth = swap ? ext.Width : ext.Depth;
        spec.Height = ext.Height;

        string transomNote = spec.HullTransom > 0
            ? $"トランサム{spec.HullTransom}%"
            : "ダブルエンダー";
        string frameNote = spec.HullFrameStep > 0
            ? $"フレーム{Math.Max(2, spec.HullFrameStep!.Value)}間隔"
            : "フレームなし";
        string bulwarkNote = spec.HullBulwark > 0 ? $"舷墻{spec.HullBulwark}" : "舷墻なし";
        string mastNote = spec.HullMastCount > 0
            ? $"マスト{spec.HullMastCount}本・高さ{spec.HullMastHeight}"
            : "マストなし";
        string sailNote = spec.HullSail switch
        {
            "set" => $"帆 {spec.HullSailWidth}×{spec.HullSailHeight}",
            "furled" => $"帆を畳む（幅{spec.HullSailWidth}）",
            _ => "帆なし",
        };
        string shieldNote = spec.HullShieldPerSide > 0
            ? $"盾 片舷{spec.HullShieldPerSide}・計{spec.HullShieldPerSide * 2}枚（2種を交互）"
            : "盾掛けなし";
        string headNote = spec.HullStemHead switch
        {
            "spiral" => "渦巻き飾り",
            "dragon" => "竜頭",
            _ => "飾りなし",
        };

        summary = $"{KindJp()} 全長{spec.HullLength}×型幅{spec.HullBeam}×深さ{depth} / 喫水{draft} / " +
                  $"断面{spec.HullSection} / 入角{spec.HullEntryAngle}度 / {transomNote} / " +
                  $"船首材{spec.HullStemRake}度 / シア{spec.HullSheer}% / {frameNote} / " +
                  $"竜骨{spec.HullKeelDepth} / {bulwarkNote} / {mastNote} / {sailNote} / " +
                  $"{shieldNote} / {(spec.HullSteeringOar == true ? "舵あり" : "舵なし")} / " +
                  $"{headNote} / 船首{FaceJp(face)} / " +
                  $"外寸 {spec.Width}×{spec.Depth}×{spec.Height}";
        return spec;
    }
}
