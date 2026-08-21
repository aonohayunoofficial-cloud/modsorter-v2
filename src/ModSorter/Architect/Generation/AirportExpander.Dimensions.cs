namespace ModSorter.Architect.Generation;

// ===== 実寸の出典（平面土木）=====
//   滑走路幅         … Code E で 45m（Code C 30m / Code F 60m）
//   滑走路ショルダー … Code D/E は舗装総幅 60m ＝ 片側 7.5m
//   進入端標識       … 進入端から 6m の位置から始まる縦縞。長さ 30m 以上、幅 1.8m、
//                      間隔 1.8m。本数は幅で決まる（18m:4 / 23m:6 / 30m:8 / 45m:12 / 60m:16）。
//                      横は舗装縁から 3m 以内、または中心線から 27m 以内の小さい方まで。
//   中心線標識       … 実線 30m ＋ 間隔 20m の破線（1周期 50m 以上）。幅は精密進入で 0.90m。
//   着陸目標点標識   … 進入端から 400m（滑走路長 2400m 以上のとき）、長さ 45〜60m。
//   接地帯標識       … 進入端から 150m ごとの対。帯の長さ 22.5m。
//   滑走路縁灯       … 間隔 60m 以下。
//   誘導路幅         … Code B:10.5m / C:15m（主脚外側間隔 6m 未満）・18m（6〜9m）/
//                      E:23m / F:25m。ショルダー込みの舗装総幅は C:25m / D:38m /
//                      E:44m / F:60m なので、Code E なら片側 10.5m。
//   誘導路中心線標識 … 黄の実線 1 本、幅 0.15m。縁標識は 2 本の連続線。
public static partial class AirportExpander
{
    // ===== 実寸（m）。マス数はすべてここから Scale で割って導く =====
    private const double ThresholdOffsetM = 6.0;     // 進入端から縦縞の始まりまで
    private const double ThresholdStripeLenM = 30.0; // 縦縞の長さ（最小30m）
    private const double StripeWidthM = 1.8;         // 縦縞の幅
    private const double EdgeClearM = 3.0;           // 舗装縁から縦縞までの空き
    private const double CenterOnM = 30.0;           // 中心線標識の実線長
    private const double CenterOffM = 20.0;          // 中心線標識の間隔
    private const double CenterWidthM = 0.90;        // 中心線標識の幅（精密進入）
    private const double AimPointM = 400.0;          // 着陸目標点標識の位置
    private const double AimLenM = 45.0;             // 着陸目標点標識の長さ
    private const double AimWidthM = 6.0;            // 着陸目標点標識の幅
    private const double TdzFirstM = 150.0;          // 接地帯標識の1組目
    private const double TdzStepM = 150.0;           // 接地帯標識の間隔
    private const double TdzLenM = 22.5;             // 接地帯標識の帯の長さ
    private const double TdzWidthM = 3.0;            // 接地帯標識の帯の幅
    private const double SideOffsetM = 18.0;         // 中心線から接地帯・目標点までの横距離
    private const double EdgeLightM = 60.0;          // 縁灯の間隔
    private const double TaxiCenterWidthM = 0.15;    // 誘導路中心線標識の幅
    private const double RunwayShoulderM = 7.5;      // 滑走路ショルダー（片側）
    private const double TaxiShoulderM = 10.5;       // 誘導路ショルダー（Code E・片側）
}
