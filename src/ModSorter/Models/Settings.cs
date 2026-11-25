using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ModSorter.Models;

public class Settings
{
    public string InstancePath { get; set; } = "";
    // 暗号化済みのキー(Base64)。生のキーはここに保持しない
    public string CurseForgeKeyEnc { get; set; } = "";
    public string DeepLKeyEnc { get; set; } = "";

    // 日本語化パックの翻訳エンジン識別子("deepl" / "azure")。
    // TranslatorFactory.Normalize を通して使う。既存の settings.json にはこの項目が無いが、
    // その場合は既定値 "deepl" のまま読まれるので従来どおり DeepL で動く。
    public string TranslatorEngine { get; set; } = "deepl";

    // Azure AI Translator のキー(暗号化済み Base64)。
    public string AzureTranslatorKeyEnc { get; set; } = "";

    // Azure のリソースのリージョン(例: japaneast)。リソース作成画面で選んだ場所を入れる。
    // グローバルのリソースなら空でよい。空のときは Ocp-Apim-Subscription-Region を送らない。
    public string AzureTranslatorRegion { get; set; } = "";

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModSorter");
    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    // 生のキーを受け取り暗号化して保存用文字列にする
    public static string Encrypt(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var bytes = Encoding.UTF8.GetBytes(plain);
        var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    // 保存文字列を復号して生のキーに戻す
    public static string Decrypt(string enc)
    {
        if (string.IsNullOrEmpty(enc)) return "";
        try
        {
            var bytes = Convert.FromBase64String(enc);
            var dec = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch { return ""; }
    }
}
