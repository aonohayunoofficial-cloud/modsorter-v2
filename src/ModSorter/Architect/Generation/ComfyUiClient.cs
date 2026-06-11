using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModSorter.Architect.Generation;

// ComfyUI(FLUX.2) をHTTP APIで叩き、テキスト→画像を生成するクライアント。
// プロンプトとシードを差し替えて /prompt に投げ、完成画像を取得する。
public static class ComfyUiClient
{
    private const string BaseUrl = "http://127.0.0.1:8188";

    // ワークフローJSONのテンプレート。
    // ノード "76" のプロンプトと "75:73" のシードだけプレースホルダにしてある。
    // {{PROMPT}} と {{SEED}} を実行時に差し替える。
    private const string WorkflowTemplate = """
{
  "9": { "inputs": { "filename_prefix": "Flux2-Klein", "images": ["75:65", 0] }, "class_type": "SaveImage" },
  "76": { "inputs": { "value": "{{PROMPT}}" }, "class_type": "PrimitiveStringMultiline" },
  "75:61": { "inputs": { "sampler_name": "euler" }, "class_type": "KSamplerSelect" },
  "75:62": { "inputs": { "steps": 20, "width": ["75:68", 0], "height": ["75:69", 0] }, "class_type": "Flux2Scheduler" },
  "75:63": { "inputs": { "cfg": 5, "model": ["75:70", 0], "positive": ["75:74", 0], "negative": ["75:67", 0] }, "class_type": "CFGGuider" },
  "75:64": { "inputs": { "noise": ["75:73", 0], "guider": ["75:63", 0], "sampler": ["75:61", 0], "sigmas": ["75:62", 0], "latent_image": ["75:66", 0] }, "class_type": "SamplerCustomAdvanced" },
  "75:65": { "inputs": { "samples": ["75:64", 0], "vae": ["75:72", 0] }, "class_type": "VAEDecode" },
  "75:66": { "inputs": { "width": ["75:68", 0], "height": ["75:69", 0], "batch_size": 1 }, "class_type": "EmptyFlux2LatentImage" },
  "75:67": { "inputs": { "text": "", "clip": ["75:71", 0] }, "class_type": "CLIPTextEncode" },
  "75:68": { "inputs": { "value": 1024 }, "class_type": "PrimitiveInt" },
  "75:69": { "inputs": { "value": 1024 }, "class_type": "PrimitiveInt" },
  "75:73": { "inputs": { "noise_seed": {{SEED}} }, "class_type": "RandomNoise" },
  "75:70": { "inputs": { "unet_name": "flux-2-klein-4b-fp8.safetensors", "weight_dtype": "default" }, "class_type": "UNETLoader" },
  "75:71": { "inputs": { "clip_name": "qwen_3_4b.safetensors", "type": "flux2", "device": "default" }, "class_type": "CLIPLoader" },
  "75:72": { "inputs": { "vae_name": "flux2-vae.safetensors" }, "class_type": "VAELoader" },
  "75:74": { "inputs": { "text": ["76", 0], "clip": ["75:71", 0] }, "class_type": "CLIPTextEncode" }
}
""";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public sealed class GenResult
    {
        public bool Success;
        public string Error = "";
        public byte[]? ImageBytes;   // 取得した画像(PNG)のバイト列
        public string ImageFilename = "";
    }

    // 1枚生成する。prompt=指示, seed=乱数シード, onLog=進捗ログ。
    public static async Task<GenResult> GenerateAsync(
        string prompt, long seed, Action<string>? onLog = null)
    {
        var result = new GenResult();
        try
        {
            // 1. テンプレートにプロンプトとシードを埋め込む。
            //    プロンプトは JSON 文字列として安全にエスケープする。
            string promptJson = JsonSerializer.Serialize(prompt); // 前後に " が付く
            string promptInner = promptJson.Substring(1, promptJson.Length - 2); // " を除いた中身
            string workflow = WorkflowTemplate
                .Replace("{{PROMPT}}", promptInner)
                .Replace("{{SEED}}", seed.ToString());

            // 2. /prompt に投げる本体 { "prompt": <workflow> } を作る。
            string body = "{\"prompt\":" + workflow + "}";
            onLog?.Invoke($"[ComfyUI] 生成依頼 seed={seed}");

            var postContent = new StringContent(body, Encoding.UTF8, "application/json");
            var postResp = await Http.PostAsync($"{BaseUrl}/prompt", postContent);
            string postBody = await postResp.Content.ReadAsStringAsync();
            if (!postResp.IsSuccessStatusCode)
            {
                result.Error = $"/prompt が失敗: {(int)postResp.StatusCode} {postBody}";
                return result;
            }

            // 3. 返ってきた prompt_id を取り出す。
            using var doc = JsonDocument.Parse(postBody);
            string promptId = doc.RootElement.GetProperty("prompt_id").GetString() ?? "";
            if (string.IsNullOrEmpty(promptId))
            {
                result.Error = $"prompt_id が取得できません: {postBody}";
                return result;
            }
            onLog?.Invoke($"[ComfyUI] prompt_id={promptId} 生成待ち...");

            // 4. /history/{promptId} をポーリングして完了を待つ。
            string? imageFilename = null;
            string imageSubfolder = "";
            string imageType = "output";
            for (int i = 0; i < 600; i++) // 最大 600*1s = 10分
            {
                await Task.Delay(1000);
                var histResp = await Http.GetAsync($"{BaseUrl}/history/{promptId}");
                if (!histResp.IsSuccessStatusCode) continue;
                string histBody = await histResp.Content.ReadAsStringAsync();
                using var hist = JsonDocument.Parse(histBody);

                // 履歴に promptId が現れたら完了。outputs から画像を探す。
                if (!hist.RootElement.TryGetProperty(promptId, out var entry)) continue;
                if (!entry.TryGetProperty("outputs", out var outputs)) continue;

                foreach (var node in outputs.EnumerateObject())
                {
                    if (node.Value.TryGetProperty("images", out var images))
                    {
                        foreach (var img in images.EnumerateArray())
                        {
                            imageFilename = img.GetProperty("filename").GetString();
                            imageSubfolder = img.TryGetProperty("subfolder", out var sf)
                                ? (sf.GetString() ?? "") : "";
                            imageType = img.TryGetProperty("type", out var ty)
                                ? (ty.GetString() ?? "output") : "output";
                        }
                    }
                }
                if (imageFilename != null) break;
            }

            if (imageFilename == null)
            {
                result.Error = "生成完了を確認できませんでした(タイムアウト)。";
                return result;
            }
            onLog?.Invoke($"[ComfyUI] 完成画像: {imageFilename}");

            // 5. /view から画像バイト列を取得する。
            string viewUrl =
                $"{BaseUrl}/view?filename={Uri.EscapeDataString(imageFilename)}" +
                $"&subfolder={Uri.EscapeDataString(imageSubfolder)}" +
                $"&type={Uri.EscapeDataString(imageType)}";
            var imgBytes = await Http.GetByteArrayAsync(viewUrl);

            result.Success = true;
            result.ImageBytes = imgBytes;
            result.ImageFilename = imageFilename;
            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"{ex.GetType().Name}: {ex.Message}";
            return result;
        }
    }
}
