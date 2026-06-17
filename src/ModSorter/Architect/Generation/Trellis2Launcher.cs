using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ModSorter.Architect.Generation;

// TRELLIS.2 常駐サーバー(trellis_server.py)を必要時に WSL 経由で起動し、
// /health が応答するまで待つ。すでに起動済み(手動含む)ならそれを流用し、二重起動しない。
// モデルロードに時間がかかるため、起動待ちは長め(既定180秒)。
public static class Trellis2Launcher
{
    private const string BaseUrl = "http://127.0.0.1:8189";
    private const string Distro = "Ubuntu-22.04";
    private const string CondaEnv = "trellis2";
    private const string ProjectDir = "~/projects/TRELLIS.2";
    private const string ServerScript = "trellis_server.py";

    // 起動したプロセス。アプリ終了時に止めるため保持する。
    private static Process? _proc;

    // 応答確認用。短いタイムアウトで叩く。
    private static readonly HttpClient _http =
        new() { Timeout = TimeSpan.FromSeconds(3) };

    // サーバーが応答するか確認する。応答すれば true。
    public static async Task<bool> IsRunningAsync()
    {
        try
        {
            // /health に GET して 200 が返れば起動中とみなす。
            using var res = await _http.GetAsync(BaseUrl + "/health");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // 必要なら起動し、応答するまで待つ。
    // onLog: 進捗ログ。timeoutSec: 起動待ちの上限(既定180秒。モデルロードが重いため長め)。
    // 戻り値: 最終的に応答すれば true。
    public static async Task<bool> EnsureRunningAsync(
        Action<string> onLog, int timeoutSec = 180)
    {
        // 1. すでに動いているならそのまま使う。
        if (await IsRunningAsync())
        {
            onLog("TRELLIS.2 サーバーは起動済みです。");
            return true;
        }

        // 2. 起動コマンドを WSL 経由でバックグラウンド実行。
        // ATTN_BACKEND=sdpa: 新ドライバ(610系)と flash_attn 2.7.3 の相性で
        // device not ready が出るため、安定する sdpa を明示指定する。
        onLog("TRELLIS.2 サーバーを起動します...（モデル読み込みに時間がかかります）");
        string innerCmd =
            $"export ATTN_BACKEND=sdpa && " +
            $"source ~/miniconda3/etc/profile.d/conda.sh && " +
            $"conda activate {CondaEnv} && cd {ProjectDir} && " +
            $"python {ServerScript}";

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
            onLog($"TRELLIS.2 サーバーの起動に失敗しました: {ex.Message}");
            return false;
        }

        // 3. 応答するまでポーリング(2秒間隔)。
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSec)
        {
            await Task.Delay(2000);
            if (await IsRunningAsync())
            {
                onLog($"TRELLIS.2 サーバーが応答しました（{sw.Elapsed.TotalSeconds:F0} 秒）。");
                return true;
            }
            onLog($"TRELLIS.2 サーバー起動待ち... ({sw.Elapsed.TotalSeconds:F0}/{timeoutSec} 秒)");
        }

        onLog("TRELLIS.2 サーバーが時間内に応答しませんでした。");
        return false;
    }

    // WSL 内の trellis_server.py を名前で確実に停止する。
    // _proc(wsl.exe ラッパー)の Kill だけでは中の python が残る場合があるため、
    // pkill で確実に落として VRAM とプロセスを解放する。
    public static async Task ForceKillServerAsync(Action<string> onLog)
    {
        // まず保持しているラッパープロセスを止める。
        Stop();

        // WSL 側の python プロセスを名前で kill。
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {Distro} bash -lc \"pkill -9 -f {ServerScript} || true\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p != null)
                await p.WaitForExitAsync();
            onLog("TRELLIS.2 サーバープロセスを停止しました。");
        }
        catch (Exception ex)
        {
            onLog($"TRELLIS.2 サーバー停止時に警告: {ex.Message}");
        }

        // /health が落ちる(応答しなくなる)まで少し待つ。
        for (int i = 0; i < 10; i++)
        {
            if (!await IsRunningAsync())
                break;
            await Task.Delay(500);
        }
    }

    // サーバーを毎回クリーンに作り直してから起動待ちする。
    // 後処理(to_glb)の累積劣化で生成のたびに遅くなるため、
    // 1 推論ごとにプロセスを新品にして 1 回目の速度を維持する。
    public static async Task<bool> RestartAndWaitAsync(
        Action<string> onLog, int timeoutSec = 180)
    {
        onLog("TRELLIS.2 サーバーを再起動します（クリーンな状態で生成するため）。");
        await ForceKillServerAsync(onLog);
        // kill 後は必ず未起動なので EnsureRunningAsync が新規起動を行う。
        return await EnsureRunningAsync(onLog, timeoutSec);
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
