using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModSorter.Services;

// 用語辞書。原文中の指定語を翻訳前にタグへ退避し、翻訳後に指定の訳語で戻す。
// DeepL には語そのものが渡らないので、多義語("Spring" が「春」になる等)を確実に固定できる。
//
// 退避には LangPackService の既存のプレースホルダ機構(<x id="n"/>)をそのまま使う。
// 新しい復元経路を作らないことで、壊れ方の種類を増やさない。
//
// 保存先は ApplicationData\ModSorter\glossary_ja.json。無ければ既定辞書を書き出す。
public static class GlossaryService
{
    public sealed class Term
    {
        // 原文側の語(英語)。例: "Spring"
        public string Source { get; set; } = "";
        // 置き換える訳語(日本語)。例: "バネ"
        public string Target { get; set; } = "";
        // 大文字小文字を区別するか。既定 false("spring" も拾う)。
        public bool CaseSensitive { get; set; }
        // 有効/無効。false なら一致させない。
        public bool Enabled { get; set; } = true;
    }

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ModSorter");

    public static string FilePath => Path.Combine(Dir, "glossary_ja.json");

    // 読み込み済みの用語。長い Source が先に来るよう整列して保持する。
    private static List<Term> _terms = new();
    private static bool _loaded;

    // 訳語に使えない文字。RestoreXml が復元後に XmlUnescape を通すため、
    // & < > を含む訳語は復元時に化ける。登録段階で弾く。
    private static bool IsValidTarget(string t)
        => !string.IsNullOrWhiteSpace(t) && t.IndexOfAny(new[] { '&', '<', '>' }) < 0;

    // MOD でよく出る多義語の既定辞書。初回起動時にファイルへ書き出される。
    // 以降はユーザーがファイルを直接編集して増やせる。
    private static List<Term> DefaultTerms() => new()
    {
        new Term { Source = "Spring", Target = "バネ" },
        new Term { Source = "Springs", Target = "バネ" },
        new Term { Source = "Chest", Target = "チェスト" },
        new Term { Source = "Bolt", Target = "ボルト" },
        new Term { Source = "Nut", Target = "ナット" },
        new Term { Source = "Wrench", Target = "レンチ" },
        new Term { Source = "Casing", Target = "ケーシング" },
        new Term { Source = "Shaft", Target = "シャフト" },
        new Term { Source = "Gear", Target = "歯車" },
        new Term { Source = "Cog", Target = "歯車" },
        new Term { Source = "Belt", Target = "ベルト" },
        new Term { Source = "Press", Target = "プレス" },
        new Term { Source = "Mixer", Target = "ミキサー" },
        new Term { Source = "Drain", Target = "ドレイン" },
        new Term { Source = "Tank", Target = "タンク" },
        new Term { Source = "Pipe", Target = "パイプ" },
        new Term { Source = "Valve", Target = "バルブ" },
        new Term { Source = "Coil", Target = "コイル" },
        new Term { Source = "Battery", Target = "バッテリー" },
        new Term { Source = "Cell", Target = "セル" },
        new Term { Source = "Core", Target = "コア" },
        new Term { Source = "Frame", Target = "フレーム" },
        new Term { Source = "Plate", Target = "板" },
        new Term { Source = "Rod", Target = "棒" },
        new Term { Source = "Ingot", Target = "インゴット" },
        new Term { Source = "Nugget", Target = "ナゲット" },
        new Term { Source = "Dust", Target = "粉" },
        new Term { Source = "Netherite", Target = "ネザライト" },
        new Term { Source = "Creeper", Target = "クリーパー" },
        new Term { Source = "Enderman", Target = "エンダーマン" },
        new Term { Source = "Shulker", Target = "シュルカー" },
        new Term { Source = "Redstone", Target = "レッドストーン" },
        new Term { Source = "Nether", Target = "ネザー" },
        new Term { Source = "End", Target = "エンド", CaseSensitive = true },
    };

    public static void Load(bool force = false)
    {
        if (_loaded && !force) return;
        _loaded = true;

        List<Term>? loaded = null;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                loaded = JsonSerializer.Deserialize<List<Term>>(json);
            }
        }
        catch { loaded = null; }

        if (loaded == null)
        {
            loaded = DefaultTerms();
            Save(loaded);
        }

        // 有効かつ両側が埋まっているものだけを残し、長い語を先に当てるため降順で並べる。
        // "Spring Loaded" を "Spring" より先に一致させ、部分置換で崩れるのを防ぐ。
        _terms = loaded
            .Where(t => t != null
                && !string.IsNullOrWhiteSpace(t.Source)
                && IsValidTarget(t.Target)
                && t.Enabled)
            .GroupBy(t => t.Source, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())   // 同じ語が重複していたら先勝ち
            .OrderByDescending(t => t.Source.Length)
            .ToList();
    }

    public static void Save(IEnumerable<Term> terms)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(terms, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder
                    .UnsafeRelaxedJsonEscaping   // 日本語をそのまま書く
            });
            File.WriteAllText(json is null ? FilePath : FilePath, json, new UTF8Encoding(false));
        }
        catch { }
    }

    // 現在有効な用語数。UI 表示用。
    public static int Count { get { Load(); return _terms.Count; } }

    public static IReadOnlyList<Term> Terms { get { Load(); return _terms; } }

    // 原文 src の中から用語の一致範囲を探し、(開始位置, 長さ, 訳語) の一覧を返す。
    // 一致は単語境界に限る。前後が英数字なら一致させない。
    //   "Spring" は "Springboard" や "Offspring" に一致しない。
    // 長い語を先に当て、既に確保した範囲とは重ねない。
    // 呼び出し側(ProtectXml)は、プレースホルダの範囲と競合しないことを確認して使う。
    public static List<(int Start, int Length, string Target)> FindMatches(string src)
    {
        Load();
        var hits = new List<(int Start, int Length, string Target)>();
        if (string.IsNullOrEmpty(src) || _terms.Count == 0) return hits;

        var taken = new bool[src.Length];

        foreach (var t in _terms)
        {
            var cmp = t.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int from = 0;
            while (from <= src.Length - t.Source.Length)
            {
                int at = src.IndexOf(t.Source, from, cmp);
                if (at < 0) break;
                int end = at + t.Source.Length;
                from = at + 1;

                // 単語境界の確認。前後が英数字なら語の一部なので採らない。
                if (at > 0 && IsWordChar(src[at - 1])) continue;
                if (end < src.Length && IsWordChar(src[end])) continue;

                // 既に他の用語が押さえた範囲とは重ねない。
                bool overlap = false;
                for (int i = at; i < end; i++) if (taken[i]) { overlap = true; break; }
                if (overlap) continue;

                for (int i = at; i < end; i++) taken[i] = true;
                hits.Add((at, t.Source.Length, t.Target));
            }
        }

        hits.Sort((a, b) => a.Start.CompareTo(b.Start));
        return hits;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c);
}
