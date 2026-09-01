namespace ModSorter.Architect.Manual;

// 船体のパラメータUIの並びのうち艤装以降。HullParamsControl の partial。
// マスト・帆・砲門・デッキハウス/煙突・貨物艙・舵・船楼・盾掛け/櫂/飾りを持つ。
//
// HullParamsControl.Panel.cs が1ファイル10,000バイトの上限を超えたので分けた。
// 分けたのは並びだけで、spec への詰め替えは HullParamsControl.cs の1か所のまま。
// 呼び出し元は BuildPanel の末尾（BuildBlockPanel の直前）。
public sealed partial class HullParamsControl
{
    private void BuildFitPanel(HullPreset p)
    {
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

        _ui.Heading("デッキハウス・煙突")
           .IntSlider("house_decks", "層数", 0, 8, p.HouseDecks,
               "0でなし。リバティ船の船橋楼は3層。1層は床＋内法2マスの3マス")
           .IntSlider("house_len", "前後長", 5, 60, p.HouseLen,
               "全長に対する%。リバティ船は15%級")
           .IntSlider("house_shift", "前後の位置", -20, 20, p.HouseShift,
               "0で船体中央。+で船首側へずらす")
           .IntSlider("funnel", "煙突の高さ", 0, 16, p.Funnel,
               "屋根から上へのマス数。0でなし。3マス角で中が煙路")
           .Note("箱は両舷に1マスの通路（サイドデッキ）を残す幅で載る。" +
                 "上の層は前後を1マスずつ詰めるので、下の層の屋根が甲板になる。" +
                 "各層の後面に幅1・高さ2の戸口が開き、脇に外舷梯子が付く。" +
                 "層数が0のときは煙突も立たない（煙突の高さは屋根を基準に取る）。");

        _ui.Heading("貨物艙")
           .IntSlider("holds", "艙口の数", 0, 8, p.Holds,
               "0でなし。リバティ船は5。船体を等分した位置へ置く")
           .Toggle("derrick", "荷役デリックあり", "なし", p.Derrick)
           .Note("艙口は甲板を抜いて周りに1マスのコーミング（縁の立ち上がり）を立てる。" +
                 "幅は型幅の1/3級で、両舷に2マス以上の通路が残る幅へ抑える。" +
                 "デッキハウスと重なる位置の艙口は置かない。" +
                 "デリックは艙口の前後に高さ8の柱を立て、腕木を艙口の上へ倒す。");

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
    }
}
