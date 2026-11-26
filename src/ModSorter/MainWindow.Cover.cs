using ModSorter.Models;
using System.Windows;

namespace ModSorter;

// カバーアート帯(軽量カルーセル / 最近5日間更新)の構築・配置・画像読み込み。
// スライド・ドラッグ・クリックは MainWindow.Cover.Slide.cs。
public partial class MainWindow : Window
{
    // ===== カバーアート（軽量カルーセル / 最近5日間更新） =====
    private readonly System.Collections.Generic.List<ModEntry> _coverMods = new();
    private System.Windows.Threading.DispatcherTimer? _coverTimer;
    private double _coverCardWidth = 0;       // 1枚幅(窓幅/3)
    private bool _coverAnimating = false;

    // 物理スロット(0..SLOTS-1)に置くカードと、各スロットが指す論理index
    private const int SLOTS = 5;              // 予備1 + 見える3 + 予備1
    private readonly System.Windows.Controls.Border[] _coverSlots =
        new System.Windows.Controls.Border[SLOTS];
    private int _coverHead = 0;               // スロット0が指す _coverMods のindex
    private double _coverOffset = 0;          // スライドオフセット(px, 左へ負)

    // ドラッグ
    private bool _coverDragging = false;
    private bool _coverDragMoved = false;
    private double _coverDragStartX = 0;
    private double _coverDragBaseOffset = 0;

    private readonly System.Collections.Generic.Dictionary<string,
        System.Windows.Media.Imaging.BitmapImage> _coverBmpCache = new();

    // 5日以内に更新があったMODを集めてカバーを構築
    private void RefreshCoverArts()
    {
        _coverMods.Clear();
        var since = System.DateTime.Now.AddDays(-5);
        foreach (var m in _mods
                     .Where(m => m.FileModified >= since)
                     .OrderByDescending(m => m.FileModified))
        {
            _coverMods.Add(m);
        }

        _coverHead = 0;
        _coverOffset = 0;
        BuildCoverSlots();

        _coverTimer?.Stop();
        if (_coverMods.Count > 3)
        {
            if (_coverTimer == null)
            {
                _coverTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = System.TimeSpan.FromSeconds(3)
                };
                _coverTimer.Tick += (s, e) => AnimateStep(forward: true);
            }
            _coverTimer.Start();
        }
    }

    // スロット5枚を生成して Canvas に配置
    private void BuildCoverSlots()
    {
        if (CoverCanvas == null) return;
        CoverCanvas.Children.Clear();

        if (_coverCardWidth <= 0 && CoverCanvas.ActualWidth > 0)
            _coverCardWidth = CoverCanvas.ActualWidth / 3.0;

        if (_coverMods.Count == 0) return;

        for (int slot = 0; slot < SLOTS; slot++)
        {
            var border = new System.Windows.Controls.Border
            {
                Background = (System.Windows.Media.Brush)FindResource("StoneDark"),
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            border.MouseLeftButtonUp += CoverCard_Click;
            border.Child = new System.Windows.Controls.Image
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };
            _coverSlots[slot] = border;
            CoverCanvas.Children.Add(border);
        }
        LayoutCoverSlots();
    }

    // 各スロットの位置・サイズ・画像を現在の _coverHead と _coverOffset で更新
    private void LayoutCoverSlots()
    {
        if (_coverMods.Count == 0 || _coverCardWidth <= 0) return;
        double h = CoverCanvas.ActualHeight;

        for (int slot = 0; slot < SLOTS; slot++)
        {
            var b = _coverSlots[slot];
            if (b == null) continue;

            // スロット0を左予備(-1枚)とする: 実表示は slot=1,2,3 が窓内
            double left = (slot - 1) * _coverCardWidth + _coverOffset + 3;
            System.Windows.Controls.Canvas.SetLeft(b, left);
            System.Windows.Controls.Canvas.SetTop(b, 3);
            b.Width = _coverCardWidth - 6;
            b.Height = h - 6;

            int idx = Mod(_coverHead + (slot - 1), _coverMods.Count);
            var mod = _coverMods[idx];
            b.Tag = mod;
            if (b.Child is System.Windows.Controls.Image img)
                img.Source = LoadCoverBitmap(mod.IconSource);
        }
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    private void CoverCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (CoverCanvas == null || CoverCanvas.ActualWidth <= 0) return;
        double newWidth = CoverCanvas.ActualWidth / 3.0;
        if (System.Math.Abs(newWidth - _coverCardWidth) < 0.5 && _coverSlots[0] != null)
        {
            LayoutCoverSlots();
            return;
        }
        _coverCardWidth = newWidth;
        if (_coverSlots[0] == null) BuildCoverSlots();
        else LayoutCoverSlots();
    }

    private System.Windows.Media.Imaging.BitmapImage? LoadCoverBitmap(string src)
    {
        if (string.IsNullOrEmpty(src)) return null;
        if (_coverBmpCache.TryGetValue(src, out var cached)) return cached;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            if (System.IO.File.Exists(src))
            {
                var bytes = System.IO.File.ReadAllBytes(src);
                using var ms = new System.IO.MemoryStream(bytes);
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            else
            {
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new System.Uri(src, System.UriKind.RelativeOrAbsolute);
                bmp.EndInit();
            }
            bmp.Freeze();
            _coverBmpCache[src] = bmp;
            return bmp;
        }
        catch { return null; }
    }
}
