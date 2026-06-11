using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ModSorter.Architect.Preview;

// assets フォルダ内の生成画像(arch_*.png)を一覧し、複数選択・削除・3D化できる窓。
// 3D化は「決定して閉じる」形にし、選ばれた画像の WSL 相対パスを呼び出し側へ返す。
public partial class GalleryWindow : Window
{
    // 画像が置かれている WSL 側 assets フォルダ(UNC)。
    private const string AssetsUnc =
        @"\\wsl$\Ubuntu-22.04\home\yuno\projects\TRELLIS.2\assets";

    // 選択中ファイルの UNC パス集合。
    private readonly HashSet<string> _selected = new();
    // Border → (uncPath, wslPath) の対応。
    private readonly Dictionary<Border, (string unc, string wsl)> _map = new();

    // 3D化が押されたとき、選ばれた画像の WSL 相対パス(例 assets/arch_xxx.png)が入る。
    // 呼び出し側はこれを RunSculptureFromImagesAsync に渡す。
    public List<string> SelectedForSculpt { get; } = new();

    public GalleryWindow()
    {
        InitializeComponent();
        LoadImages();
    }

    // assets 内の arch_*.png を読み込んでサムネイル表示する。
    private void LoadImages()
    {
        ImagesPanel.Children.Clear();
        _map.Clear();
        _selected.Clear();

        if (!Directory.Exists(AssetsUnc))
        {
            StatusText.Text = $"フォルダが見つかりません: {AssetsUnc}";
            return;
        }

        // arch_ で始まる png を新しい順に。
        var files = Directory.GetFiles(AssetsUnc, "arch_*.png")
            .OrderByDescending(f => f)
            .ToList();

        if (files.Count == 0)
        {
            StatusText.Text = "画像がまだありません。彫刻モードで生成すると貯まります。";
            return;
        }

        foreach (var unc in files)
        {
            string wsl = "assets/" + Path.GetFileName(unc);
            AddThumbnail(unc, wsl);
        }

        UpdateStatus();
    }

    // 1枚ぶんのサムネイル(Border+Image)を作って追加。クリックでトグル選択。
    private void AddThumbnail(string uncPath, string wslPath)
    {
        // UNC は UriSource が使えないので StreamSource で読む(ロック回避)。
        var bmp = new BitmapImage();
        try
        {
            using var fs = new FileStream(uncPath, FileMode.Open,
                FileAccess.Read, FileShare.Read);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 220; // サムネ用に縮小して読む
            bmp.StreamSource = fs;
            bmp.EndInit();
            bmp.Freeze();
        }
        catch
        {
            return; // 壊れた画像はスキップ
        }

        var image = new Image
        {
            Source = bmp,
            Stretch = Stretch.Uniform,
            Width = 200,
            Height = 200,
            Margin = new Thickness(4)
        };

        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(2),
            Background = Brushes.Black,
            Cursor = Cursors.Hand,
            Margin = new Thickness(6),
            Child = image
        };

        _map[border] = (uncPath, wslPath);

        border.MouseLeftButtonUp += (s, e) =>
        {
            if (s is not Border b) return;
            var (unc, _) = _map[b];
            if (_selected.Contains(unc))
            {
                _selected.Remove(unc);
                b.BorderBrush = Brushes.Gray;
            }
            else
            {
                _selected.Add(unc);
                b.BorderBrush = Brushes.DeepSkyBlue;
            }
            UpdateStatus();
        };

        ImagesPanel.Children.Add(border);
    }

    private void UpdateStatus()
    {
        StatusText.Text =
            $"全 {_map.Count} 枚 / 選択 {_selected.Count} 枚。" +
            "クリックで選択、「選択を3D化」または「選択を削除」。";
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadImages();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == 0)
        {
            StatusText.Text = "削除する画像を選んでください。";
            return;
        }

        var confirm = MessageBox.Show(
            $"選択した {_selected.Count} 枚を削除します。よろしいですか？",
            "削除の確認", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        foreach (var unc in _selected.ToList())
        {
            try { File.Delete(unc); } catch { /* 使用中などは無視 */ }
        }
        LoadImages(); // 一覧を作り直す
    }

    private void Sculpt_Click(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == 0)
        {
            StatusText.Text = "3D化する画像を選んでください。";
            return;
        }

        // 選択を WSL 相対パスに変換して返す。新しい順だと案番号が逆になるので
        // ファイル名昇順(=生成が古い順)に整える。
        SelectedForSculpt.Clear();
        SelectedForSculpt.AddRange(
            _map.Where(kv => _selected.Contains(kv.Value.unc))
                .Select(kv => kv.Value.wsl)
                .OrderBy(p => p));

        DialogResult = true; // 閉じて呼び出し側で3D化を走らせる
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
