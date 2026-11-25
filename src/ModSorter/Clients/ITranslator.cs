namespace ModSorter.Clients;

// 日本語化パックの翻訳エンジン共通口。
// 入出力の取り決め: TranslateBatchXmlAsync へ渡す文字列は LangPackService.ProtectXml の
// 出力そのもの（退避断片は <x id="n"/>、本文の < > & はエスケープ済み）。
// 戻り値も同じ形（<x id="n"/> が残った訳文）で返す。エンジン固有の表現への
// 載せ替えと戻しは各実装の内側で閉じ、RestoreXml は全エンジン共通のまま使う。
public interface ITranslator
{
    // エンジン識別子。TranslationCache のファイル名にも使う（"deepl" / "azure"）。
    string Name { get; }

    // 画面表示用の名前。
    string DisplayName { get; }

    // キーが設定され、送信できる状態か。
    bool IsReady { get; }

    // 直近の失敗内容。
    string LastError { get; }

    // 直近の失敗が、待っても回復しない種類か（枠切れ・認証失敗）。
    // true のとき呼び出し側は再送せず打ち切る。DeepL の 456、Azure の 403001 など。
    // 失敗直後にだけ意味を持つ。成功すると false に戻る。
    bool LastErrorIsFatal { get; }

    // キーで消費量を取得できるエンジンか。false なら GetUsageAsync は常に null。
    bool SupportsUsage { get; }

    // 宛先とキー末尾4文字。宛先の取り違えや古いキーの残りを切り分ける表示に使う。
    string Endpoint { get; }
    string KeyTail { get; }

    // 入力順と同じ順序で返す。失敗時は null（理由は LastError）。
    Task<List<string>?> TranslateBatchXmlAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default);

    // (使用済み, 上限)。取得できない・非対応なら null。
    Task<(long Count, long Limit)?> GetUsageAsync();
}
