using System;
using System.Threading.Tasks;
using System.Windows;

namespace ModSorter.Architect.Preview;

public partial class PreviewWindow : Window
{
    private bool _ready = false;

    public PreviewWindow()
    {
        InitializeComponent();
    }

    // WebView2 を初期化し、Three.js入りHTMLを読み込む
    public async Task InitAsync()
    {
        try
        {
            await PreviewWeb.EnsureCoreWebView2Async();
            PreviewWeb.NavigateToString(PreviewHtml.Build());
            _ready = true;
        }
        catch (Exception)
        {
            _ready = false;
        }
    }

    // ブロックJSON(文字列)を渡して描画。renderBlocks(json) を呼ぶ。
    public async Task RenderAsync(string blocksJson)
    {
        if (!_ready) return;
        try
        {
            string jsArg = System.Text.Json.JsonSerializer.Serialize(blocksJson);
            await PreviewWeb.ExecuteScriptAsync($"renderBlocks({jsArg})");
        }
        catch (Exception)
        {
            // 描画失敗は無視（ウィンドウが閉じられた直後など）
        }
    }

    public bool IsReady => _ready;
}
