using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModSorter.Architect.Generation
{
    // テクスチャ実在フィルタ済みのブロックパレット。
    // 初回だけ全MODを走査して JSON に吐き、2回目以降はそれを読むだけにする。
    public sealed class PaletteEntry
    {
        public string Id { get; set; } = "";
        public string Mod { get; set; } = "";       // namespace (例: minecraft, create)
        public string Category { get; set; } = "";  // 名前ヒューリスティック分類
        public int[] Color { get; set; } = new[] { 128, 128, 128 }; // 代表色 RGB
    }

    public sealed class BlockPaletteCache
    {
        // キャッシュ形式のバージョン。生成ロジックを変えたら上げて作り直させる。
        public int Version { get; set; } = 2;
        // jar 構成のフィンガープリント。これが変われば再生成する。
        public string Fingerprint { get; set; } = "";
        public List<PaletteEntry> Entries { get; set; } = new();

        // ===== 除外MOD(ハードコード) =====
        // 建材に使わない/ノイズが多いと判明しているMOD。namespace(小文字)で照合。
        // 料理・農業・モブファーム・装甲ログ・中間モデル等。
        private static readonly HashSet<string> ExcludedMods = new(StringComparer.OrdinalIgnoreCase)
        {
            "createfood", "displaydelight", "mm_cooking", "farmersdelight",
            "cannoncompressedarmor", "mob_farms", "easy_mob_farm",
            "productivebees", "cobblemon", "travelersbackpack",
            "realmrpg_skeletons", "mts", "create_bb", "botanypots",
        };

        // ===== 名前ヒューリスティック分類 =====
        // 末尾/含む語で「装飾(shape)・機能(functional)・通常(normal)」に振り分ける。
        private static readonly string[] DecorTokens =
        {
            "_stairs", "_slab", "_fence", "_wall", "_door", "_trapdoor",
            "_button", "_pressure_plate", "_pane", "_bars", "_carpet",
            "_sign", "_fence_gate", "_ladder",
        };
        private static readonly string[] FunctionalTokens =
        {
            "machine", "furnace", "chest", "tank", "generator", "pipe",
            "cable", "controller", "crafter", "barrel", "hopper",
            "cogwheel", "shaft", "flywheel", "fan", "boiler",
        };

        public static string Classify(string id)
        {
            // namespace を落とした本体名で判定。
            string name = id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id;
            string lower = name.ToLowerInvariant();
            if (DecorTokens.Any(t => lower.Contains(t))) return "decor";
            if (FunctionalTokens.Any(t => lower.Contains(t))) return "functional";
            return "normal";
        }

        public static bool IsExcludedMod(string ns) => ExcludedMods.Contains(ns);

        // ===== フィンガープリント =====
        // バニラjar + MOD jar群のパスと最終更新時刻を連結したものをハッシュ化。
        // MODの増減・更新で変わるので、変化したら再生成すべきと判定できる。
        public static string ComputeFingerprint(string? vanillaJar, IEnumerable<string> modJars)
        {
            var parts = new List<string>();
            void Add(string? p)
            {
                if (string.IsNullOrEmpty(p)) return;
                try
                {
                    var fi = new FileInfo(p);
                    parts.Add($"{p}|{(fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0)}|{(fi.Exists ? fi.Length : 0)}");
                }
                catch { parts.Add(p); }
            }
            Add(vanillaJar);
            foreach (var j in modJars.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                Add(j);
            string joined = string.Join("\n", parts);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(joined));
            return Convert.ToHexString(hash);
        }

        // ===== 保存先 =====
        public static string CachePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "block_palette_cache.json");

        public static BlockPaletteCache? TryLoad(string expectedFingerprint)
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                var json = File.ReadAllText(CachePath);
                var cache = JsonSerializer.Deserialize<BlockPaletteCache>(json);
                if (cache == null) return null;
                if (cache.Version != 2) return null;
                if (cache.Fingerprint != expectedFingerprint) return null; // 構成が変わった
                return cache;
            }
            catch { return null; }
        }

        // フィンガープリントを無視して、存在すれば読む。
        // BlockPickerWindow など jar 情報を持たない側がツリー表示に使う。
        // 構成変化の検知は MainWindow 側の TryLoad(fingerprint) に任せる。
        public static BlockPaletteCache? TryLoadAny()
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                var json = File.ReadAllText(CachePath);
                var cache = JsonSerializer.Deserialize<BlockPaletteCache>(json);
                if (cache == null || cache.Version != 2) return null;
                return cache;
            }
            catch { return null; }
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(CachePath, json);
        }

        // ===== 生成本体 =====
        // tp: 既に vanilla+mod jar を渡して構築済みの BlockTextureProvider。
        // progress(current, total, label) は UI へ進捗を返すコールバック(任意)。
        // 重い処理なので呼び出し側で Task.Run に乗せること。
        public static BlockPaletteCache Build(
            BlockTextureProvider tp,
            string fingerprint,
            Action<int, int, string>? progress = null)
        {
            var byMod = tp.EnumerateBlocks();

            // 除外MODを落とし、対象IDを平坦化する。
            var targets = new List<(string id, string mod)>();
            foreach (var kv in byMod)
            {
                if (IsExcludedMod(kv.Key)) continue;
                foreach (var id in kv.Value)
                    targets.Add((id, kv.Key));
            }

            int total = targets.Count;
            var result = new BlockPaletteCache { Fingerprint = fingerprint };

            for (int i = 0; i < total; i++)
            {
                var (id, mod) = targets[i];

                // テクスチャ実在フィルタ: 取れないものは捨てる。
                var png = tp.GetTexture(id);
                if (png == null || png.Length == 0)
                {
                    if (i % 200 == 0) progress?.Invoke(i + 1, total, $"走査中 {i + 1}/{total}（採用 {result.Entries.Count}）");
                    continue;
                }

                // 代表色を同時に確保(取れなければグレー)。
                var avg = BlockColorSampler.GetAverageColor(tp, id);
                int[] color = avg != null && avg.Length >= 3
                    ? new[] { avg[0], avg[1], avg[2] }
                    : new[] { 128, 128, 128 };

                result.Entries.Add(new PaletteEntry
                {
                    Id = id,
                    Mod = mod,
                    Category = Classify(id),
                    Color = color,
                });

                if (i % 200 == 0 || i == total - 1)
                    progress?.Invoke(i + 1, total, $"走査中 {i + 1}/{total}（採用 {result.Entries.Count}）");
            }

            return result;
        }
    }
}
