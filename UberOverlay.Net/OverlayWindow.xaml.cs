using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace UberOverlay;

public partial class OverlayWindow : Window
{
    const int WM_HOTKEY = 0x0312, GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    const uint MOD_CONTROL = 0x2, MOD_ALT = 0x1;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong(IntPtr h, int i, int v);

    readonly Settings settings = Settings.Load();
    readonly OrderScraper scraper = new();
    readonly Forms.NotifyIcon tray = new();
    Forms.ToolStripMenuItem? ghostItem;
    readonly DispatcherTimer rerender = new() { Interval = TimeSpan.FromSeconds(15) };
    IntPtr hwnd;
    OrderData? last;
    string? lastKey;
    bool? wantVisible;
    DispatcherTimer? hideTimer;
    CancellationTokenSource? wake;

    public OverlayWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = settings.X ?? SystemParameters.WorkArea.Right - Width - 20;
        Top = settings.Y ?? SystemParameters.WorkArea.Top + 40;
        ApplyOpacity();
        OpacitySlider.Value = settings.Opacity * 100;

        // HWND créé sans afficher la fenêtre : raccourcis et styles dispo dès le départ
        hwnd = new WindowInteropHelper(this).EnsureHandle();
        HwndSource.FromHwnd(hwnd)!.AddHook(WndProc);
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        RegisterHotKey(hwnd, 1, MOD_CONTROL | MOD_ALT, 'U');
        RegisterHotKey(hwnd, 2, MOD_CONTROL | MOD_ALT, 'T');
        ApplyClickThrough();

        InitTray();
        ApplyLanguage();
        rerender.Tick += (_, _) => Render();
        rerender.Start();
    }

    // Applique la langue choisie (ou celle de Windows) à toute l'interface
    void ApplyLanguage()
    {
        Loc.Apply(settings.Language);
        RefreshBtn.ToolTip = Loc.Get("tipRefresh");
        LoginBtn.ToolTip = Loc.Get(Connected ? "tipSwitch" : "tipLogin");
        GhostBtn.ToolTip = Loc.Get("tipGhost");
        QuitBtn.ToolTip = Loc.Get("tipQuit");
        OpacitySlider.ToolTip = OpacityIcon.ToolTip = Loc.Get("tipOpacity");
        BuildMenu();
        Render();
    }

    public void Start() => _ = LoopAsync();

    // ---- Boucle de lecture ----
    async Task LoopAsync()
    {
        while (true)
        {
            var d = await scraper.PollAsync();
            if (d.UpdatedAt == default) d = d with { UpdatedAt = DateTime.Now };
            OnData(d);
            // Plus rare quand il n'y a pas de commande
            var delay = d.State == "active" ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(60);
            wake = new CancellationTokenSource();
            try { await Task.Delay(delay, wake.Token); } catch (TaskCanceledException) { }
        }
    }

    void OnData(OrderData d)
    {
        var key = d.State + "|" + d.Status;
        if (lastKey != null && key != lastKey && (d.State == "active" || d.State == "delivered"))
            tray.ShowBalloonTip(5000, "Uber Eats", d.State == "delivered" ? Loc.Get("balloonDelivered") : d.Status ?? Loc.Get("balloonUpdated"), Forms.ToolTipIcon.Info);
        lastKey = key;
        bool wasConnected = Connected;
        last = d;
        if (Connected != wasConnected) ApplyLanguage(); // met à jour le libellé du bouton et du menu
        else Render();
        AutoVisibility(d);
    }

    // Visible seulement pendant une commande (ou si connexion requise)
    void AutoVisibility(OrderData d)
    {
        hideTimer?.Stop();
        bool want = d.State is "active" or "loggedOut";
        if (d.State == "delivered")
        {
            want = true;
            hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            hideTimer.Tick += (_, _) => { hideTimer.Stop(); wantVisible = false; Hide(); };
            hideTimer.Start();
        }
        if (want == wantVisible) return; // respecte un affichage/masquage manuel
        wantVisible = want;
        if (want) Show(); else Hide();
    }

    // ---- Rendu ----
    // Format d'heure : réglage, ou celui de Windows en mode automatique
    bool Use24h => settings.TimeFormat switch
    {
        "24" => true,
        "12" => false,
        _ => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H'),
    };

    string Hhmm(DateTime t) => t.ToString(Use24h ? "HH:mm" : "h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    static int ProgressOf(OrderData d)
    {
        var s = (d.Status ?? "").ToLowerInvariant();
        if (d.State == "delivered") return 100;
        if (Regex.IsMatch(s, "arriv|proche|nearby|llegando|llega")) return 90;
        if (Regex.IsMatch(s, "en route|on its way|picked up|récupér|en camino|de camino|recogid")) return 70;
        if (Regex.IsMatch(s, "préparation|prepar")) return 40;
        if (Regex.IsMatch(s, "accept|confirm|aceptad|confirmad")) return 20;
        return 10;
    }

    // "7:45 PM", "19:45", "19h45" -> heure ; sinon durée telle quelle ("10-15 min")
    static (DateTime? at, string? text) ParseEta(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null);
        var m = Regex.Match(raw, @"(\d{1,2})\s*[:h]\s*(\d{2})\s*(am|pm)?", RegexOptions.IgnoreCase);
        if (!m.Success) return (null, raw.Trim());
        int h = int.Parse(m.Groups[1].Value), min = int.Parse(m.Groups[2].Value);
        var ap = m.Groups[3].Value.ToLowerInvariant();
        if (ap == "pm" && h < 12) h += 12;
        if (ap == "am" && h == 12) h = 0;
        if (h > 23 || min > 59) return (null, raw.Trim());
        var at = DateTime.Today.AddHours(h).AddMinutes(min);
        if (at < DateTime.Now.AddHours(-6)) at = at.AddDays(1);
        return (at, null);
    }

    static string Remaining(DateTime at)
    {
        int min = (int)Math.Ceiling((at - DateTime.Now).TotalMinutes);
        if (min <= 0) return Loc.Get("imminent");
        if (min < 60) return string.Format(Loc.Get("minLeft"), min);
        return string.Format(Loc.Get("hmLeft"), min / 60, min % 60);
    }

    void Render()
    {
        var d = last;
        if (d == null) { StatusText.Text = Loc.Get("loading"); return; }
        string status = "", eta = "", sub = "";
        int pct = 0;
        switch (d.State)
        {
            case "loggedOut": status = Loc.Get("loggedOut"); sub = Loc.Get("loggedOutSub"); break;
            case "idle": status = Loc.Get("idle"); sub = Loc.Get("idleSub"); break;
            case "delivered": status = Loc.Get("delivered"); pct = 100; break;
            case "error": status = Loc.Get("error"); sub = d.Message ?? ""; break;
            default:
                status = d.Status ?? Loc.Get("orderDefault");
                pct = ProgressOf(d);
                var (at, text) = ParseEta(d.Eta);
                if (at is { } a) { eta = Remaining(a); sub = string.Format(Loc.Get("arrival"), Hhmm(a)); }
                else eta = text ?? "";
                if (!string.IsNullOrEmpty(d.Title)) sub = sub.Length > 0 ? $"{sub} · {d.Title}" : d.Title;
                break;
        }
        Dot.Fill = new SolidColorBrush(d.State switch
        {
            "active" or "delivered" => System.Windows.Media.Color.FromRgb(6, 193, 103),
            "loggedOut" => System.Windows.Media.Color.FromRgb(255, 170, 0),
            "error" => System.Windows.Media.Color.FromRgb(240, 80, 80),
            _ => System.Windows.Media.Color.FromRgb(140, 140, 140),
        });
        StatusText.Text = status;
        EtaText.Text = eta;
        SubText.Text = sub;
        FillCol.Width = new GridLength(pct, GridUnitType.Star);
        RestCol.Width = new GridLength(100 - pct, GridUnitType.Star);
        UpdatedText.Text = Loc.Get("updated") + " " + Hhmm(d.UpdatedAt);
    }

    // ---- Interactions ----
    void Bar_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        settings.X = Left; settings.Y = Top; settings.Save();
    }

    void Opacity_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Card == null || PctText == null) return;
        settings.Opacity = e.NewValue / 100;
        ApplyOpacity();
        PctText.Text = $"{(int)Math.Round(e.NewValue)}%";
        settings.Save();
    }

    // La transparence ne touche que le fond de la carte : le texte reste net
    void ApplyOpacity()
    {
        Card.Background.Opacity = settings.Opacity;
        Card.BorderBrush.Opacity = settings.Opacity;
    }

    void Refresh_Click(object s, RoutedEventArgs e) => wake?.Cancel();
    void Login_Click(object s, RoutedEventArgs e) => OpenLogin();
    void Ghost_Click(object s, RoutedEventArgs e) => SetClickThrough(true);
    void Quit_Click(object s, RoutedEventArgs e) => Quit();

    // Connecté = la dernière lecture a réussi à lire le compte (même sans commande en cours)
    bool Connected => last?.State is "active" or "idle" or "delivered";

    void OpenLogin() => _ = scraper.LoginAsync(() => wake?.Cancel(), switchAccount: Connected);

    void SetClickThrough(bool on)
    {
        settings.ClickThrough = on; settings.Save();
        if (ghostItem != null) ghostItem.Checked = on;
        ApplyClickThrough();
    }

    void ApplyClickThrough()
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, settings.ClickThrough ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT);
        GhostBtn.Background = settings.ClickThrough ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 193, 103)) : System.Windows.Media.Brushes.Transparent;
    }

    void ToggleVisible() { if (IsVisible) Hide(); else Show(); }

    void Quit()
    {
        UnregisterHotKey(hwnd, 1); UnregisterHotKey(hwnd, 2);
        tray.Visible = false; tray.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            if (wParam.ToInt32() == 1) ToggleVisible();
            else if (wParam.ToInt32() == 2) SetClickThrough(!settings.ClickThrough);
            handled = true;
        }
        return IntPtr.Zero;
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    static bool StartupEnabled()
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("UberOverlay") != null;
    }

    static void SetStartup(bool on)
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("UberOverlay", $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue("UberOverlay", false);
    }

    void InitTray()
    {
        tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        tray.Text = "Uber Eats Overlay";
        tray.Visible = true;
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ToggleVisible(); };
    }

    // Sous-menu à choix unique (la valeur courante est cochée)
    Forms.ToolStripMenuItem ChoiceMenu(string title, (string value, string label)[] choices, string current, Action<string> set)
    {
        var parent = new Forms.ToolStripMenuItem(title);
        foreach (var (value, label) in choices)
        {
            var item = new Forms.ToolStripMenuItem(label) { Checked = value == current };
            item.Click += (_, _) => { set(value); settings.Save(); ApplyLanguage(); };
            parent.DropDownItems.Add(item);
        }
        return parent;
    }

    // (Re)construit le menu de l'icône, dans la langue courante
    void BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Loc.Get("mToggle"), null, (_, _) => ToggleVisible());
        menu.Items.Add(Loc.Get(Connected ? "mSwitch" : "mLogin"), null, (_, _) => OpenLogin());
        menu.Items.Add(Loc.Get("mRefresh"), null, (_, _) => wake?.Cancel());
        ghostItem = new Forms.ToolStripMenuItem(Loc.Get("mGhost")) { CheckOnClick = true, Checked = settings.ClickThrough };
        ghostItem.Click += (_, _) => SetClickThrough(ghostItem.Checked);
        menu.Items.Add(ghostItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var langs = new List<(string, string)> { ("auto", Loc.Get("mAuto")) };
        langs.AddRange(Loc.Languages.Select(l => (l.Code, l.Name)));
        menu.Items.Add(ChoiceMenu(Loc.Get("mLanguage"), langs.ToArray(), settings.Language, v => settings.Language = v));
        menu.Items.Add(ChoiceMenu(Loc.Get("mTimeFormat"),
            new[] { ("auto", Loc.Get("mAuto")), ("24", Loc.Get("m24")), ("12", Loc.Get("m12")) },
            settings.TimeFormat, v => settings.TimeFormat = v));

        var startupItem = new Forms.ToolStripMenuItem(Loc.Get("mStartup")) { CheckOnClick = true, Checked = StartupEnabled() };
        startupItem.Click += (_, _) => SetStartup(startupItem.Checked);
        menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.Get("mQuit"), null, (_, _) => Quit());

        var old = tray.ContextMenuStrip;
        tray.ContextMenuStrip = menu;
        old?.Dispose();
    }
}


