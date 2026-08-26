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
// スライダーの並びは全船種で共通。初期値と実物の説明文は HullPresets.cs にある。
// 要約文の組み立ては HullParamsControl.Summary.cs にある。
public sealed partial class HullParamsControl : UserControl, IManualParamControl
{
    private readonly ParamPanelBuilder _ui;
    private readonly string _kind;
    private readonly HullPreset _p;

    public event EventHandler? ParamsChanged;
    private void Raise() => ParamsChanged?.Invoke(this, EventArgs.Empty);

    public HullParamsControl(string kind)
    {
        _kind = (kind ?? "longship").Trim().ToLowerInvariant();
        _p = HullPresets.Of(_kind);
        var p = _p;
        _ui = new ParamPanelBuilder(this, Raise);

        var faces = new[] { ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west") };

        _ui.Heading("向き")
           .Choice("bow", "船首の向き", faces, "south")
           .Note("船首が向く方角。東・西では幅と奥行きが入れ替わる。");

        _ui.Heading("主要目")
           .Note(p.Note)
           .IntSlider("len", "全長", 4, 64, p.Len, "LOA")
           .IntSlider("beam", "型幅", 2, 24, p.Beam, "水線での最大幅")
           .IntSlider("depth", "深さ", 2, 16, p.Depth, "基線から船体中央の甲板まで")
           .IntSlider("draft", "喫水", 1, 12, p.Draft, "設計喫水。深さを超える値は深さへ丸める");

        _ui.Heading("横断面")
           .IntSlider("section", "断面のふくらみ", 0, 100, p.Section,
               "0で直線V、40付近で丸ビルジ、100でほぼ矩形。コグ船の平底は90前後")
           .Note("超楕円の指数で作る。0がk=1、100がk=8。");

        _ui.Heading("水線の平面形")
           .IntSlider("entry", "入角", 5, 60, p.Entry,
               "水線の半角（度）。実船で8〜20度、肥えた船で30度超")
           .IntSlider("bowfull", "船首の肥え", 0, 100, p.BowFull, "0で凹んだ鋭い水線、100で丸い船首")
           .IntSlider("run", "船尾の絞り長", 5, 60, p.Run, "全長に対する%。ダブルエンダーは長く取る")
           .IntSlider("sternfull", "船尾の肥え", 0, 100, p.SternFull, "船首と同じ意味")
           .IntSlider("transom", "トランサム幅", 0, 90, p.Transom,
               "船尾の切り落としの幅（最大幅に対する%）。0でダブルエンダー");

        _ui.Heading("前後の立ち上がり")
           .IntSlider("rake", "船首材の傾斜", 0, 70, p.Rake,
               "鉛直からの角度。現代の直立船首は0〜10度、バイキング船・コグ船は45度超")
           .IntSlider("rise", "船尾の立ち上がり", 0, 20, p.Rise,
               "船尾で船底が基線から上がる高さ。深さ-1を超える値は丸める");

        _ui.Heading("喫水線より上")
           .IntSlider("flare", "船首フレア角", 0, 45, p.Flare, "舷側が外へ開く角。船体中央より前で効く")
           .IntSlider("tumble", "タンブルホーム角", 0, 30, p.Tumble, "舷側が内へ絞る角。中央から船尾で効く")
           .IntSlider("sheer", "シア倍率", 0, 400, p.Sheer,
               "100でICLL 1966の標準シア。反りの強いロングシップは250。0で平らな甲板");

        _ui.Heading("構造・付帯")
           .IntSlider("frame", "フレーム間隔", 0, 32, p.Frame, "肋骨の間隔。0でなし。1は2へ丸める")
           .IntSlider("keel", "竜骨の張り出し", 0, 4, p.Keel, "基線より下へ出す深さ。0で面一")
           .IntSlider("bulwark", "舷墻の高さ", 0, 6, p.Bulwark,
               "0でなし。バイキング船は最上列の外板が舷縁を兼ねるので0が実物")
           .IntSlider("beam_step", "貫通横梁の間隔", 0, 32, p.BeamStep,
               "0でなし。コグ船は横梁の木口が外板を貫いて外へ出るので幅が左右2マス増える");

        _ui.Heading("マスト・帆")
           .IntSlider("masts", "マストの本数", 0, 3, p.Masts, "ロングシップ・コグ船は1本。0でなし")
           .IntSlider("mast_h", "マストの高さ", 2, 64, p.MastH,
               "甲板から上へのマス数。帆は帆桁から下へ吊るので、低いと帆が舷墻へ食い込む")
           .Choice("sail", "帆", new[]
           {
               ("張る", "set"), ("畳む", "furled"), ("なし", "none"),
           }, p.Sail)
           .BeginChoiceGroup("sail", "set", "furled")
           .IntSlider("sail_w", "帆の幅", 2, 64, p.SailW, "帆桁の長さを兼ねる")
           .IntSlider("sail_h", "帆の丈", 1, 64, p.SailH, "畳んだときは1列だけになる")
           .EndGroup()
           .Note("横帆1枚。帆桁は横に寝た丸太なので axis を持ち、船首の向きに追従する。");

        _ui.Heading("舵")
           .Toggle("rudder", "クォーターラダーあり", "なし", p.Oar)
           .Note("船尾の片舷に吊る舵。舵柄が舷内へ伸びる。バイキング船の方式。")
           .Toggle("stern_rudder", "中心線舵あり", "なし", p.SternRudder)
           .Note("船尾材に付く舵。1200年頃に側舵から置き換わった。" +
                 "船尾材より後ろへ1マス出るので奥行きが1増える。");

        _ui.Heading("船楼")
           .IntSlider("castle_aft", "船尾楼の高さ", 0, 16, p.CastleAft,
               "0でなし。コグ船は船尾に高い楼を載せる")
           .IntSlider("castle_fore", "船首楼の高さ", 0, 16, p.CastleFore,
               "0でなし。初期のコグ船は持たない")
           .IntSlider("castle_len", "船楼の前後長", 5, 40, p.CastleLen,
               "全長に対する%。船尾楼・船首楼で共通");

        _ui.Heading("盾掛け・飾り")
           .IntSlider("shields", "盾の枚数（片舷）", 0, 32, p.Shields,
               "ゴクスタ船は片舷16・計32枚。舷縁の外へ1マス出るので幅が左右2マス増える")
           .Note("盾は直径94cm・厚さ2〜3cmの板。トラップドアを選ぶと開いた薄板として" +
                 "舷側の面へ張り付く。1枚おきに2枚目の素材を使うので黄と黒の交互になる。")
           .Choice("head", "船首材・船尾材の飾り", new[]
           {
               ("渦巻き", "spiral"), ("竜頭", "dragon"), ("なし", "none"),
           }, p.Head)
           .Note("渦巻きで高さ3、竜頭で高さ5。1マス未満の彫刻は表さない。");

        _ui.Heading("使用ブロック")
           .BlockPick("shell", "外板", p.Shell)
           .BlockPick("deck", "甲板・舷縁", p.Deck)
           .BlockPick("keelb", "竜骨・船首材・船尾材", p.Keelb)
           .BlockPick("frameb", "フレーム・フロア材", p.Frameb)
           .BlockPick("railb", "舷墻・手すり", p.Railb)
           .BlockPick("mastb", "マスト・帆桁", p.Mastb)
           .BlockPick("sailb", "帆", p.Sailb)
           .BlockPick("shieldb", "盾（1枚目）", p.Shieldb)
           .BlockPick("shieldb2", "盾（2枚目）", p.Shieldb2)
           .BlockPick("fitb", "舵・舵柄・貫通横梁・飾り", p.Fitb)
           .BlockPick("castleb", "船楼", p.Castleb)
           .Note("ゴクスタ船は船体がオーク、甲板が松。コグ船はオーク一色。" +
                 "高さや枚数が0の部品はブロックを使わない。");

        Content = _ui.Root;
    }

    public StructureSpec BuildSpec(out List<string> allowed, out string summary)
    {
        string shell = _ui.GetBlock("shell", _p.Shell);
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
            HullBeamStep = _ui.GetInt("beam_step"),
            HullMastCount = _ui.GetInt("masts"),
            HullMastHeight = _ui.GetInt("mast_h"),
            HullSail = _ui.GetChoice("sail", "none"),
            HullSailWidth = _ui.GetInt("sail_w"),
            HullSailHeight = _ui.GetInt("sail_h"),
            HullShieldPerSide = _ui.GetInt("shields"),
            HullSteeringOar = _ui.GetBool("rudder"),
            HullSternRudder = _ui.GetBool("stern_rudder"),
            HullCastleAft = _ui.GetInt("castle_aft"),
            HullCastleFore = _ui.GetInt("castle_fore"),
            HullCastleLength = _ui.GetInt("castle_len"),
            HullStemHead = _ui.GetChoice("head", "none"),
            HullBlock = shell,
            DeckBlock = _ui.GetBlock("deck", _p.Deck),
            BaseBlock = _ui.GetBlock("keelb", _p.Keelb),
            AccentBlock = _ui.GetBlock("frameb", _p.Frameb),
            ParapetBlock = _ui.GetBlock("railb", _p.Railb),
            SuperstructureBlock = _ui.GetBlock("mastb", _p.Mastb),
            RoofBlock = _ui.GetBlock("sailb", _p.Sailb),
            TowerBlock = _ui.GetBlock("shieldb", _p.Shieldb),
            HullShieldBlockAlt = _ui.GetBlock("shieldb2", _p.Shieldb2),
            SeatBlock = _ui.GetBlock("fitb", _p.Fitb),
            HullCastleBlock = _ui.GetBlock("castleb", _p.Castleb)
        };

        // 外寸は展開側の Form と Top から取る。UI と生成側で式を二重に持たない。
        // Extent は canonical（船首 +z）なので、東西向きでは幅と奥行きを入れ替える。
        var ext = HullExpander.Extent(spec);
        bool swap = face is "east" or "west";
        spec.Width = swap ? ext.Depth : ext.Width;
        spec.Depth = swap ? ext.Width : ext.Depth;
        spec.Height = ext.Height;

        summary = BuildSummary(spec, face, depth, draft);
        return spec;
    }
}
