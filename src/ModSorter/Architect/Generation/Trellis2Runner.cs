using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ModSorter.Architect.Generation;

// C# から WSL の TRELLIS.2 (conda 環境 trellis2) を呼び出すラッパー。
// example.py に --input / --output を渡して画像→GLB変換を実行する。
public static class Trellis2Runner
{
    // 実行結果。成功なら Success=true、ログに全出力が入る。
    public sealed class RunResult
    {
        public bool Success;
        public int ExitCode;
        public string Log = "";
        // WSL 側の出力GLBパス(相対 or 絶対)。動作確認では sample.glb。
        public string OutputGlbWsl = "";
    }

    // WSL ディストリ名。環境に合わせる。
    private const string Distro = "Ubuntu-22.04";
    // TRELLIS.2 のプロジェクトディレクトリ(WSL側)。
    private const string ProjectDir = "~/projects/TRELLIS.2";
    // conda 環境名。
    private const string CondaEnv = "trellis2";

    // 画像→GLB変換を実行する。
    //   inputWsl : 入力画像の WSL 側パス (例 "assets/flux_test.png")
    //   outputWsl: 出力GLBの WSL 側パス (例 "sample.glb")
    //   onLog    : 進捗ログを受け取るコールバック(任意)。逐次UIへ流せる。
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

        // WSL 内で実行する bash コマンドを組み立てる。
        // bash -lc では ~/.bashrc が読まれず conda が見つからないため、
        // conda の初期化スクリプト(conda.sh)を明示的に source してから実行する。
        // conda run の --no-capture-output で、出力をためこまず逐次流す。
        // python -u で Python 側の出力バッファリングも無効化し、進捗を即時表示する。
        // --no-video で動画書き出しをスキップし高速化する。
        string innerCmd =
            $"source ~/miniconda3/etc/profile.d/conda.sh && " +
            $"cd {ProjectDir} && " +
            $"conda run -n {CondaEnv} --no-capture-output python -u example.py " +
            $"--input {inputWsl} --output {outputWsl} --no-video";

        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // 引数は ArgumentList で1つずつ渡す(スペース等の取り扱いが安全)。
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(Distro);
        psi.ArgumentList.Add("bash");
        psi.ArgumentList.Add("-lc");
        psi.ArgumentList.Add(innerCmd);

        try
        {
            using var proc = new Process { StartInfo = psi };

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) Emit(e.Data);
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                // TRELLIS.2 は進捗バーを stderr に出すため、エラーとは限らない。
                if (e.Data != null) Emit(e.Data);
            };

            Emit($"[Trellis2Runner] 実行: wsl -d {Distro} bash -lc \"{innerCmd}\"");

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();

            result.ExitCode = proc.ExitCode;
            result.Success = proc.ExitCode == 0;
            result.Log = logBuf.ToString();

            Emit($"[Trellis2Runner] 終了コード={proc.ExitCode}");
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
