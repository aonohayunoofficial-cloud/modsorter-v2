using System.Globalization;
using System.Text;

namespace ModSorter.Services;

public static partial class LangPackService
{
    // 厳格な JSON 解析が落ちた lang ファイルを救済する寛容な走査。
    // lang ファイルは "キー": "値" の平坦な集まりなので、文字列トークンを拾って
    // 対にするだけで内容を取り出せる。実機で落ちた原因は次の4種で、いずれも救える。
    //   ・# や // のコメント行が混ざっている       (MoreSnifferFlowers)
    //   ・\' のような JSON では無効なエスケープ    (libertyvillagers)
    //   ・外側の { } が無い                        (naturalsizes)
    //   ・中身がコメントだけでトークンが1つも無い  (better_lib)
    // tokens には見つけたトークン数を返す。0 なら「空」であり壊れてはいない。
    private static Dictionary<string, string> ParseJsonLenient(string text, out int tokens)
    {
        var dict = new Dictionary<string, string>();
        // (文字列か, 文字列の中身, 記号) の並び。記号は : , { } [ ] と
        // それ以外の値(数値・true 等)を表す 'v'。
        var items = new List<(bool IsString, string Text, char Symbol)>();
        int i = 0;
        int n = text.Length;
        tokens = 0;

        while (i < n)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // コメントを読み飛ばす。# と // は行末まで、/* */ は閉じまで。
            if (c == '#' || (c == '/' && i + 1 < n && text[i + 1] == '/'))
            {
                while (i < n && text[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < n && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = Math.Min(n, i + 2);
                continue;
            }

            if (c == '"')
            {
                var s = ReadLenientString(text, ref i);
                items.Add((true, s, '\0'));
                tokens++;
                continue;
            }

            if (c == ':' || c == ',' || c == '{' || c == '}' || c == '[' || c == ']')
            {
                items.Add((false, "", c));
                tokens++;
                i++;
                continue;
            }

            // 数値・true/false/null など。値としては採らないので種類だけ残す。
            int start = i;
            while (i < n && !char.IsWhiteSpace(text[i]) &&
                   text[i] != ',' && text[i] != ':' &&
                   text[i] != '}' && text[i] != ']' && text[i] != '"')
                i++;
            if (i == start) i++;  // 進めない文字はそのまま捨てる
            items.Add((false, "", 'v'));
            tokens++;
        }

        // 「文字列 : 文字列」の並びだけを拾う。値が文字列以外の対は採らない。
        for (int k = 0; k + 2 < items.Count; k++)
        {
            if (!items[k].IsString) continue;
            if (items[k + 1].IsString || items[k + 1].Symbol != ':') continue;
            if (!items[k + 2].IsString) continue;

            var key = items[k].Text;
            if (!string.IsNullOrEmpty(key)) dict[key] = items[k + 2].Text;
            k += 2;
        }

        return dict;
    }

    // 開き " の位置から文字列を1つ読む。
    // JSON では無効なエスケープ(\' 等)は印だけ落として文字を残す。
    // 閉じ引用符が無いまま改行に達したらそこで終わりとみなし、
    // 以降のトークンの対応がずれ続けるのを防ぐ。
    private static string ReadLenientString(string text, ref int i)
    {
        var sb = new StringBuilder();
        int n = text.Length;
        i++; // 開き " を読み飛ばす

        while (i < n)
        {
            char c = text[i];
            if (c == '"') { i++; break; }
            if (c == '\n') { i++; break; }

            if (c == '\\' && i + 1 < n)
            {
                char esc = text[i + 1];
                i += 2;
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 <= n && int.TryParse(
                                text.AsSpan(i, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out var cp))
                        {
                            sb.Append((char)cp);
                            i += 4;
                        }
                        else sb.Append('u');
                        break;
                    default: sb.Append(esc); break;  // \' 等は文字として残す
                }
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }
}
