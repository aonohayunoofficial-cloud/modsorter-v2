using ModSorter.Clients;
using ModSorter.Models;
using ModSorter.Services;
using System.IO;
using System.Windows;

namespace ModSorter;

public partial class MainWindow : Window
{
    // ===== Mods スキャン =====
    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_instancePath))
        {
            MessageBox.Show("先に設定で .minecraft フォルダを選択してください。", "ModSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var modsDir = Path.Combine(_instancePath, "mods");
        if (!Directory.Exists(modsDir))
        {
            Log("mods\\ フォルダが見つかりません。");
            return;
        }

        var jars = Directory.GetFiles(modsDir, "*.jar");
        _mods = jars.Select(JarReader.Read).ToList();
        RefreshModViews();
        Log($"{_mods.Count} 個の .jar を読み取りました。オンライン照合を開始します...");

        // SHA1とファイル情報を先に取得してModEntryに保持
        foreach (var mod in _mods)
        {
            mod.Sha1 = ModrinthClient.Sha1(mod.FilePath);
            try
            {
                var fi = new FileInfo(mod.FilePath);
                mod.FileSize = fi.Length;
                mod.FileCreated = fi.CreationTime;
                mod.FileModified = fi.LastWriteTime;
            }
            catch { }
        }

        // キャッシュ適用: ヒットしたものはAPI対象から外す
        var toFetch = new List<ModEntry>();
        int fromCache = 0;
        foreach (var mod in _mods)
        {
            var c = ModCache.Get(mod.Sha1);
            if (c != null)
            {
                mod.ModrinthUrl = c.ModrinthUrl;
                mod.CurseForgeUrl = c.CurseForgeUrl;
                mod.Body = c.Body;
                mod.BodyIsHtml = c.BodyIsHtml;
                mod.IconUrl = c.IconUrl;
                mod.IconFile = (!string.IsNullOrEmpty(c.IconFile) && File.Exists(c.IconFile))
                    ? c.IconFile : "";
                mod.Categories = c.Categories ?? new();
                mod.CategorySource = c.CategorySource;
                mod.LlmCategories = c.LlmCategories ?? new();
                fromCache++;
            }
            else
            {
                toFetch.Add(mod);
            }
        }
        RefreshModViews();
        Log($"キャッシュ適用: {fromCache} 件。新規照合対象: {toFetch.Count} 件。");

        if (toFetch.Count == 0)
        {
            ScanStatus.Text = $"完了(全てキャッシュ): {_mods.Count} 件";
            ScanProgress.Visibility = Visibility.Collapsed;
            ModCache.Save();
            AddActivity($"スキャン完了: {_mods.Count} 件 (全てキャッシュ)");
            return;
        }

        // 進捗UIを表示
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.Value = 0;
        ScanStatus.Text = "照合中...";

        int total = toFetch.Count;
        var lockObj = new object();
        int mrMatched = 0, cfMatched = 0, done = 0;

        var cfKey = Settings.Decrypt(_settings.CurseForgeKeyEnc);
        bool useCf = !string.IsNullOrEmpty(cfKey);
        if (useCf) CurseForgeClient.Init(cfKey);
        else Log("CurseForge APIキー未設定のため、Modrinthのみ照合します。");

        int grandTotal = total + (useCf ? total : 0);

        void Bump()
        {
            int cur;
            lock (lockObj) cur = ++done;
            Dispatcher.Invoke(() =>
            {
                ScanProgress.Value = grandTotal == 0 ? 100 : (cur * 100.0 / grandTotal);
                int shown = useCf ? (cur + 1) / 2 : cur;
                if (shown > total) shown = total;
                ScanStatus.Text = $"照合中... {shown}/{total}";
            });
        }

        var mrSem = new SemaphoreSlim(5);
        var cfSem = new SemaphoreSlim(3);

        var mrTasks = toFetch.Select(async mod =>
        {
            await mrSem.WaitAsync();
            try
            {
                var r = await ModrinthClient.GetByHashAsync(mod.FilePath);
                if (r != null)
                {
                    mod.ModrinthUrl = r.Url;
                    mod.Body = r.Body;
                    mod.BodyIsHtml = false;
                    if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                    // カテゴリはCurseForge優先。CFがまだ設定していなければModrinthで埋める
                    if (mod.Categories.Count == 0 && r.Categories.Count > 0)
                    {
                        mod.Categories = r.Categories;
                        mod.CategorySource = "Modrinth";
                    }
                    lock (lockObj) mrMatched++;
                }

            }
            finally { mrSem.Release(); Bump(); }
        });

        IEnumerable<Task> cfTasks = Array.Empty<Task>();
        if (useCf)
        {
            cfTasks = toFetch.Select(async mod =>
            {
                await cfSem.WaitAsync();
                try
                {
                    var r = await CurseForgeClient.GetByFingerprintAsync(mod.FilePath);
                    if (r != null && !string.IsNullOrEmpty(r.Url))
                    {
                        mod.CurseForgeUrl = r.Url;
                        if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                        if (string.IsNullOrEmpty(mod.Body))
                        {
                            if (!string.IsNullOrEmpty(r.DescriptionHtml))
                            {
                                mod.Body = r.DescriptionHtml;
                                mod.BodyIsHtml = true;
                            }
                            else
                            {
                                mod.Body = r.Summary;
                                mod.BodyIsHtml = false;
                            }
                        }
                        // CurseForgeのカテゴリを優先採用(Modrinthが設定済みでも上書き)
                        if (r.Categories.Count > 0)
                        {
                            mod.Categories = r.Categories;
                            mod.CategorySource = "CurseForge";
                        }
                        lock (lockObj) cfMatched++;
                    }

                }
                finally { cfSem.Release(); Bump(); }
            });
        }

        await Task.WhenAll(mrTasks.Concat(cfTasks));

        // アイコンをローカル保存
        ScanStatus.Text = "アイコンを保存中...";
        foreach (var mod in toFetch)
        {
            if (!string.IsNullOrEmpty(mod.IconUrl))
                mod.IconFile = await ModCache.EnsureIconAsync(mod.Sha1, mod.IconUrl);
        }

        // キャッシュに保存
        foreach (var mod in toFetch)
        {
            ModCache.Put(new CacheEntry
            {
                Sha1 = mod.Sha1,
                ModId = mod.ModId,
                Version = mod.Version,
                Loader = mod.Loader,
                ModrinthUrl = mod.ModrinthUrl,
                CurseForgeUrl = mod.CurseForgeUrl,
                Body = mod.Body,
                BodyIsHtml = mod.BodyIsHtml,
                IconUrl = mod.IconUrl,
                IconFile = mod.IconFile,
                Categories = mod.Categories,
                CategorySource = mod.CategorySource,
                LlmCategories = mod.LlmCategories
            });

        }
        ModCache.Save();

        ScanStatus.Text = $"完了: MR {mrMatched} / CF {cfMatched}(新規 {total} 件)";
        ScanProgress.Value = 100;
        Log($"照合完了: Modrinth {mrMatched} 件、CurseForge {cfMatched} 件。キャッシュ保存済み。");
        AddActivity($"スキャン完了: {_mods.Count} 件 (新規照合 {total} 件)");
        RefreshModViews();
    }


    // 1件のMODをオンライン照合し、アイコン保存とキャッシュ書き戻しまで行う
    private async Task<bool> FetchOneAsync(ModEntry mod)
    {
        bool hit = false;

        // 一旦クリア(古いキャッシュ由来データを消して取り直す)
        mod.ModrinthUrl = "";
        mod.CurseForgeUrl = "";
        mod.Body = "";
        mod.BodyIsHtml = false;
        mod.IconUrl = "";
        mod.IconFile = "";
        mod.Categories = new();
        mod.CategorySource = "";
        mod.TranslatedHtml = "";

        // SHA1とファイル情報が未取得なら取得
        if (string.IsNullOrEmpty(mod.Sha1))
            mod.Sha1 = ModrinthClient.Sha1(mod.FilePath);

        // Modrinth照合
        try
        {
            var r = await ModrinthClient.GetByHashAsync(mod.FilePath);
            if (r != null)
            {
                mod.ModrinthUrl = r.Url;
                mod.Body = r.Body;
                mod.BodyIsHtml = false;
                if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                if (mod.Categories.Count == 0 && r.Categories.Count > 0)
                {
                    mod.Categories = r.Categories;
                    mod.CategorySource = "Modrinth";
                }
                hit = true;
            }
        }
        catch { }

        // CurseForge照合(APIキーがあれば)
        var cfKey = Settings.Decrypt(_settings.CurseForgeKeyEnc);
        if (!string.IsNullOrEmpty(cfKey))
        {
            if (!CurseForgeClient.IsReady) CurseForgeClient.Init(cfKey);
            try
            {
                var r = await CurseForgeClient.GetByFingerprintAsync(mod.FilePath);
                if (r != null && !string.IsNullOrEmpty(r.Url))
                {
                    mod.CurseForgeUrl = r.Url;
                    if (string.IsNullOrEmpty(mod.IconUrl)) mod.IconUrl = r.IconUrl;
                    if (string.IsNullOrEmpty(mod.Body))
                    {
                        if (!string.IsNullOrEmpty(r.DescriptionHtml))
                        {
                            mod.Body = r.DescriptionHtml;
                            mod.BodyIsHtml = true;
                        }
                        else
                        {
                            mod.Body = r.Summary;
                            mod.BodyIsHtml = false;
                        }
                    }
                    if (r.Categories.Count > 0)
                    {
                        mod.Categories = r.Categories;
                        mod.CategorySource = "CurseForge";
                    }
                    hit = true;
                }
            }
            catch { }
        }

        // アイコンをローカル保存
        if (!string.IsNullOrEmpty(mod.IconUrl))
            mod.IconFile = await ModCache.EnsureIconAsync(mod.Sha1, mod.IconUrl);

        // キャッシュに書き戻し
        ModCache.Put(new CacheEntry
        {
            Sha1 = mod.Sha1,
            ModId = mod.ModId,
            Version = mod.Version,
            Loader = mod.Loader,
            ModrinthUrl = mod.ModrinthUrl,
            CurseForgeUrl = mod.CurseForgeUrl,
            Body = mod.Body,
            BodyIsHtml = mod.BodyIsHtml,
            IconUrl = mod.IconUrl,
            IconFile = mod.IconFile,
            Categories = mod.Categories,
            CategorySource = mod.CategorySource,
            LlmCategories = mod.LlmCategories
        });

        ModCache.Save();

        return hit;
    }
}
