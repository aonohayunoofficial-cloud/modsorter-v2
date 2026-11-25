using ModSorter.Models;

namespace ModSorter.Clients;

// 設定値から翻訳エンジンを作る。キーが未設定でも null は返さず、IsReady=false の
// インスタンスを返す（呼び出し側は IsReady で判定し、LastError を表示に使える）。
// エンジンを足すときは、ここの定数・Engines・Create・HasKey と Settings のキー欄を足す。
public static class TranslatorFactory
{
    public const string DeepL = "deepl";
    public const string Azure = "azure";

    // UI の選択肢（識別子, 表示名）。並び順がそのまま画面の順になる。
    public static readonly IReadOnlyList<(string Id, string Label)> Engines = new[]
    {
        (DeepL, "DeepL"),
        (Azure, "Azure AI Translator"),
    };

    // 未知の値・空は DeepL に倒す。settings.json に項目が無い既存環境でも従来どおり動かすため。
    public static string Normalize(string? engine)
    {
        var e = (engine ?? "").Trim().ToLowerInvariant();
        return e == Azure ? Azure : DeepL;
    }

    public static ITranslator Create(Settings s) => Normalize(s.TranslatorEngine) switch
    {
        Azure => new AzureTranslator(
            Settings.Decrypt(s.AzureTranslatorKeyEnc), s.AzureTranslatorRegion),
        _ => new DeepLTranslator(Settings.Decrypt(s.DeepLKeyEnc)),
    };

    // 指定エンジンのキーが保存済みか（設定画面の「保存済み/未設定」表示用）。
    public static bool HasKey(Settings s, string engine) => Normalize(engine) == Azure
        ? !string.IsNullOrEmpty(s.AzureTranslatorKeyEnc)
        : !string.IsNullOrEmpty(s.DeepLKeyEnc);
}
