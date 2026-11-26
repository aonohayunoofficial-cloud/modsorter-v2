using ModSorter.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ModSorter;

// 一覧の絞り込み。ツリー選択・カテゴリ・検索・配布元(CurseForge / Modrinth)。
public partial class MainWindow : Window
{
    private void ModTree_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item) return;

        if (item.Tag is ModEntry mod)
        {
            // MOD名クリック: 中央へスクロール強調＋右パネルに詳細
            ShowDetail(mod);

            // カテゴリ表示で絞り込み中だと対象が中央に居ない場合があるので
            // 念のため、その時点のCardListに含まれていればスクロール強調する
            if (CardList.Items.Contains(mod))
            {
                CardList.ScrollIntoView(mod);
                Dispatcher.BeginInvoke(new Action(() => HighlightCard(mod)),
                    DispatcherPriority.Loaded);
            }
        }
        else if (item.Tag is string category)
        {
            // カテゴリ名クリック: 中央をそのカテゴリのMODだけに絞り込む
            FilterCardsByCategory(category);
        }
    }

    // 中央カードを指定カテゴリのMODだけに絞り込む(空文字=未分類)
    private void FilterCardsByCategory(string category)
    {
        _activeCategory = category;   // カテゴリ絞り込みを記憶(検索と併用)
        ApplyCardFilter();
    }

    // 検索ボックスの入力変更(デバウンス: 入力が止まって250ms後に実行)
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_searchTimer == null)
        {
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchTimer.Tick += (_, __) =>
            {
                _searchTimer!.Stop();
                ApplyCardFilter();
            };
        }
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    // AND/ORトグルの変更
    private void SearchMode_Changed(object sender, RoutedEventArgs e)
    {
        // 初期化中(SearchBox未生成)はスキップ
        if (SearchBox == null) return;
        ApplyCardFilter();
    }

    // 現在の配布元＋カテゴリ絞り込み＋検索語で中央カードを絞り込む
    private void ApplyCardFilter()
    {
        var sorted = GetVisibleMods();

        // 1) カテゴリ絞り込み(カテゴリ表示でカテゴリ名選択中のみ)
        IEnumerable<ModEntry> baseList = sorted;
        if (_activeCategory != null)
        {
            if (_activeCategory.Length == 0)
            {
                baseList = sorted.Where(m => m.Categories == null || m.Categories.Count == 0);
            }
            else
            {
                baseList = sorted.Where(m => m.Categories != null &&
                    m.Categories.Any(c => string.Equals(c, _activeCategory,
                        StringComparison.OrdinalIgnoreCase)));
            }
        }

        // 2) 検索語で絞り込み(名前・カテゴリ・ModId、AND/OR)
        string raw = SearchBox?.Text?.Trim() ?? "";
        if (raw.Length > 0)
        {
            var terms = raw.Split(new[] { ' ', '　' },
                StringSplitOptions.RemoveEmptyEntries);
            bool andMode = SearchAndRadio?.IsChecked == true;

            baseList = baseList.Where(m =>
            {
                bool MatchTerm(string term)
                {
                    if (m.DisplayName != null &&
                        m.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (m.ModId != null &&
                        m.ModId.Contains(term, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (m.Categories != null &&
                        m.Categories.Any(c => c != null &&
                            c.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        return true;
                    return false;
                }
                return andMode ? terms.All(MatchTerm) : terms.Any(MatchTerm);
            });
        }
        var result = baseList.ToList();
        CardList.ItemsSource = result;
    }

    // ===== 配布元(CurseForge / Modrinth)の絞り込み =====
    // 並び順を適用したうえで配布元の絞り込みをかけた一覧。ツリーとカードの元になる。
    private List<ModEntry> GetVisibleMods()
        => GetSortedMods().Where(MatchesSourceFilter).ToList();

    // 両方チェック=全件 / 片方だけ=そちらにしか URL が無い MOD / 両方外す=どちらも無い MOD。
    // URL はファイルのハッシュ照合で付くので、「その jar がそちらでしか一致しなかった」の意味になる。
    private bool MatchesSourceFilter(ModEntry m)
    {
        bool cf = SourceCfCheck?.IsChecked != false;
        bool mr = SourceMrCheck?.IsChecked != false;
        if (cf && mr) return true;

        bool hasCf = !string.IsNullOrWhiteSpace(m.CurseForgeUrl);
        bool hasMr = !string.IsNullOrWhiteSpace(m.ModrinthUrl);
        if (cf) return hasCf && !hasMr;
        if (mr) return hasMr && !hasCf;
        return !hasCf && !hasMr;
    }

    // 配布元チェックボックスの切り替え
    private void SourceFilter_Changed(object sender, RoutedEventArgs e)
    {
        // xaml 読み込み途中は部品が揃っていないので何もしない
        if (SourceCfCheck == null || SourceMrCheck == null || SourceFilterInfo == null ||
            ModTree == null || CardList == null) return;
        if (_mods.Count == 0) { UpdateSourceFilterInfo(0); return; }

        RefreshModViews();   // ツリーとカードを配布元で絞り込み直す
        ApplyCardFilter();   // カードへカテゴリ・検索を重ね直す
    }

    // チェックボックス横の件数表示。両方チェック(全件)のときは出さない。
    private void UpdateSourceFilterInfo(int visible)
    {
        if (SourceFilterInfo == null) return;
        bool cf = SourceCfCheck?.IsChecked != false;
        bool mr = SourceMrCheck?.IsChecked != false;
        string label = cf && mr ? ""
            : cf ? "CurseForge のみ"
            : mr ? "Modrinth のみ"
            : "どちらにも無い";
        SourceFilterInfo.Text = label.Length == 0 || _mods.Count == 0
            ? ""
            : $"{label}: {visible} / {_mods.Count} 件";
    }
}
