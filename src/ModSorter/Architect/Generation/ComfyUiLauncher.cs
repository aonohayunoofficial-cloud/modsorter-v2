using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ModSorter.Architect.Generation;

// ComfyUI を必要時に WSL 経由で起動し、API が応答するまで待つ。
// すでに起動済み(手動含む)ならそれを流用し、二重起動しない。
public static class ComfyUiLauncher
{
    private const string BaseUrl = "http://127.0.0.1:8188";
    private const string Distro = "Ubuntu-22.04";
    private const string CondaEnv = "comfyui";
    private const string ProjectDir = "~/projects/ComfyUI";

    // 起動したプロセス。アプリ終了時に止めるため保持する。
    private static Process? _proc;

    // 応答確認用。短いタイムアウトで叩く。
    private static readonly HttpClient _http =
        new() { Timeout = TimeSpan.FromSeconds(3) };

    // ComfyUI が応答するか確認する。応答すれば true。
    public static async Task<bool> IsRunningAsync()
    {
        try
        {
            // ルートに GET して何か返ってくれば起動中とみなす。
            using var res = await _http.GetAsync(BaseUrl + "/");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // 必要なら起動し、応答するまで待つ。
    // onLog: 進捗ログ。timeoutSec: 起動待ちの上限(既定120秒)。
    // 戻り値: 最終的に応答すれば true。
    public static async Task<bool> EnsureRunningAsync(
        Action<string> onLog, int timeoutSec = 120)
    {
        // 1. すでに動いているならそのまま使う。
        if (await IsRunningAsync())
        {
            onLog("ComfyUI は起動済みです。");
            return true;
        }

        // 2. 起動コマンドを WSL 経由でバックグラウンド実行。
        onLog("ComfyUI を起動します...");
        string innerCmd =
            $"source ~/miniconda3/etc/profile.d/conda.sh && " +
            $"conda activate {CondaEnv} && cd {ProjectDir} && " +
            $"python main.py";

        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            Arguments = $"-d {Distro} bash -lc \"{innerCmd}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            _proc = Process.Start(psi);
        }
        catch (Exception ex)
        {
            onLog($"ComfyUI の起動に失敗しました: {ex.Message}");
            return false;
        }

        // 3. 応答するまでポーリング(2秒間隔)。
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSec)
        {
            await Task.Delay(2000);
            if (await IsRunningAsync())
            {
                onLog($"ComfyUI が応答しました（{sw.Elapsed.TotalSeconds:F0} 秒）。");
                return true;
            }
            onLog($"ComfyUI 起動待ち... ({sw.Elapsed.TotalSeconds:F0}/{timeoutSec} 秒)");
        }

        onLog("ComfyUI が時間内に応答しませんでした。");
        return false;
    }

    // アプリ終了時などに呼ぶ。起動したプロセスを止める。
    // (手動起動など、こちらが起動していない場合は何もしない)
    public static void Stop()
    {
        try
        {
            if (_proc is { HasExited: false })
            {
                _proc.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 既に終了済みなどは無視。
        }
        finally
        {
            _proc = null;
        }
    }
}
