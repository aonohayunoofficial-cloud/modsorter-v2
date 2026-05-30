using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ModSorter;

public class Settings
{
    public string InstancePath { get; set; } = "";
    // 暗号化済みのキー(Base64)。生のキーはここに保持しない
    public string CurseForgeKeyEnc { get; set; } = "";
    public string DeepLKeyEnc { get; set; } = "";

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
