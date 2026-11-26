namespace ModSorter.Architect.Manual;

// 船体のパラメータUIの並びのうちコックピット・風防・サンパッド。HullParamsControl の partial。
// HullParamsControl.Panel.Fit.cs が9,221バイトあり、足すと10,000バイトの上限を
// 超えるので分けた。spec への詰め替えは HullParamsControl.cs の1か所のまま。
// 呼び出し元は BuildPanel（BuildFitPanel の直後・BuildBlockPanel の直前）。
public sealed partial class HullParamsControl
{
    private void BuildCockpitPanel(HullPreset p)
    {
        _ui.Heading("コックピット・風防")
           .IntSlider("cockpit_len", "コックピットの前後長", 0, 60, p.CockpitLen,
               "全長に対する%。0でなし。リーヴァ・アクアラマは座席2列で45%")
           .IntSlider("cockpit_shift", "コックピットの位置", -20, 20, p.CockpitShift,
               "0で船体中央。+で船首側へずらす")
           .IntSlider("cockpit_seats", "座席の列数", 0, 4, p.CockpitSeats,
               "後ろから2マスおき（座面＋足元）。収まらない列は置かない")
           .Toggle("windscreen", "風防あり", "なし", p.Windscreen)
           .IntSlider("sun_pad", "サンパッドの前後長", 0, 16, p.SunPad,
               "コックピットの後ろの甲板へ敷くクッション（マス）。0でなし")
           .Note("甲板を張った船体の中央だけを1マス凹ませ、甲板の1段下へ床を張る。" +
                 "舷縁の1列は残す。座席は床の上の半ブロックで、座面は床から0.5m。" +
                 "最前列の前にも足元を1マス残し、その前の甲板の上に風防が立つ" +
                 "（最前列の両舷へ1マスずつ回り込む）。サンパッドはカーペット。" +
                 "デッキハウス・船楼・貨物艙口・開放艇のどれかがあるときは開かない" +
                 "（同じ甲板を使うため）。舷が寄る船首側の station も開かない。");
    }
}
