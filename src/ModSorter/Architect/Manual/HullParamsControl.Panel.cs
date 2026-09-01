namespace ModSorter.Architect.Manual;

// 船体のパラメータUIの並び（船型まで）。HullParamsControl の partial。
// 並びは全船種で共通で、船種ごとに変わるのは HullPreset から受け取る初期値だけ。
// spec への詰め替えは HullParamsControl.cs、要約文は HullParamsControl.Summary.cs にある。
//
// 1ファイル9KBの目安を超えた（10,230バイト）ので、艤装以降の並びを
// HullParamsControl.Panel.Fit.cs へ分けた。こちらが持つのは
// 向き・主要目・横断面・水線の平面形・前後の立ち上がり・喫水線より上・構造/付帯まで。
// 分けたのは並びだけで、spec への詰め替えは HullParamsControl.cs の1か所のまま。
public sealed partial class HullParamsControl
{
    private void BuildPanel(HullPreset p)
    {
        var faces = new[] { ("南", "south"), ("北", "north"), ("東", "east"), ("西", "west") };

        _ui.Heading("向き")
           .Choice("bow", "船首の向き", faces, "south")
           .Note("船首が向く方角。東・西では幅と奥行きが入れ替わる。");

        _ui.Heading("主要目")
           .Note(p.Note)
           .IntSlider("len", "全長", 4, 140, p.Len, "LOA。商船は100m級なので上限を140に取る")
           .IntSlider("beam", "型幅", 2, 32, p.Beam, "水線での最大幅")
           .IntSlider("depth", "深さ", 2, 24, p.Depth, "基線から船体中央の甲板まで")
           .IntSlider("draft", "喫水", 1, 20, p.Draft, "設計喫水。深さを超える値は深さへ丸める")
           .Note("全長100マス級ではブロック数が1万を超え、生成に時間がかかる。");

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

        BuildFitPanel(p);
        BuildBlockPanel(p);
    }
}
