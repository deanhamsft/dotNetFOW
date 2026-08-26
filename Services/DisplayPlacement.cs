using System.Runtime.InteropServices;
using System.Windows;

namespace rpgFogOfWar.Services;

public readonly record struct DisplayInfo(
    string Name,
    bool IsPrimary,
    double DipLeft,
    double DipTop,
    double DipWidth,
    double DipHeight,
    double DipWorkLeft,
    double DipWorkTop,
    double DipWorkWidth,
    double DipWorkHeight);

public static class DisplayPlacement
{
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var list = new List<DisplayInfo>();
        MonitorEnumProc proc = (hMon, hdc, lprc, data) =>
        {
            var info = new MONITORINFOEX();
            info.cbSize = Marshal.SizeOf<MONITORINFOEX>();
            if (!GetMonitorInfo(hMon, ref info))
                return true;

            uint dpiX = 96;
            uint dpiY = 96;
            if (GetDpiForMonitor(hMon, MdEffectiveDpi, out var x, out var y) == 0)
            {
                dpiX = x == 0 ? 96 : x;
                dpiY = y == 0 ? 96 : y;
            }

            double sx = 96.0 / dpiX;
            double sy = 96.0 / dpiY;
            var r = info.rcMonitor;
            var w = info.rcWork;
            bool primary = (info.dwFlags & MonitorinfofPrimary) != 0;
            string name = string.IsNullOrWhiteSpace(info.szDevice) ? "Display" : info.szDevice.Trim();

            list.Add(new DisplayInfo(
                name,
                primary,
                r.Left * sx,
                r.Top * sy,
                (r.Right - r.Left) * sx,
                (r.Bottom - r.Top) * sy,
                w.Left * sx,
                w.Top * sy,
                (w.Right - w.Left) * sx,
                (w.Bottom - w.Top) * sy));
            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        GC.KeepAlive(proc);

        if (list.Count == 0)
        {
            list.Add(new DisplayInfo(
                "Display",
                true,
                0, 0, 1280, 720,
                0, 0, 1280, 680));
        }

        return list;
    }

    public static DisplayInfo? Primary()
    {
        var displays = GetDisplays();
        foreach (var display in displays)
        {
            if (display.IsPrimary)
                return display;
        }

        return displays.Count > 0 ? displays[0] : null;
    }

    public static void PlaceControl(Window window)
    {
        var primary = Primary();
        if (primary == null)
            return;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = primary.Value.DipWorkLeft + 40;
        window.Top = primary.Value.DipWorkTop + 40;
    }

    public static void PlaceFullscreen(Window window, DisplayInfo display)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStyle = WindowStyle.None;
        window.Topmost = true;
        window.ShowInTaskbar = false;
        window.Left = display.DipLeft;
        window.Top = display.DipTop;
        window.Width = Math.Max(display.DipWidth, 1);
        window.Height = Math.Max(display.DipHeight, 1);
    }

    public static void PlaceWindowed(Window window)
    {
        var primary = Primary();
        window.WindowState = WindowState.Normal;
        window.ResizeMode = ResizeMode.CanResize;
        window.WindowStyle = WindowStyle.SingleBorderWindow;
        window.Topmost = false;
        window.ShowInTaskbar = true;
        window.Width = 960;
        window.Height = 540;
        if (primary != null)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = primary.Value.DipWorkLeft + Math.Max(primary.Value.DipWorkWidth - 1000, 40);
            window.Top = primary.Value.DipWorkTop + 80;
        }
    }

    private const int MdEffectiveDpi = 0;
    private const uint MonitorinfofPrimary = 1;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
}
