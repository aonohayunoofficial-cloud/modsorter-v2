using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using ModSorter.Architect.Generation;

// C# から TRELLIS.2 常駐サーバー(trellis_server.py)を呼び出すラッパー。
// サーバーが起きていなければ Trellis2Launcher で自動起動し、
// /generate に画像パスと出力GLB名を渡して 3D 化を実行する。
// 署名は従来(RunAsync(inputWsl, outputWsl, onLog))のまま。呼び出し側は変更不要。
public static class Trellis2Runner
{
    // 実行結果。成功なら Success=true、ログに全出力が入る。
    public sealed class RunResult
    {
        public bool Success;
        public int ExitCode;       // 0=成功、それ以外=失敗(互換のため残す)
        public string Log = "";
        public string OutputGlbWsl = "";
    }

    private const string BaseUrl = "http://127.0.0.1:8189";

    // 生成は重い(数分〜十数分)ので、生成用 HttpClient は長めのタイムアウトにする。
    // 再起動方式で毎回クリーンにしても、重いメッシュ+モデルロードで10分を超える
    // ことがあるため余裕を持たせる。
    private static readonly HttpClient _http =
        new() { Timeout = TimeSpan.FromMinutes(20) };

    // 画像→GLB変換を実行する。
    //   inputWsl : 入力画像の WSL 側相対パス (例 "assets/arch_xxx.png")
    //   outputWsl: 出力GLBの WSL 側相対パス (例 "arch_case0.glb")
    //   onLog    : 進捗ログを受け取るコールバック(任意)。
    public static async Task<RunResult> RunAsync(
        string inputWsl, string outputWsl, Action<string>? onLog = null)
    {
        var result = new RunResult { OutputGlbWsl = outputWsl };
        var logBuf = new StringBuilder();

        void Emit(string line)
        {
            logBuf.AppendLine(line);
            onLog?.Invoke(line);
        }

        // 1. サーバーを毎回クリーンに再起動してから生成する。
        //    後処理(to_glb)の累積劣化で生成のたびに遅くなるため、
        //    1 推論ごとにプロセスを作り直して 1 回目の速度を維持する。
        bool ready = await Trellis2Launcher.RestartAndWaitAsync(Emit);
        if (!ready)
        {
            Emit("[Trellis2Runner] サーバーが利用できません。");
            result.Success = false;
            result.ExitCode = -1;
            result.Log = logBuf.ToString();
            return result;
        }

        // 2. /generate を叩いて 3D 化を依頼する。
        string url = BaseUrl + "/generate"
            + "?input=" + HttpUtility.UrlEncode(inputWsl)
            + "&output=" + HttpUtility.UrlEncode(outputWsl);

        Emit($"[Trellis2Runner] 3D化を依頼: {inputWsl} -> {outputWsl}");
        try
        {
            using var res = await _http.GetAsync(url);
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Emit($"[Trellis2Runner] サーバーエラー (HTTP {(int)res.StatusCode}): {body}");
                result.Success = false;
                result.ExitCode = (int)res.StatusCode;
                result.Log = logBuf.ToString();
                return result;
            }

            // 応答 JSON から所要秒数を取り出してログに出す(任意)。
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("seconds", out var sec))
                    Emit($"[Trellis2Runner] 完了（{sec.GetDouble():F1} 秒）。");
                else
                    Emit("[Trellis2Runner] 完了。");
            }
            catch
            {
                Emit("[Trellis2Runner] 完了。");
            }

            result.Success = true;
            result.ExitCode = 0;
            result.Log = logBuf.ToString();
            return result;
        }
        catch (TaskCanceledException)
        {
            Emit("[Trellis2Runner] タイムアウト(20分)しました。");
            result.Success = false;
            result.ExitCode = -1;
            result.Log = logBuf.ToString();
            return result;
        }
        catch (Exception ex)
        {
            Emit($"[Trellis2Runner] 例外: {ex.GetType().Name}: {ex.Message}");
            result.Success = false;
            result.ExitCode = -1;
            result.Log = logBuf.ToString();
            return result;
        }
    }
}
 