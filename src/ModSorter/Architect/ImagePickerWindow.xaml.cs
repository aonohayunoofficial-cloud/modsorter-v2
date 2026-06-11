using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ModSorter.Architect.Generation;

namespace ModSorter.Architect.Preview;

// プロンプトから画像を複数生成し、ユーザーに1枚選ばせるウィンドウ。
// 選択結果は SelectedImagePath(Windowsパス) に入り、DialogResult=true で返る。
// リテイクで同じプロンプト・別シードで作り直す。
public partial class ImagePickerWindow : Window
{
    // 一度に生成する枚数（拡張用に定数化）。
    private const int ImageCount = 3;

    // 画像を保存する WSL 側 assets フォルダ(UNC)。
    private const string AssetsUnc =
        @"\\wsl$\Ubuntu-22.04\home\yuno\projects\TRELLIS.2\assets";

    private readonly string _prompt;
    private readonly Random _rng = new();

    // 選択上限（案ボタンが3つのため当面3枚まで）。
    private const int MaxSelect = 3;

    // 現在選択中の画像の WSL 相対パス（クリック順を保持）。
    private readonly List<string> _selectedWsl = new();
    // Border と WSL パスの対応（選択ハイライト切り替え用）。
    private readonly Dictionary<System.Windows.Controls.Border, string> _borderToWsl = new();

    // 決定時に確定する、選ばれた画像の WSL 相対パス群（選択順）。
    public List<string> SelectedImageWslPaths { get; private set; } = new();

    public ImagePickerWindow(string prompt)
    {
        InitializeComponent();
        _prompt = prompt;
    }

    // 表示直後に最初の生成を走らせる。
    public async Task StartAsync()
    {
        await GenerateBatchAsync();
    }

    // 画像を ImageCount 枚生成し、パネルに並べる。
    private async Task GenerateBatchAsync()
    {
        RetakeBtn.IsEnabled = false;
        ImagesPanel.Children.Clear();
        // 選択状態をリセット（前回ぶんの選択・対応表を破棄）。
        _selectedWsl.Clear();
        _borderToWsl.Clear();
        UpdateSelectionInfo();
        StatusText.Text = "画像を生成中...（少しお待ちください）";

        for (int i = 0; i < ImageCount; i++)
        {
            StatusText.Text = $"画像を生成中... ({i + 1}/{ImageCount})";
            long seed = (long)(_rng.NextDouble() * 1_000_000_000_000_000L);

            var img = await ComfyUiClient.GenerateAsync(_prompt, seed);
            if (!img.Success || img.ImageBytes == null)
            {
                StatusText.Text = $"画像生成に失敗しました: {img.Error}";
                RetakeBtn.IsEnabled = true;
                return;
            }

            // assets に保存。履歴として残すため、上書きされない
            // タイムスタンプ付きの名前にする（例 arch_20260611_153012_842_0.png）。
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string pngName = $"arch_{stamp}_{i}.png";
            string pngUnc = Path.Combine(AssetsUnc, pngName);
            await File.WriteAllBytesAsync(pngUnc, img.ImageBytes);

            // サムネイル(クリックで選択)を作ってパネルへ。
            AddThumbnail(pngUnc, $"assets/{pngName}");
        }

        StatusText.Text = "気に入った画像をクリックしてください。なければ「リテイク」。";
        RetakeBtn.IsEnabled = true;
    }

    // 1枚ぶんのサムネイル(Border+Image)を作って ImagesPanel に追加する。
    private void AddThumbnail(string uncPath, string wslPath)
    {
        // UNC パス(\\wsl$\...)は new Uri で弾かれるため、
        // ファイルをストリームで読み込んで BitmapImage に流し込む。
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad; // 読み込み後ロックを残さない
        using (var fs = new FileStream(uncPath, FileMode.Open, FileAccess.Read))
        {
            bmp.StreamSource = fs;
            bmp.EndInit();   // using の中で EndInit して読み切る
        }
        bmp.Freeze(); // 別スレッドからも安全に使えるよう凍結

        var image = new Image
        {
            Source = bmp,
            Stretch = System.Windows.Media.Stretch.Uniform,
            Margin = new Thickness(6)
        };

        var border = new Border
        {
            BorderBrush = System.Windows.Media.Brushes.Gray,
            BorderThickness = new Thickness(2),
            Background = System.Windows.Media.Brushes.Black,
            Cursor = Cursors.Hand,
            Child = image
        };
        _borderToWsl[border] = wslPath;

        // クリックで選択をトグル（複数選択可、上限 MaxSelect）。
        border.MouseLeftButtonUp += (s, e) =>
        {
            if (s is not System.Windows.Controls.Border b) return;
            string wp = _borderToWsl[b];

            if (_selectedWsl.Contains(wp))
            {
                // 選択解除。
                _selectedWsl.Remove(wp);
                b.BorderBrush = System.Windows.Media.Brushes.Gray;
                b.BorderThickness = new Thickness(2);
            }
            else
            {
                if (_selectedWsl.Count >= MaxSelect)
                {
                    StatusText.Text = $"選択は最大 {MaxSelect} 枚までです。";
                    return;
                }
                // 選択。
                _selectedWsl.Add(wp);
                b.BorderBrush = System.Windows.Media.Brushes.LightGreen;
                b.BorderThickness = new Thickness(4);
            }
            UpdateSelectionInfo();
        };

        ImagesPanel.Children.Add(border);
    }

    // 選択枚数の表示と決定ボタンの有効/無効を更新する。
    private void UpdateSelectionInfo()
    {
        SelectionInfo.Text = $"選択: {_selectedWsl.Count} 枚";
        DecideBtn.IsEnabled = _selectedWsl.Count > 0;
    }

    // 決定。選択中の画像群を確定して閉じる。
    private void Decide_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWsl.Count == 0) return;
        SelectedImageWslPaths = new List<string>(_selectedWsl);
        DialogResult = true;
    }

    private async void Retake_Click(object sender, RoutedEventArgs e)
    {
        await GenerateBatchAsync();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
