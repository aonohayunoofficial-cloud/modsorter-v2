using ModSorter.Clients;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ModSorter;

// 日本語化パックの表示まわり。
//   生成前: 残量と所要目安の文言(BuildUsageMessageAsync)
//   生成中: 進捗メーター。1秒ごとに経過・残り目安・応答待ち秒数を出し直す。
//           Azure の送信間隔の調整や応答待ちで件数が動かない間も、
//           秒数が進むことで処理が止まっていないと分かるようにする。
public partial class MainWindow : Window
{
    private DispatcherTimer? _lpTimer;
    private readonly Stopwatch _lpTotalWatch = new();   // メーター開始からの経過
    private readonly Stopwatch _lpCountWatch = new();   // 件数が出始めてからの経過(残り目安用)
    private DateTime _lpLastReport;                      // 直近に件数が進んだ時刻
    private int _lpDone, _lpTotal, _lpFirstDone;
    private bool _lpCounting;      // 件数が出ている(バーが確定表示)
    private bool _lpTranslating;   // 翻訳中(応答待ち秒数を出す)
    private string _lpWaitNote = "";

    // 生成全体の所要時間(完了表示用)。
    private string LangPackElapsedText => FormatSpan(_lpTotalWatch.Elapsed);

    // メーターを出して計時を始める。
    private void LangPackProgressStart(string phase)
    {
        if (_lpTimer == null)
        {
            _lpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _lpTimer.Tick += (_, __) => LangPackProgressTick();
        }
        _lpTotalWatch.Restart();
        _lpTimer.Start();
        LangPackProgress.Visibility = Visibility.Visible;
        LangPackProgressDetail.Visibility = Visibility.Visible;
        LangPackProgressPhase(phase);
    }

    // 件数の分からない段階(抽出・見積もり・パック生成)。バーは流れる表示にする。
    private void LangPackProgressPhase(string phase)
    {
        _lpCounting = false;
        _lpTranslating = false;
        LangPackProgress.IsIndeterminate = true;
        LangPackProgressPct.Text = "";
        LangPackStatus.Text = phase;
        LangPackProgressTick();
    }

    // 翻訳に入る。最初のバッチが返るまでは件数が出ないので流れる表示のまま、
    // 応答待ちの秒数だけを進める。note は応答待ちの後ろに添える補足。
    private void LangPackProgressTranslateBegin(string note)
    {
        _lpTranslating = true;
        _lpWaitNote = note;
        _lpLastReport = DateTime.Now;
        LangPackProgressTick();
    }

    // TranslateAsync の進捗通知から呼ぶ。done/total は件数。
    private void LangPackProgressReport(int done, int total, string message)
    {
        if (!_lpCounting)
        {
            // 最初の通知を起点に速さを測る。起点より前の待ち時間を含めると、
            // 残り目安が実際より長く出るため。
            _lpCounting = true;
            _lpFirstDone = done;
            _lpCountWatch.Restart();
            LangPackProgress.IsIndeterminate = false;
        }
        _lpDone = done;
        _lpTotal = total;
        _lpLastReport = DateTime.Now;
        double pct = total <= 0 ? 100 : done * 100.0 / total;
        LangPackProgress.Value = pct;
        LangPackProgressPct.Text = $"{pct:0}%";
        LangPackStatus.Text = message;
        LangPackProgressTick();
    }

    // 1秒ごと(と各通知時)に詳細行を出し直す。
    private void LangPackProgressTick()
    {
        var parts = new List<string> { "経過 " + FormatSpan(_lpTotalWatch.Elapsed) };

        if (_lpCounting && _lpDone < _lpTotal)
        {
            int span = _lpDone - _lpFirstDone;
            if (span > 0)
            {
                double sec = _lpCountWatch.Elapsed.TotalSeconds
                             * (_lpTotal - _lpDone) / span;
                parts.Add("残り約 " + FormatSpan(TimeSpan.FromSeconds(sec)));
            }
            else
            {
                parts.Add("残り 計算中");
            }
        }

        if (_lpTranslating)
        {
            int wait = (int)(DateTime.Now - _lpLastReport).TotalSeconds;
            if (wait >= 3)
                parts.Add($"応答待ち {wait} 秒" +
                    (_lpWaitNote.Length > 0 ? $"（{_lpWaitNote}）" : ""));
        }

        LangPackProgressDetail.Text = string.Join(" ・ ", parts);
    }

    // メーターを閉じる。何度呼んでもよい。状態表示(LangPackStatus)は呼び出し側が書く。
    private void LangPackProgressEnd()
    {
        _lpTimer?.Stop();
        _lpTotalWatch.Stop();
        _lpCountWatch.Stop();
        _lpCounting = false;
        _lpTranslating = false;
        LangPackProgress.IsIndeterminate = false;
        LangPackProgress.Visibility = Visibility.Collapsed;
        LangPackProgressPct.Text = "";
        LangPackProgressDetail.Visibility = Visibility.Collapsed;
        LangPackProgressDetail.Text = "";
    }

    private static string FormatSpan(TimeSpan t)
        => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    // 残量表示の文言と、残量超過の見込みかを返す。
    // Azure はキーで消費量を取れないので照合はせず、F0 の月間上限と所要目安を添える。
    private static async Task<(string Msg, bool Over)> BuildUsageMessageAsync(
        ITranslator tr, int send)
    {
        if (!tr.SupportsUsage)
        {
            // AzureTranslator は F0 の時間あたり上限に合わせ、毎分約 30,000 文字で送る。
            int minutes = (int)Math.Ceiling(send / 30000.0);
            return ($"{tr.DisplayName}: 残量はキーから取得できません" +
                    "(F0 は月 2,000,000 文字まで。Azure ポータルで確認)\n" +
                    $"送信ペース調整による所要目安: 約 {minutes} 分", false);
        }

        var usage = await tr.GetUsageAsync();
        if (!usage.HasValue)
            return ($"{tr.DisplayName} 残量取得に失敗(続行は可能): {tr.LastError}", false);

        long remaining = usage.Value.Limit - usage.Value.Count;
        var msg = $"{tr.DisplayName} 残り {remaining:N0} / 上限 {usage.Value.Limit:N0} 文字";
        if (send > remaining) return (msg, true);
        if (send > remaining * 0.8) msg += "(残量の80%超の見込み)";
        return (msg, false);
    }
}
