namespace ModSorter.Architect.Manual;

// 船体のパラメータUIの並び。HullParamsControl の partial。
// 並びは全船種で共通で、船種ごとに変わるのは HullPreset から受け取る初期値だけ。
// spec への詰め替えは HullParamsControl.cs、要約文は HullParamsControl.Summary.cs にある。
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
               ("横帆を張る", "set"), ("縦帆を張る", "fore"),
               ("畳む", "furled"), ("なし", "none"),
           }, p.Sail)
           .BeginChoiceGroup("sail", "set", "fore", "furled")
           .IntSlider("sail_w", "帆の幅", 2, 64, p.SailW,
               "横帆では帆桁の長さ。縦帆ではブームの長さ（マストから船尾側へ）")
           .IntSlider("sail_h", "帆の丈", 1, 64, p.SailH, "畳んだときは1列だけになる")
           .EndGroup()
           .Note("横帆はマスト1本につき1枚を帆桁から下へ吊る。帆桁は型幅より長くてよい。" +
                 "縦帆はマストの後ろへ張り、下辺をブーム・上辺をガフが持つ。ブームの後端は" +
                 "船尾材か後ろのマストの手前で止まり、下は人が通れるよう2マス空く。" +
                 "帆桁・ブームは寝た丸太なので axis を持ち、船首の向きに追従する。");

        _ui.Heading("砲門")
           .IntSlider("gun_rows", "砲門の段数", 0, 4, p.GunRows,
               "0でなし。フリゲートは1段、戦列艦は3段。上へ2マスおきに重ねる")
           .IntSlider("gun_step", "砲門の間隔", 0, 16, p.GunStep,
               "前後の中心間。0でなし。ヴィクトリーは下段 片舷15門で12.4ft＝3.8m間隔")
           .IntSlider("gun_base", "最下段の高さ", 0, 8, p.GunBase,
               "喫水線から上へのマス数。ヴィクトリーの下段は4ft9in＝1.4m、フリゲートは2.4m級")
           .Note("外板に1マスの口を開け、その1マス内側へ砲身を寝かせる。" +
                 "18ポンド砲の砲門は幅0.86m・高さ0.76mなので1マスに収まる。" +
                 "甲板のすぐ下には外板を1列残すので、深さが足りない段は開かない。" +
                 "舷が寄る船首・船尾も左右の口がぶつかるので開かない。");

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

        _ui.Heading("盾掛け・櫂・飾り")
           .IntSlider("shields", "盾の枚数（片舷）", 0, 32, p.Shields,
               "ゴクスタ船は片舷16・計32枚。舷縁の外へ1マス出るので幅が左右2マス増える")
           .Note("盾は直径94cm・厚さ2〜3cmの板。トラップドアを選ぶと開いた薄板として" +
                 "舷側の面へ張り付く。1枚おきに2枚目の素材を使うので黄と黒の交互になる。")
           .IntSlider("row_oars", "櫂の数（片舷）", 0, 32, p.RowOars,
               "ガレーは漕ぎ座 片舷24。舷縁の外へ3マス出るので幅が左右6マス増える")
           .Note("櫂は舷縁から1マスごとに1段下げて水面へ向け、水面の手前で止まる。" +
                 "素材はマストと同じものを使う。")
           .Choice("head", "船首材・船尾材の飾り", new[]
           {
               ("渦巻き", "spiral"), ("竜頭", "dragon"), ("なし", "none"),
           }, p.Head)
           .Note("渦巻きで高さ3、竜頭で高さ5。1マス未満の彫刻は表さない。");

        BuildBlockPanel(p);
    }
}
