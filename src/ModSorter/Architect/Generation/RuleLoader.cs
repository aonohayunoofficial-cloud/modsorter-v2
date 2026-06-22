using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ModSorter.Architect.Generation;

public static class RuleLoader
{
    private const string RulesDirName = "Architect/Rules";

    public static string LoadAllRules()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Architect", "Rules");
            if (!Directory.Exists(dir))
                return "";

            var files = Directory
                .EnumerateFiles(dir, "*.txt", SearchOption.TopDirectoryOnly)
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                .ToList();

            if (files.Count == 0)
                return "";

            var sb = new StringBuilder();
            foreach (var f in files)
            {
                var text = File.ReadAllText(f);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                sb.AppendLine(text.TrimEnd());
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
        catch
        {
            return "";
        }
    }
}
