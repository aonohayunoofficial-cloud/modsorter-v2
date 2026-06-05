using ModSorter.Clients;
using System.Windows;
using System.Windows.Controls;

namespace ModSorter;

public partial class MainWindow : Window
{
    // ===== 取得モード(選択・再取得・Ollama分類) =====
    private bool _selectionMode = false;

    private void CardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CardList.SelectedItem is not ModEntry mod) return;

        if (_selectionMode)
        {
            // 選択モード中: クリックでチェック切替(詳細は出さない)
            mod.IsSelected = !mod.IsSelected;
            RefreshSelectedList();
            UpdateRefetchSelectedLabel();
            // ListBoxの選択ハイライト自体は使わないので選択を解除しておく
            CardList.SelectedItem = null;
        }
        else
        {
            ShowDetail(mod);
        }
    }

    private void SelectMode_Click(object sender, RoutedEventArgs e)
    {
        _selectionMode = !_selectionMode;

        // 全MODのSelectionModeフラグを更新（カードのチェックボックス表示制御）
        foreach (var m in _mods)
            m.SelectionMode = _selectionMode;

        if (_selectionMode)
        {
            // 選択モードに入る
            SelectModeBtn.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Collapsed;
            SelectionPanel.Visibility = Visibility.Visible;

            RefreshSelectedList();
            UpdateRefetchSelectedLabel();
        }
        else
        {
            // 選択モードを抜ける：全チェック解除して通常表示へ
            foreach (var m in _mods)
                m.IsSelected = false;

            SelectModeBtn.Visibility = Visibility.Visible;
            SelectionPanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var mod in _mods) mod.IsSelected = true;
        RefreshSelectedList();
        UpdateRefetchSelectedLabel();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var mod in _mods) mod.IsSelected = false;
        RefreshSelectedList();
        UpdateRefetchSelectedLabel();
    }

    // 選択中MOD一覧を再構築
    private void RefreshSelectedList()
    {
        var selected = _mods.Where(m => m.IsSelected)
                            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                            .ToList();
        SelectedList.ItemsSource = selected;
    }

    // 選択中リストの行クリック → 中央カードへスクロール＋黄枠ハイライト
    private void SelectedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedList.SelectedItem is not ModEntry mod) return;

        // クリックを単発扱いにするため選択状態はリセット
        SelectedList.SelectedItem = null;

        CardList.ScrollIntoView(mod);

        // 仮想化でコンテナ生成が間に合わないことがあるため遅延実行
        Dispatcher.BeginInvoke(new Action(() => HighlightCard(mod)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateRefetchSelectedLabel()
    {
        int n = _mods.Count(m => m.IsSelected);
        RefetchSelectedBtn.Content = $"選択を再取得({n}件)";
        OllamaSelectedBtn.Content = $"Ollama取得({n}件)";
    }

    private async void RefetchSelected_Click(object sender, RoutedEventArgs e)
    {
        var targets = _mods.Where(m => m.IsSelected).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("再取得するMODを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"{targets.Count} 件のMODを再取得します。よろしいですか?",
            "ModSorter - 選択再取得",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        RefetchSelectedBtn.IsEnabled = false;
        SelectModeBtn.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;

        int done = 0, hit = 0;
        foreach (var mod in targets)
        {
            bool ok = await FetchOneAsync(mod);
            if (ok) hit++;
            done++;
            ScanProgress.Value = done * 100.0 / targets.Count;
            ScanStatus.Text = $"再取得中... {done}/{targets.Count}";
        }

        ScanProgress.Value = 100;
        ScanStatus.Text = $"再取得完了: {hit}/{targets.Count} 件ヒット";
        RefetchSelectedBtn.IsEnabled = true;
        SelectModeBtn.IsEnabled = true;
        Log($"選択再取得完了: {hit}/{targets.Count} 件ヒット。");

        RefreshModViews();
        RefreshSelectedList();
    }

    // 取得済みカテゴリを集計して候補リストを作る(案A)
    // CurseForge/Modrinthどちらの体系かは取得時点で決まっているので
    // _mods全体のCategoriesをユニーク化すれば、その出所の体系になる
    private List<string> BuildCategoryCandidates()
    {
        return _mods
            .Where(m => m.Categories != null)
            .SelectMany(m => m.Categories)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // 選択中MODをOllamaでまとめて再分類(直列)
    private async void OllamaSelected_Click(object sender, RoutedEventArgs e)
    {
        var targets = _mods.Where(m => m.IsSelected).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("分類するMODを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 候補カテゴリ(取得済みカテゴリの集計)
        var candidates = BuildCategoryCandidates();
        if (candidates.Count == 0)
        {
            MessageBox.Show(
                "候補となるカテゴリがありません。\n先にスキャンしてAPIカテゴリを取得してください。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Ollamaの起動確認
        Log("Ollamaの起動を確認中...");
        if (!await OllamaClient.IsAvailableAsync())
        {
            MessageBox.Show(
                "Ollamaに接続できません。\nOllamaが起動しているか確認してください(http://localhost:11434)。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Warning);
            Log("Ollama分類中止: 接続不可。");
            return;
        }

        OllamaSelectedBtn.IsEnabled = false;
        RefetchSelectedBtn.IsEnabled = false;
        SelectModeBtn.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;
        ScanProgress.Maximum = targets.Count;

        int done = 0, ok = 0;
        foreach (var mod in targets)
        {
            ScanStatus.Text = $"Ollama分類中: {mod.DisplayName} ({done + 1}/{targets.Count})";
            var result = await OllamaClient.ClassifyAsync(
                mod.DisplayName, mod.Body, candidates);

            if (result != null && result.Count > 0)
            {
                mod.LlmCategories = result;   // INotifyで表示は自動更新
                ok++;
                Log($"Ollama分類: {mod.DisplayName} → {string.Join(", ", result)}");
            }
            else
            {
                Log($"Ollama分類失敗({mod.DisplayName}): {OllamaClient.LastError}");
            }

            done++;
            ScanProgress.Value = done;
        }

        ScanStatus.Text = "";
        ScanProgress.Visibility = Visibility.Collapsed;
        OllamaSelectedBtn.IsEnabled = true;
        RefetchSelectedBtn.IsEnabled = true;
        SelectModeBtn.IsEnabled = true;

        Log($"Ollama分類完了: {targets.Count} 件中 {ok} 件成功。");
        if (ok < targets.Count)
        {
            MessageBox.Show(
                $"{targets.Count} 件中 {ok} 件を分類しました。\n失敗した分はログを確認してください。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        RefreshModViews();
        RefreshSelectedList();
    }
}
