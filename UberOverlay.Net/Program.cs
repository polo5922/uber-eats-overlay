using System.Windows;

namespace UberOverlay;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(true, "UberOverlay.SingleInstance", out bool created);
        if (!created) return;

        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var overlay = new OverlayWindow();
        overlay.Start();
        app.Run();
    }
}
