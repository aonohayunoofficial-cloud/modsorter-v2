using ModSorter.Clients;
using ModSorter.Models;
using System.Windows;
using System.Windows.Controls;

namespace ModSorter;

// 設定画面(Tab 3)の処理。MainWindow.xaml.cs が 10,000 バイトを超えたため分けた。
//   キー保存 / DeepL 残量表示 / 日本語化パックの翻訳エンジン選択 / Azure 欄
public partial class MainWindow : Window
{
    // コンボへ選択肢を詰めている間・選択を戻している間は SelectionChanged を無視する。
    private bool _engineComboLoading;

    // 起動時に1回呼ぶ。エンジン選択・Azure 欄・状態表示を設定値から組み立てる。
    private void InitTranslatorSettingsUi()
    {
        _engineComboLoading = true;
        TranslatorEngineCombo.Items.Clear();
        var current = LangPackEngine;
        foreach (var (id, label) in TranslatorFactory.Engines)
        {
            var item = new ComboBoxItem { Content = label, Tag = id };
            TranslatorEngineCombo.Items.Add(item);
            if (id == current) TranslatorEngineCombo.SelectedItem = item;
        }
        _engineComboLoading = false;

        AzureRegionBox.Text = _settings.AzureTranslatorRegion;
        UpdateSettingsStatus("");
        RefreshAzureStatus();
        UpdateLangPackEngineLabel();
    }

    // エンジン選択。選んだ時点で保存する。保存ボタンを押し忘れたまま
    // 別エンジンで生成が走るのを防ぐため。
    private void TranslatorEngine_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_engineComboLoading) return;
        if (TranslatorEngineCombo.SelectedItem is not ComboBoxItem item) return;
        var id = TranslatorFactory.Normalize(item.Tag as string);
        if (id == LangPackEngine) return;

        // 実行中は切り替えない。走っている処理と画面表示・キャッシュの宛先がずれるため。
        if (_langPackCts != null)
        {
            _engineComboLoading = true;
            TranslatorEngineCombo.SelectedItem =
                e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            _engineComboLoading = false;
            MessageBox.Show("生成/再翻訳の実行中は翻訳エンジンを切り替えられません。",
                "ModSorter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.TranslatorEngine = id;
        _settings.Save();
        UpdateSettingsStatus("");
        UpdateLangPackEngineLabel();
        Log($"日本語化パックの翻訳エンジンを {item.Content} に切り替えました。");
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings.InstancePath = _instancePath ?? "";

        // 入力欄が空のときは既存のキーを残す作り。今回貼り替えたかを表示で区別し、
        // 古いキーが残ったまま枠切れ(DeepL の 456 等)が続く状態に気づけるようにする。
        bool cfUpdated = !string.IsNullOrEmpty(CfKeyBox.Password);
        if (cfUpdated)
            _settings.CurseForgeKeyEnc = Settings.Encrypt(CfKeyBox.Password.Trim());
        bool dlUpdated = !string.IsNullOrEmpty(DeepLKeyBox.Password);
        if (dlUpdated)
            _settings.DeepLKeyEnc = Settings.Encrypt(DeepLKeyBox.Password.Trim());
        bool azUpdated = !string.IsNullOrEmpty(AzureKeyBox.Password);
        if (azUpdated)
            _settings.AzureTranslatorKeyEnc = Settings.Encrypt(AzureKeyBox.Password.Trim());

        // リージョンは空も有効(グローバルのリソース)なので常に書く。
        // "Japan East" と打たれても通るよう、空白を除いて小文字にそろえる。
        _settings.AzureTranslatorRegion =
            (AzureRegionBox.Text ?? "").Replace(" ", "").Trim().ToLowerInvariant();
        AzureRegionBox.Text = _settings.AzureTranslatorRegion;
        _settings.Save();

        UpdateSettingsStatus("保存しました。", cfUpdated, dlUpdated, azUpdated);
        Log($"設定を保存しました。(DeepL: {(dlUpdated ? "今回更新" : "変更なし")} / " +
            $"Azure: {(azUpdated ? "今回更新" : "変更なし")})");
        CfKeyBox.Password = "";
        DeepLKeyBox.Password = "";
        AzureKeyBox.Password = "";

        // 保存したキーで表示を取り直す。キーを替えたのに末尾4文字が動かないときは、
        // 貼り替えが効いていないか、別のキーが残っている。
        RefreshAzureStatus();
        _ = RefreshDeepLUsageAsync();
    }

    // 各キーの状態と選択中エンジンを出す。head が空なら起動時・切替時の表示。
    private void UpdateSettingsStatus(string head,
        bool cfUpdated = false, bool dlUpdated = false, bool azUpdated = false)
    {
        bool saved = head.Length > 0;
        string State(string enc, bool updated) =>
            string.IsNullOrEmpty(enc) ? "未設定"
            : !saved ? "保存済み"
            : updated ? "今回更新" : "保存済み(今回は変更なし)";

        SettingsStatus.Text =
            (saved ? head + "\n" : "") +
            $"CurseForge: {State(_settings.CurseForgeKeyEnc, cfUpdated)}\n" +
            $"DeepL: {State(_settings.DeepLKeyEnc, dlUpdated)}\n" +
            $"Azure: {State(_settings.AzureTranslatorKeyEnc, azUpdated)}\n" +
            $"日本語化パックの翻訳エンジン: {LangPackEngineLabel}\n" +
            "(キーは変更する場合のみ再入力)";
    }

    // Azure 欄の状態表示。Azure はキーだけでは消費量を取れないので、
    // どのキー・どの宛先で送るかだけを出す。
    private void RefreshAzureStatus()
    {
        var key = Settings.Decrypt(_settings.AzureTranslatorKeyEnc);
        if (string.IsNullOrEmpty(key))
        {
            AzureStatusText.Text = "状態: (キー未設定)";
            return;
        }
        var az = new AzureTranslator(key, _settings.AzureTranslatorRegion);
        AzureStatusText.Text =
            $"状態: キー末尾 …{az.KeyTail} / {az.Endpoint}\n" +
            "消費量はキーから取得できません(Azure ポータルのリソースのメトリックで確認)";
    }

    // 日本語化パック欄の見出しを選択中エンジンに合わせる。
    // 前のエンジンの残量表示が残ると取り違えるので消す(次の生成で入り直す)。
    private void UpdateLangPackEngineLabel()
    {
        LangPackTitle.Text = $"日本語化パック生成({LangPackEngineLabel})";
        LangPackUsage.Text = "";
    }

    // 「残量を確認」ボタン(DeepL)。
    private void DeepLUsage_Click(object sender, RoutedEventArgs e)
        => _ = RefreshDeepLUsageAsync();

    // DeepL の消費を取り直して設定画面へ出す。v2/usage は文字を消費しないので、
    // 何度呼んでも枠は減らない。どのキーで動いているかを末尾4文字で添える。
    // DeepL キーは MOD 説明文の翻訳・プロンプト英訳でも使うため、選択中エンジンに
    // かかわらず DeepL 欄の表示として残す。
    private async Task RefreshDeepLUsageAsync()
    {
        var key = Settings.Decrypt(_settings.DeepLKeyEnc);
        if (string.IsNullOrEmpty(key))
        {
            DeepLUsageText.Text = "消費: (キー未設定)";
            return;
        }

        DeepLUsageText.Text = "消費: 取得中...";
        DeepLClient.Init(key);
        var usage = await DeepLClient.GetUsageAsync();

        if (usage.HasValue)
        {
            long used = usage.Value.Count, limit = usage.Value.Limit;
            int pct = limit <= 0 ? 0 : (int)(used * 100 / limit);
            DeepLUsageText.Text =
                $"消費: {used:N0} / {limit:N0} 文字（{pct}%） / キー末尾 …"
                + DeepLClient.KeyTail;
        }
        else
        {
            DeepLUsageText.Text =
                $"消費: 取得失敗（{DeepLClient.LastError}） / 宛先 "
                + DeepLClient.BaseUrl;
        }
    }
}
