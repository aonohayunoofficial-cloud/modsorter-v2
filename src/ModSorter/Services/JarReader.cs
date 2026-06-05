using ModSorter.Models;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModSorter.Services;

public static class JarReader
{
    public static ModEntry Read(string jarPath)
    {
        var entry = new ModEntry
        {
            FileName = Path.GetFileName(jarPath),
            FilePath = jarPath,
            ModId = "",
            Version = "-",
            Loader = "-"
        };

        try
        {
            using var zip = ZipFile.OpenRead(jarPath);

            var neoforge = zip.GetEntry("META-INF/neoforge.mods.toml");
            var forge = zip.GetEntry("META-INF/mods.toml");
            var fabric = zip.GetEntry("fabric.mod.json");
            var quilt = zip.GetEntry("quilt.mod.json");

            if (neoforge != null)
            {
                ReadToml(neoforge, entry);
                entry.Loader = "NeoForge";
            }
            else if (forge != null)
            {
                ReadToml(forge, entry);
                entry.Loader = "Forge";
            }
            else if (fabric != null)
            {
                ReadFabric(fabric, entry);
                entry.Loader = "Fabric";
            }
            else if (quilt != null)
            {
                ReadQuilt(quilt, entry);
                entry.Loader = "Quilt";
            }
            else
            {
                entry.Loader = "不明";
            }
        }
        catch (Exception ex)
        {
            entry.Loader = "読取失敗";
            entry.Version = ex.Message;
        }

        return entry;
    }

    private static string ReadAll(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    // mods.toml / neoforge.mods.toml から modId と version を抜き出す
    private static void ReadToml(ZipArchiveEntry e, ModEntry entry)
    {
        var text = ReadAll(e);

        var idMatch = Regex.Match(text, "modId\\s*=\\s*\"([^\"]+)\"");
        if (idMatch.Success) entry.ModId = idMatch.Groups[1].Value;

        var verMatch = Regex.Match(text, "version\\s*=\\s*\"([^\"]+)\"");
        if (verMatch.Success)
        {
            var v = verMatch.Groups[1].Value;
            // ${file.jarVersion} のようなプレースホルダはそのまま表示
            entry.Version = v;
        }
    }

    private static void ReadFabric(ZipArchiveEntry e, ModEntry entry)
    {
        var text = ReadAll(e);
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("id", out var id))
                entry.ModId = id.GetString() ?? "";
            if (root.TryGetProperty("version", out var ver))
                entry.Version = ver.GetString() ?? "-";
        }
        catch
        {
            // JSONが壊れている場合は正規表現でフォールバック
            var idMatch = Regex.Match(text, "\"id\"\\s*:\\s*\"([^\"]+)\"");
            if (idMatch.Success) entry.ModId = idMatch.Groups[1].Value;
        }
    }

    private static void ReadQuilt(ZipArchiveEntry e, ModEntry entry)
    {
        var text = ReadAll(e);
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            // quilt.mod.json は quilt_loader の中に id/version がある
            if (root.TryGetProperty("quilt_loader", out var ql))
            {
                if (ql.TryGetProperty("id", out var id))
                    entry.ModId = id.GetString() ?? "";
                if (ql.TryGetProperty("version", out var ver))
                    entry.Version = ver.GetString() ?? "-";
            }
        }
        catch
        {
            var idMatch = Regex.Match(text, "\"id\"\\s*:\\s*\"([^\"]+)\"");
            if (idMatch.Success) entry.ModId = idMatch.Groups[1].Value;
        }
    }
}
