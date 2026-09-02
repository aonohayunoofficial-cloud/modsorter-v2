namespace ModSorter.Services;

public static partial class LangPackService
{
    // 生成結果サマリ
    public sealed class LangPackResult
    {
        public int ModCount;              // 走査した jar 数
        public int NamespaceCount;        // 翻訳対象になった名前空間数
        public int EntryCount;            // 翻訳対象の総エントリ数(不足キーのみ)
        public int TranslatedChars;       // 実際に翻訳送信した文字数(キャッシュ未ヒット分)
        public int PreservedEntries;      // MOD 同梱 ja_jp から引き継いだエントリ数
        public int PartialNamespaces;     // 同梱 ja_jp があり不足分だけ補った名前空間数
        public int SkippedJaExisting;     // 同梱 ja_jp が完備で除外した名前空間数
        public int SkippedBroken;         // 解析失敗の総件数(jar + lang ファイル)
        public int SkippedBrokenJars;     // うち jar 自体が開けなかった件数
        public int SkippedBrokenEntries;  // うち lang ファイル1件の解析に失敗した件数
        public int NestedJars;            // 走査した同梱 jar(jar-in-jar)の数
        public int RepairedFiles;         // 厳格解析が落ちたが救済して読めた lang ファイル数
        public int EmptyLangFiles;        // 中身がコメント・空白だけだった lang ファイル数
        // lang ファイルを1件も持たなかった jar の数。
        // 開けたが assets/<ns>/lang/ が無い jar は、失敗でも救済でもないため
        // 従来はログに出ず消えていた。ライブラリ jar のほか、表示名を lang キーではなく
        // 独自の定義 JSON に持つ形式(Immersive Vehicles のコンテンツパック等)が該当し、
        // これらはリソースパック方式では翻訳できない。区別できるよう数と名前を残す。
        public int NoLangJars;
        public List<string> NoLangJarNames = new();
        // 解析失敗の内訳。「どのファイルがなぜ落ちたか」を残す。
        // 内訳が無いと、救える失敗(壊れた JSON 等)と救えない失敗の区別がつかない。
        public List<string> SkippedDetails = new();
        // 救済・空判定の内訳。何を直して読めたのかを残す。
        public List<string> RepairDetails = new();
        public int RestoreWarnings;       // プレースホルダ復元漏れ件数
        // 復元漏れした原文の一覧(どの文でトークンが戻せなかったか)。
        // 原文が分かれば後から名前空間/キーを検索でき、再翻訳の対象も絞れる。
        public List<string> RestoreWarningSources = new();
        public List<string> ExcludedNamespaces = new(); // 除外した名前空間一覧
        public string OutputPath = "";    // 出力した zip のパス
        public bool Canceled;

        // DeepL 送信に失敗したバッチ数と、その巻き添えになったエントリ数。
        // 失敗分はキャッシュに書かないため、次回生成で自動的に再送信される。
        public int FailedBatches;
        public int FailedEntries;
        // 最後に発生した API エラー(HTTP コード等)。原因表示用。
        public string LastApiError = "";
        // 連続失敗で翻訳を打ち切ったか(枠切れ/キー不正等の恒久エラー想定)。
        public bool AbortedByApiError;

        // 復元漏れ原文の重複判定用。List.Contains だと件数に応じて O(n^2) になるため、
        // 判定は集合で行い、一覧は表示順を保つため List 側へ積む。
        private readonly HashSet<string> _warnedSources = new(StringComparer.Ordinal);

        internal void NoteRestoreWarningSource(string source)
        {
            if (string.IsNullOrEmpty(source)) return;
            if (_warnedSources.Add(source)) RestoreWarningSources.Add(source);
        }

        // 解析失敗を1件記録する。件数が膨らんでも表示が潰れないよう内訳は上限を設ける。
        internal void NoteSkip(string where, string reason, bool isJar)
        {
            SkippedBroken++;
            if (isJar) SkippedBrokenJars++; else SkippedBrokenEntries++;
            if (SkippedDetails.Count < 300) SkippedDetails.Add($"{where} : {Shorten(reason)}");
        }

        // 厳格解析が落ちたが寛容な走査で読めた1件を記録する。
        // 救済と空判定は同じ一覧へ積むため、行頭の種別はここで書く。
        // 表示側で一律に付けると、キーが無かったファイルまで「救済」と出てしまう。
        internal void NoteRepair(string where, string what)
        {
            RepairedFiles++;
            if (RepairDetails.Count < 300)
                RepairDetails.Add($"救済 {where} : {Shorten(what)}");
        }

        // 中身がコメント・空白だけで翻訳するキーが無かった1件を記録する。
        internal void NoteEmpty(string where)
        {
            EmptyLangFiles++;
            if (RepairDetails.Count < 300)
                RepairDetails.Add($"空   {where} : 中身がコメント・空白のみ(キーなし)");
        }

        // lang ファイルを1件も持たない jar を記録する。
        internal void NoteNoLang(string jarName)
        {
            NoLangJars++;
            if (NoLangJarNames.Count < 300) NoLangJarNames.Add(jarName);
        }

        // 例外メッセージは長いので内訳表示用に丸める。
        private static string Shorten(string s)
            => string.IsNullOrEmpty(s) ? ""
             : (s.Length <= 160 ? s : s.Substring(0, 160) + "…");

        // 抽出フェーズで確定する集計値だけを写す。
        // 抽出結果を使い回したときも、呼び出し側の result に同じ数字が入るようにする。
        internal void CopyExtractCountersFrom(LangPackResult src)
        {
            ModCount = src.ModCount;
            NamespaceCount = src.NamespaceCount;
            EntryCount = src.EntryCount;
            PreservedEntries = src.PreservedEntries;
            PartialNamespaces = src.PartialNamespaces;
            SkippedJaExisting = src.SkippedJaExisting;
            SkippedBroken = src.SkippedBroken;
            SkippedBrokenJars = src.SkippedBrokenJars;
            SkippedBrokenEntries = src.SkippedBrokenEntries;
            NestedJars = src.NestedJars;
            RepairedFiles = src.RepairedFiles;
            EmptyLangFiles = src.EmptyLangFiles;
            NoLangJars = src.NoLangJars;
            NoLangJarNames = new List<string>(src.NoLangJarNames);
            ExcludedNamespaces = new List<string>(src.ExcludedNamespaces);
            SkippedDetails = new List<string>(src.SkippedDetails);
            RepairDetails = new List<string>(src.RepairDetails);
        }
    }
}
