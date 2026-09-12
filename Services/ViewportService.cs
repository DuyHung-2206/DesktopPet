using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Microsoft.Win32;

namespace DesktopPet.Services
{
    public class ViewportBounds
    {
        // WPF Device-Independent Pixels (DIPs, 96 units per inch)
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Right => Left + Width;
        public double Bottom => Top + Height;

        // Monitor DPI scaling factors
        public double DpiScaleX { get; set; } = 1.0;
        public double DpiScaleY { get; set; } = 1.0;
        public uint DpiX { get; set; } = 96;
        public uint DpiY { get; set; } = 96;

        // Win32 Physical Device Pixels
        public int DeviceLeft { get; set; }
        public int DeviceTop { get; set; }
        public int DeviceWidth { get; set; }
        public int DeviceHeight { get; set; }
        public int DeviceRight => DeviceLeft + DeviceWidth;
        public int DeviceBottom => DeviceTop + DeviceHeight;
        public Rectangle DeviceWorkingArea => new Rectangle(DeviceLeft, DeviceTop, DeviceWidth, DeviceHeight);
        public Rectangle DeviceBounds { get; set; }

        public ViewportBounds(double left, double top, double width, double height, double dpiScaleX = 1.0, double dpiScaleY = 1.0)
        {
            Left = left;
            Top = top;
            Width = Math.Max(100, width);
            Height = Math.Max(100, height);
            DpiScaleX = dpiScaleX > 0 ? dpiScaleX : 1.0;
            DpiScaleY = dpiScaleY > 0 ? dpiScaleY : 1.0;
            DpiX = (uint)Math.Round(DpiScaleX * 96.0);
            DpiY = (uint)Math.Round(DpiScaleY * 96.0);

            DeviceLeft = (int)Math.Round(left * DpiScaleX);
            DeviceTop = (int)Math.Round(top * DpiScaleY);
            DeviceWidth = (int)Math.Round(width * DpiScaleX);
            DeviceHeight = (int)Math.Round(height * DpiScaleY);
            DeviceBounds = new Rectangle(DeviceLeft, DeviceTop, DeviceWidth, DeviceHeight);
        }

        public (int deviceX, int deviceY) DipToDevice(double dipX, double dipY)
        {
            int devX = (int)Math.Round(DeviceLeft + (dipX - Left) * DpiScaleX);
            int devY = (int)Math.Round(DeviceTop + (dipY - Top) * DpiScaleY);
            return (devX, devY);
        }

        public (double dipX, double dipY) DeviceToDip(int devX, int devY)
        {
            double dipX = Left + (devX - DeviceLeft) / DpiScaleX;
            double dipY = Top + (devY - DeviceTop) / DpiScaleY;
            return (dipX, dipY);
        }

        public override string ToString() =>
            $"Viewport[DIP: ({Left:0},{Top:0},{Width:0}x{Height:0}), Device: ({DeviceLeft},{DeviceTop},{DeviceWidth}x{DeviceHeight}), Scale: {DpiScaleX:0.##}x{DpiScaleY:0.##} ({DpiX}dpi)]";
    }

    /// <summary>
    /// Centralized viewport, DPI awareness, and screen boundary utility.
    /// Handles dynamic detection of screen resolution, per-monitor DPI scaling, multi-monitor setups,
    /// safe margin enforcement, position clamping, and responsive positioning of windows and overlays.
    /// </summary>
    public static class ViewportService
    {
        #region Win32 P/Invoke
        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("User32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("SHCore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("User32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOSIZE = 0x0001;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        #endregion

        private class NativeMonitorInfo
        {
            public string DeviceName { get; set; } = "";
            public bool IsPrimary { get; set; }
            public Rectangle PhysicalMonitor { get; set; }
            public Rectangle PhysicalWorkArea { get; set; }
            public uint DpiX { get; set; } = 96;
            public uint DpiY { get; set; } = 96;
            public double DpiScaleX => DpiX / 96.0;
            public double DpiScaleY => DpiY / 96.0;
        }

        private static List<NativeMonitorInfo> GetNativeMonitors()
        {
            var list = new List<NativeMonitorInfo>();
            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
                {
                    var mi = new MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                    if (GetMonitorInfo(hMon, ref mi))
                    {
                        uint dpiX = 96, dpiY = 96;
                        try
                        {
                            int hr = GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                            if (hr != 0 || dpiX == 0 || dpiY == 0)
                            {
                                dpiX = 96;
                                dpiY = 96;
                            }
                        }
                        catch
                        {
                            dpiX = 96;
                            dpiY = 96;
                        }

                        list.Add(new NativeMonitorInfo
                        {
                            DeviceName = mi.szDevice ?? "",
                            IsPrimary = (mi.dwFlags & 1) != 0,
                            PhysicalMonitor = new Rectangle(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top),
                            PhysicalWorkArea = new Rectangle(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top),
                            DpiX = dpiX,
                            DpiY = dpiY
                        });
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"EnumDisplayMonitors failed: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Configurable safe margin in pixels from screen edges. Default: 15px.
        /// </summary>
        public static double SafeMargin { get; set; } = 15.0;

        /// <summary>
        /// Optional viewport override for testing simulated screen sizes and DPIs.
        /// </summary>
        public static ViewportBounds? SimulatedViewport { get; set; }

        /// <summary>
        /// Event fired whenever resolution, orientation, work area, or display settings change.
        /// </summary>
        public static event Action<ViewportBounds>? ViewportChanged;

        static ViewportService()
        {
            try
            {
                SystemEvents.DisplaySettingsChanged += (s, e) =>
                {
                    LoggerService.Info("Độ phân giải màn hình hoặc cài đặt hiển thị đã thay đổi (DisplaySettingsChanged)");
                    NotifyViewportChanged();
                };

                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (e.Category == UserPreferenceCategory.Desktop || e.Category == UserPreferenceCategory.General)
                    {
                        LoggerService.Info("Khu vực làm việc desktop thay đổi (UserPreferenceChanged)");
                        NotifyViewportChanged();
                    }
                };
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"Không thể đăng ký sự kiện SystemEvents: {ex.Message}");
            }
        }

        public static void NotifyViewportChanged(int screenIndex = 0)
        {
            var vp = GetViewport(screenIndex);
            ViewportChanged?.Invoke(vp);
        }

        /// <summary>
        /// Gets the current active viewport bounds in WPF Device-Independent Pixels (DIPs)
        /// with accurate DPI scaling and multi-monitor support.
        /// Queries native Win32 GetMonitorInfo for true physical coordinates, then converts to DIPs.
        /// </summary>
        public static ViewportBounds GetViewport(int screenIndex = 0)
        {
            if (SimulatedViewport != null)
            {
                return SimulatedViewport;
            }

            try
            {
                // 1. Primary path: Native Win32 EnumDisplayMonitors + GetMonitorInfo
                // This guarantees true physical device coordinates unaffected by WinForms DPI context variations
                var nativeMonitors = GetNativeMonitors();
                if (nativeMonitors.Count > 0)
                {
                    NativeMonitorInfo? target = null;

                    // Match with Screen.AllScreens[screenIndex] by DeviceName (e.g. \\.\DISPLAY1)
                    try
                    {
                        var screens = Screen.AllScreens;
                        if (screens.Length > 0)
                        {
                            int sIdx = (screenIndex >= 0 && screenIndex < screens.Length) ? screenIndex : 0;
                            var winFormsScreen = screens[sIdx];
                            target = nativeMonitors.Find(m => string.Equals(m.DeviceName, winFormsScreen.DeviceName, StringComparison.OrdinalIgnoreCase));
                        }
                    }
                    catch { }

                    // Fallback: order by Primary first, then screenIndex
                    if (target == null)
                    {
                        var ordered = new List<NativeMonitorInfo>(nativeMonitors);
                        ordered.Sort((a, b) => b.IsPrimary.CompareTo(a.IsPrimary));
                        int idx = (screenIndex >= 0 && screenIndex < ordered.Count) ? screenIndex : 0;
                        target = ordered[idx];
                    }

                    double scaleX = target.DpiScaleX;
                    double scaleY = target.DpiScaleY;

                    // Convert true physical Win32 WorkArea to WPF Device-Independent Pixels (DIPs)
                    double dipLeft = target.PhysicalWorkArea.Left / scaleX;
                    double dipTop = target.PhysicalWorkArea.Top / scaleY;
                    double dipWidth = target.PhysicalWorkArea.Width / scaleX;
                    double dipHeight = target.PhysicalWorkArea.Height / scaleY;

                    var vp = new ViewportBounds(dipLeft, dipTop, dipWidth, dipHeight, scaleX, scaleY)
                    {
                        DeviceLeft = target.PhysicalWorkArea.Left,
                        DeviceTop = target.PhysicalWorkArea.Top,
                        DeviceWidth = target.PhysicalWorkArea.Width,
                        DeviceHeight = target.PhysicalWorkArea.Height,
                        DeviceBounds = target.PhysicalMonitor,
                        DpiX = target.DpiX,
                        DpiY = target.DpiY
                    };
                    return vp;
                }

                // 2. Secondary fallback: WinForms Screen.AllScreens with DPI-awareness detection
                var screensFallback = Screen.AllScreens;
                if (screensFallback.Length > 0)
                {
                    int idx = (screenIndex >= 0 && screenIndex < screensFallback.Length) ? screenIndex : 0;
                    var s = screensFallback[idx];
                    var devWork = s.WorkingArea;
                    var devBounds = s.Bounds;

                    uint dpiX = 96, dpiY = 96;
                    try
                    {
                        var pt = new POINT(devBounds.Left + 10, devBounds.Top + 10);
                        var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                        if (hMon != IntPtr.Zero)
                        {
                            int hr = GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                            if (hr != 0 || dpiX == 0 || dpiY == 0) { dpiX = 96; dpiY = 96; }
                        }
                    }
                    catch { dpiX = 96; dpiY = 96; }

                    double scaleX = dpiX / 96.0;
                    double scaleY = dpiY / 96.0;

                    // Defensive check: If s.Bounds.Width is close to DIP width, WinForms already returned DIPs!
                    bool isAlreadyDip = scaleX > 1.05 && devBounds.Width <= (SystemParameters.VirtualScreenWidth + 50);

                    double dipLeft, dipTop, dipWidth, dipHeight;
                    int finalDevLeft, finalDevTop, finalDevWidth, finalDevHeight;

                    if (isAlreadyDip)
                    {
                        dipLeft = devWork.Left;
                        dipTop = devWork.Top;
                        dipWidth = devWork.Width;
                        dipHeight = devWork.Height;

                        finalDevLeft = (int)Math.Round(devWork.Left * scaleX);
                        finalDevTop = (int)Math.Round(devWork.Top * scaleY);
                        finalDevWidth = (int)Math.Round(devWork.Width * scaleX);
                        finalDevHeight = (int)Math.Round(devWork.Height * scaleY);
                    }
                    else
                    {
                        dipLeft = devWork.Left / scaleX;
                        dipTop = devWork.Top / scaleY;
                        dipWidth = devWork.Width / scaleX;
                        dipHeight = devWork.Height / scaleY;

                        finalDevLeft = devWork.Left;
                        finalDevTop = devWork.Top;
                        finalDevWidth = devWork.Width;
                        finalDevHeight = devWork.Height;
                    }

                    return new ViewportBounds(dipLeft, dipTop, dipWidth, dipHeight, scaleX, scaleY)
                    {
                        DeviceLeft = finalDevLeft,
                        DeviceTop = finalDevTop,
                        DeviceWidth = finalDevWidth,
                        DeviceHeight = finalDevHeight,
                        DeviceBounds = new Rectangle(finalDevLeft, finalDevTop, finalDevWidth, finalDevHeight),
                        DpiX = dpiX,
                        DpiY = dpiY
                    };
                }

                // 3. Fallback to WPF Primary WorkArea
                var wpfArea = SystemParameters.WorkArea;
                if (wpfArea.Width > 0 && wpfArea.Height > 0)
                {
                    return new ViewportBounds(wpfArea.Left, wpfArea.Top, wpfArea.Width, wpfArea.Height, 1.0, 1.0);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"Lỗi khi lấy thông số Viewport: {ex.Message}");
            }

            // Fallback safe resolution
            return new ViewportBounds(0, 0, 1920, 1080, 1.0, 1.0);
        }

        public static Rectangle GetWorkingArea(int screenIndex = 0)
        {
            var vp = GetViewport(screenIndex);
            return new Rectangle((int)Math.Round(vp.Left), (int)Math.Round(vp.Top), (int)Math.Round(vp.Width), (int)Math.Round(vp.Height));
        }

        public static int GetScreenCount()
        {
            try
            {
                return Screen.AllScreens.Length;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Positions a WPF window reliably on a specific screen.
        /// Sets WPF Left/Top in DIPs, and if HWND is available, uses Win32 SetWindowPos with device coordinates
        /// to guarantee exact pixel positioning across multi-monitor setups with mixed DPI.
        /// </summary>
        public static void SetWindowPosition(System.Windows.Window win, double dipLeft, double dipTop, int screenIndex = 0)
        {
            var vp = GetViewport(screenIndex);

            // 1. Update WPF window logical coordinates
            win.Left = dipLeft;
            win.Top = dipTop;

            // 2. If HWND exists, apply exact physical placement via Win32 SetWindowPos
            try
            {
                var helper = new WindowInteropHelper(win);
                if (helper.Handle != IntPtr.Zero)
                {
                    var (devX, devY) = vp.DipToDevice(dipLeft, dipTop);
                    SetWindowPos(helper.Handle, IntPtr.Zero, devX, devY, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSIZE);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"SetWindowPosition Win32 failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Clamps a position to ensure the element stays completely inside the visible viewport,
        /// observing the configurable safe margin.
        /// Formula:
        ///   x = Math.Max(minX, Math.Min(x, viewportWidth - petWidth - margin))
        ///   y = Math.Max(minY, Math.Min(y, viewportHeight - petHeight - margin))
        /// </summary>
        public static (double clampedX, double clampedY) ClampPosition(
            double x, double y,
            double elementWidth, double elementHeight,
            int screenIndex = 0,
            double? margin = null)
        {
            var vp = GetViewport(screenIndex);
            var m = margin ?? SafeMargin;

            var minX = vp.Left + m;
            var maxX = vp.Left + vp.Width - elementWidth - m;
            var minY = vp.Top + m;
            var maxY = vp.Top + vp.Height - elementHeight - m;

            if (maxX < minX) maxX = minX;
            if (maxY < minY) maxY = minY;

            var cx = Math.Max(minX, Math.Min(x, maxX));
            var cy = Math.Max(minY, Math.Min(y, maxY));

            return (cx, cy);
        }

        /// <summary>
        /// Computes the ground Y coordinate for the pet to sit on the desktop floor above the taskbar.
        /// </summary>
        public static double GetGroundY(double petHeight, int screenIndex = 0, double? margin = null)
        {
            var vp = GetViewport(screenIndex);
            var m = margin ?? SafeMargin;
            return Math.Max(vp.Top + m, vp.Top + vp.Height - petHeight - m);
        }

        /// <summary>
        /// Calculates a safe, resolution-independent default spawn position for a pet.
        /// Never relies on hard-coded developer coordinates.
        /// </summary>
        public static (double spawnX, double spawnY) GetDefaultSpawnPosition(
            double petWidth, double petHeight, int screenIndex = 0)
        {
            var vp = GetViewport(screenIndex);
            var m = SafeMargin;
            // Spawn comfortably at 60% of viewport width and grounded on the floor
            var targetX = vp.Left + Math.Max(m, (vp.Width - petWidth) * 0.6);
            var targetY = GetGroundY(petHeight, screenIndex, m);
            return ClampPosition(targetX, targetY, petWidth, petHeight, screenIndex, m);
        }

        /// <summary>
        /// Automatically repositions a popup or window near the pet without covering the pet
        /// and without getting clipped by viewport edges or covered by the taskbar.
        /// </summary>
        public static void PositionWindowNearPet(
            System.Windows.Window win, double petX, double petY, double petWidth, double petHeight, int screenIndex = 0)
        {
            var vp = GetViewport(screenIndex);
            var m = SafeMargin;

            // Ensure window dimensions fit inside viewport with safe margins
            if (win.Width > vp.Width - (m * 2))
            {
                win.Width = Math.Max(320, vp.Width - (m * 2));
            }
            if (win.Height > vp.Height - (m * 2))
            {
                win.Height = Math.Max(300, vp.Height - (m * 2));
            }

            win.WindowStartupLocation = WindowStartupLocation.Manual;

            // Check horizontal space to right vs left of pet
            double spaceRight = vp.Right - (petX + petWidth + m);
            double spaceLeft = (petX - m) - vp.Left;

            double targetLeft;
            if (spaceRight >= win.Width)
            {
                // Plenty of room to the right of the pet
                targetLeft = petX + petWidth + m;
            }
            else if (spaceLeft >= win.Width)
            {
                // Plenty of room to the left of the pet
                targetLeft = petX - m - win.Width;
            }
            else
            {
                // Viewport is narrow or pet is centered on small screen:
                // Center the window in the viewport
                targetLeft = vp.Left + Math.Max(m, (vp.Width - win.Width) / 2);
            }

            // Align window vertically near pet center, clamped within viewport
            double desiredTop = petY + (petHeight / 2) - (win.Height / 2);
            double minTop = vp.Top + m;
            double maxTop = Math.Max(minTop, vp.Bottom - win.Height - m);
            double targetTop = Math.Max(minTop, Math.Min(desiredTop, maxTop));

            double finalLeft = Math.Max(vp.Left + m, Math.Min(targetLeft, vp.Right - win.Width - m));
            double finalTop = Math.Max(minTop, Math.Min(targetTop, maxTop));

            SetWindowPosition(win, finalLeft, finalTop, screenIndex);
        }
    }
}
