using ModSorter.Models;
using System.Windows;
using System.Windows.Threading;

namespace ModSorter;

// カバーアート帯のスライドアニメーション・ドラッグ・クリック。
// 状態(フィールド)と構築・配置は MainWindow.Cover.cs。
public partial class MainWindow : Window
{
    // 1枚ぶんスライド(forward=true:次へ, false:前へ)
    private void AnimateStep(bool forward)
    {
        if (_coverAnimating || _coverDragging) return;
        if (_coverMods.Count <= 3 || _coverCardWidth <= 0) return;

        _coverAnimating = true;
        double from = _coverOffset;
        double to = forward ? from - _coverCardWidth : from + _coverCardWidth;
        AnimateOffsetTo(from, to, () =>
        {
            // 論理indexを進退してオフセットを0へリセット
            _coverHead = Mod(_coverHead + (forward ? 1 : -1), _coverMods.Count);
            _coverOffset = 0;
            LayoutCoverSlots();
            _coverAnimating = false;
        });
    }

    // オフセットをアニメ。各フレームで LayoutCoverSlots を呼んでスロットを動かす
    private void AnimateOffsetTo(double from, double to, System.Action onDone)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double durMs = 300;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(15)
        };
        timer.Tick += (s, e) =>
        {
            double t = sw.ElapsedMilliseconds / durMs;
            if (t >= 1.0)
            {
                timer.Stop();
                _coverOffset = to;
                LayoutCoverSlots();
                onDone();
                return;
            }
            double eased = 1 - System.Math.Pow(1 - t, 3); // easeOutCubic
            _coverOffset = from + (to - from) * eased;
            LayoutCoverSlots();
        };
        timer.Start();
    }

    // ===== ドラッグ =====
    private void CoverCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_coverMods.Count <= 3 || _coverAnimating) return;
        _coverDragging = true;
        _coverDragMoved = false;
        _coverDragStartX = e.GetPosition(CoverCanvas).X;
        _coverDragBaseOffset = _coverOffset;
        _coverTimer?.Stop();
        CoverCanvas.CaptureMouse();
    }

    private void CoverCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_coverDragging) return;
        double cur = e.GetPosition(CoverCanvas).X;
        double delta = cur - _coverDragStartX;
        if (System.Math.Abs(delta) > 4) _coverDragMoved = true;

        _coverOffset = _coverDragBaseOffset + delta;
        // 1枚幅を超えたらその場で論理indexを巻き取り、offsetを範囲内に収める
        while (_coverOffset <= -_coverCardWidth)
        {
            _coverOffset += _coverCardWidth;
            _coverHead = Mod(_coverHead + 1, _coverMods.Count);
        }
        while (_coverOffset >= _coverCardWidth)
        {
            _coverOffset -= _coverCardWidth;
            _coverHead = Mod(_coverHead - 1, _coverMods.Count);
        }
        LayoutCoverSlots();
    }

    private void CoverCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_coverDragging) return;
        _coverDragging = false;
        CoverCanvas.ReleaseMouseCapture();
        SettleDrag();
    }

    private void CoverCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_coverDragging) return;
        _coverDragging = false;
        CoverCanvas.ReleaseMouseCapture();
        SettleDrag();
    }

    // 離した位置から最寄り境界(0 or ±cardW)へ収束
    private void SettleDrag()
    {
        if (_coverCardWidth <= 0 || _coverMods.Count == 0)
        {
            if (_coverMods.Count > 3) _coverTimer?.Start();
            return;
        }
        _coverAnimating = true;
        double from = _coverOffset;
        // offsetは -cardW..+cardW の範囲。半分超なら1枚送る方向へ、未満なら0へ
        double to;
        int headDelta;
        if (from <= -_coverCardWidth / 2.0) { to = -_coverCardWidth; headDelta = 1; }
        else if (from >= _coverCardWidth / 2.0) { to = _coverCardWidth; headDelta = -1; }
        else { to = 0; headDelta = 0; }

        AnimateOffsetTo(from, to, () =>
        {
            if (headDelta != 0)
                _coverHead = Mod(_coverHead + headDelta, _coverMods.Count);
            _coverOffset = 0;
            LayoutCoverSlots();
            _coverAnimating = false;
            if (_coverMods.Count > 3) _coverTimer?.Start();
        });
    }

    private void CoverCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_coverDragMoved) { _coverDragMoved = false; return; }
        if (sender is not System.Windows.Controls.Border b) return;
        if (b.Tag is not ModEntry mod) return;

        ShowDetail(mod);
        if (CardList.Items.Contains(mod))
        {
            CardList.ScrollIntoView(mod);
            Dispatcher.BeginInvoke(new Action(() => HighlightCard(mod)),
                DispatcherPriority.Loaded);
        }
    }
}
