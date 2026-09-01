using System.Text.RegularExpressions;

namespace ModSorter.Services;

// ja_jp を持たない MOD の en_us を翻訳し、1つの日本語リソースパック(zip)を生成する。
// jar 内の実ディレクトリ assets/<ns>/lang/ を走査するため、宣言 modid に依存しない。
//
// 1ファイル9KB以下の規約に合わせ、機能単位で partial へ分割している。
//   LangPackService.cs               共通の型・退避対象の定義（このファイル）
//   LangPackService.Result.cs        生成結果サマリ LangPackResult
//   LangPackService.Extract.cs       jar 走査 ExtractTargets / ScanArchive
//   LangPackService.Extract.Diff.cs  名前空間ごとの差分判定
//   LangPackService.Extract.Cache.cs 抽出結果の再利用キャッシュ
//   LangPackService.Parse.cs         lang ファイルの読み取りと解析
//   LangPackService.Protect.cs       プレースホルダ・用語の退避と復元
//   LangPackService.Translate.cs     見積もりと翻訳
//   LangPackService.Retranslate.cs   対象を絞った再翻訳
//   LangPackService.Repair.cs        枠を使わない再検査・修復・キャッシュ掃除
//   LangPackService.Pack.cs          zip 生成
public static partial class LangPackService
{
    // 進捗通知用コールバック(現在値, 総数, メッセージ)。UI 側で受ける。
    public delegate void ProgressHandler(int done, int total, string message);

    // 翻訳させたくない断片。printf 系・MessageFormat 系・書式コードを拾う。
    // %02d や %.2f のような幅・精度付きも取りこぼさない。
    // 記号や空白を挟む形は拾わない("50% each" の "% e" を誤検出しないため)。
    private static readonly Regex PlaceholderRegex = new(
        @"%%" +                            // リテラルの %
        @"|%(\d+\$)?\d*(\.\d+)?[sdf]" +    // %s %d %1$s %02d %.2f
        @"|\{\d+\}" +                      // {0} {1}
        @"|§.",                            // §a 等の書式コード
        RegexOptions.Compiled);

    // 抽出した1名前空間分のデータ
    public sealed class NamespaceLang
    {
        public string Namespace = "";
        // キー -> 原文(en_us)。名前空間内の en_us 全キーを入力順で保持する。
        // 出力する ja_jp.json のキー順もこの順に合わせる。
        public Dictionary<string, string> Entries = new();
        // MOD 同梱の ja_jp に既に入っている訳。キー -> 訳文。
        // リソースパックは同名の lang ファイルを丸ごと置き換えるため、
        // ここを出力にそのまま載せ直さないと同梱の日本語が英語に戻る。
        public Dictionary<string, string> Existing = new();
        // Entries のうち翻訳が必要なキー。
        // 同梱 ja_jp に無いキーと、値が英語のまま残っているキーだけが入る。
        public HashSet<string> TranslateKeys = new();
        // 抽出元(複数 jar にまたがる場合の記録用)
        public List<string> SourceJars = new();

        // 翻訳が必要なキーと原文だけを列挙する。見積もり・翻訳・再検査はこれを使う。
        public IEnumerable<KeyValuePair<string, string>> TranslationTargets()
        {
            foreach (var kv in Entries)
                if (TranslateKeys.Contains(kv.Key)) yield return kv;
        }
    }
}
