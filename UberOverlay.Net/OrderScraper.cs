using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace UberOverlay;

public record OrderData(string State, string? Status = null, string? Eta = null, string? Title = null,
                        string? TrackHref = null, string? Message = null, DateTime UpdatedAt = default);

/// <summary>
/// Lit le suivi Uber Eats via WebView2. Pour économiser la RAM, le navigateur n'existe que
/// le temps d'une lecture (quelques secondes) puis est détruit.
/// </summary>
public class OrderScraper
{
    const string OrdersUrl = "https://www.ubereats.com/orders";
    static readonly string DataFolder = Path.Combine(Settings.Folder, "webview");
    static readonly bool Debug = Environment.GetEnvironmentVariable("UBER_DEBUG") == "1";

    public bool LoginOpen { get; private set; }

    // Script exécuté dans la page Uber Eats. Isolé ici car le DOM d'Uber change souvent.
    const string Extract = """
    (() => {
      const text = (document.body && document.body.innerText) || '';
      const url = location.href;
      if (/login|auth\.uber\.com/i.test(url)) return { state: 'loggedOut', url };
      const noise = /sign up|inscri|become a|devenir|driver|courier|livreur partenaire|download|télécharg|regístra|registrar|conviértete|repartidor|descarg/i;
      const lines = text.split('\n').map(s => s.trim()).filter(l => l && !noise.test(l));
      const isList = location.pathname.replace(/\/+$/, '').endsWith('/orders');
      if (isList) {
        const hs = [...document.querySelectorAll('h1,h2,h3')];
        const hp = hs.find(h => /^(in progress|en cours|en curso)/i.test(h.innerText.trim()));
        const hpast = hs.find(h => /^(past orders|commandes pass|pedidos anteriores|pedidos pasados)/i.test(h.innerText.trim()));
        if (!hp) return { state: 'idle', url };
        const link = [...document.querySelectorAll('a[href*="/orders/"]')].find(a =>
          (hp.compareDocumentPosition(a) & Node.DOCUMENT_POSITION_FOLLOWING) &&
          (!hpast || (hpast.compareDocumentPosition(a) & Node.DOCUMENT_POSITION_PRECEDING)));
        if (!link) return { state: 'idle', url };
        return { state: 'active', trackHref: link.href, url };
      }
      const find = (re) => lines.find(l => re.test(l));
      const status = find(/préparation|prepar|en route|arriv|livré|livraison|delivered|on its way|picked up|récupér|accept|confirm|finding|recherche|preparando|en camino|de camino|llegando|llega|entregado|aceptad|confirmad|buscando|recogid/i);
      const eta = find(/\d+\s*(min|h)\b|\b\d{1,2}[:h]\d{2}\b/i);
      const delivered = /commande livrée|order delivered|has been delivered|a été livrée|pedido entregado|ha sido entregado/i.test(text);
      const title = (document.querySelector('h1,h2') || {}).innerText;
      return { state: delivered ? 'delivered' : 'active', status, eta, title, url, dump: lines.slice(0, 60) };
    })()
    """;

    static OrderData Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        string? S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return new OrderData(S("state") ?? "idle", S("status"), S("eta"), S("title"), S("trackHref"), null, DateTime.Now);
    }

    static WebView2 NewView() => new()
    {
        CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = DataFolder,
            // Navigateur allégé : pas de GPU, un seul process de rendu
            AdditionalBrowserArguments = "--disable-gpu --renderer-process-limit=1 --disable-extensions --disable-background-networking",
        },
    };

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    /// <summary>Rend la mémoire à Windows entre deux lectures.</summary>
    public static void TrimMemory()
    {
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
    }

    static async Task NavigateAsync(WebView2 wv, string url)
    {
        var tcs = new TaskCompletionSource();
        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e) { wv.CoreWebView2.NavigationCompleted -= Handler; tcs.TrySetResult(); }
        wv.CoreWebView2.NavigationCompleted += Handler;
        wv.CoreWebView2.Navigate(url);
        await Task.WhenAny(tcs.Task, Task.Delay(20000));
        await Task.Delay(3000); // laisse la SPA se rendre
    }

    static async Task<OrderData> ExtractAsync(WebView2 wv)
    {
        var json = await wv.CoreWebView2.ExecuteScriptAsync(Extract);
        if (Debug)
        {
            try { File.AppendAllText(Path.Combine(Settings.Folder, "debug.log"), DateTime.Now + " " + json + "\n"); } catch { }
        }
        return Parse(json);
    }

    public async Task<OrderData> PollAsync()
    {
        if (LoginOpen) return new OrderData("loggedOut", UpdatedAt: DateTime.Now);
        // Fenêtre hôte hors écran : WebView2 a besoin d'un HWND visible pour s'initialiser
        var host = new Window
        {
            Width = 1000, Height = 800, Left = -32000, Top = -32000, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        var wv = NewView();
        host.Content = wv;
        try
        {
            host.Show();
            await wv.EnsureCoreWebView2Async();
            await NavigateAsync(wv, OrdersUrl);
            var d = await ExtractAsync(wv);
            if (d.State == "active" && d.TrackHref is { } href && href.Contains("/orders/"))
            {
                await NavigateAsync(wv, href);
                d = await ExtractAsync(wv);
            }
            return d;
        }
        catch (Exception ex)
        {
            return new OrderData("error", Message: ex.Message, UpdatedAt: DateTime.Now);
        }
        finally
        {
            wv.Dispose();
            host.Close();
            host.Content = null;
            await Task.Delay(3000); // laisse les process WebView2 se fermer
            TrimMemory();
        }
    }

    /// <summary>Ouvre la fenêtre de connexion ; se ferme seule une fois connecté.</summary>
    public async Task LoginAsync(Action onLoggedIn, bool switchAccount = false)
    {
        if (LoginOpen) return;
        LoginOpen = true;
        var wv = NewView();
        var win = new Window { Title = Loc.Get("loginTitle"), Width = 480, Height = 760, Content = wv };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) =>
        {
            try
            {
                if (wv.CoreWebView2 == null) return;
                var url = wv.CoreWebView2.Source;
                if (!url.Contains("ubereats.com/orders")) return;
                var d = await ExtractAsync(wv);
                if (d.State != "loggedOut") { timer.Stop(); win.Close(); }
            }
            catch { }
        };
        win.Closed += (_, _) => { timer.Stop(); wv.Dispose(); LoginOpen = false; onLoggedIn(); };
        win.Show();
        try
        {
            await wv.EnsureCoreWebView2Async();
            // Changement de compte : on efface la session (ce profil ne sert qu'à Uber Eats)
            if (switchAccount) await wv.CoreWebView2.Profile.ClearBrowsingDataAsync();
            wv.CoreWebView2.Navigate(OrdersUrl);
            timer.Start();
        }
        catch { win.Close(); }
    }
}
