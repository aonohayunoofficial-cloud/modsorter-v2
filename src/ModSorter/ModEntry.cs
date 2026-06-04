namespace ModSorter;

public class ModEntry
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ModId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Loader { get; set; } = "";
    public string DisplayName => string.IsNullOrEmpty(ModId) || ModId.StartsWith("(")
        ? FileName : ModId;

    public string ModrinthUrl { get; set; } = "";
    public string CurseForgeUrl { get; set; } = "";
    public string Body { get; set; } = "";        // MODページ本文(原文)
    public string TranslatedBody { get; set; } = ""; // 翻訳後(Day 3後半)

    // 旧コードとの互換用
    public string Url { get; set; } = "";

    //アイコンURLと、本文の形式を追加
    public string IconUrl { get; set; } = "";
    public bool BodyIsHtml { get; set; } = false; // true=HTML(CurseForge), false=Markdown(Modrinth)

    public string Sha1 { get; set; } = "";
    public string IconFile { get; set; } = "";
    // 画像表示用: ローカルファイルがあればそれを、なければURLを使う
    public string IconSource => string.IsNullOrEmpty(IconFile) ? IconUrl : IconFile;
    public string TranslatedHtml { get; set; } = ""; // 翻訳済みHTMLのキャッシュ(セッション内)

    // API由来のカテゴリ(CurseForgeまたはModrinth)
    public List<string> Categories { get; set; } = new();
    // カテゴリの出所表示用 ("CurseForge" または "Modrinth")
    public string CategorySource { get; set; } = "";
    // 詳細パネル等で1行表示するための文字列
    public string CategoryText => Categories.Count == 0
        ? "(未分類)"
        : string.Join(", ", Categories);

    // ソート用のファイル情報
    public long FileSize { get; set; }
    public DateTime FileCreated { get; set; }
    public DateTime FileModified { get; set; }
}
