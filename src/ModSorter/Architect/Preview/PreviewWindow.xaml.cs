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

    // WebView2 を初期化し、Three.js入りHTMLを読み込む。
    // NavigateToString は読み込み開始のみで完了を待たないため、
    // NavigationCompleted イベントを待ってから _ready を立てる。
    public async Task InitAsync()
    {
        try
        {
            await PreviewWeb.EnsureCoreWebView2Async();

            // ナビゲーション完了を待つための仕掛け。
            var navDone = new TaskCompletionSource<bool>();
            void Handler(object? s,
                Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
            {
                // 一度だけ拾えばよいのでハンドラを外す。
                PreviewWeb.NavigationCompleted -= Handler;
                navDone.TrySetResult(e.IsSuccess);
            }
            PreviewWeb.NavigationCompleted += Handler;

            PreviewWeb.NavigateToString(PreviewHtml.Build());

            // ページ読み込み完了を待つ（最大10秒のタイムアウト保険つき）。
            var completed = await Task.WhenAny(navDone.Task, Task.Delay(10000));
            if (completed == navDone.Task && navDone.Task.Result)
            {
                _ready = true;
            }
            else
            {
                // タイムアウト or 失敗。念のため外しておく。
                PreviewWeb.NavigationCompleted -= Handler;
                _ready = false;
            }
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

    // ブロックID→テクスチャ(データURI)の辞書を渡す。renderBlocks より前に呼ぶ。
    public async Task SetTexturesAsync(string texturesJson)
    {
        if (!_ready) return;
        try
        {
            string jsArg = System.Text.Json.JsonSerializer.Serialize(texturesJson);
            await PreviewWeb.ExecuteScriptAsync($"setTextures({jsArg})");
        }
        catch (Exception)
        {
            // 失敗は無視（ウィンドウが閉じられた直後など）
        }
    }

    public bool IsReady => _ready;
}
