using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopPet.Services
{
    public class ViewportBounds
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Right => Left + Width;
        public double Bottom => Top + Height;

        public ViewportBounds(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = Math.Max(100, width);
            Height = Math.Max(100, height);
        }

        public override string ToString() => $"Viewport[{Left:0}, {Top:0}, {Width:0}x{Height:0} (Right={Right:0}, Bottom={Bottom:0})]";
    }

    /// <summary>
    /// Centralized viewport and screen boundary utility.
    /// Handles dynamic detection of screen resolution, DPI scaling, multi-monitor setups,
    /// safe margin enforcement, position clamping, and responsive positioning of windows and overlays.
    /// </summary>
    public static class ViewportService
    {
        /// <summary>
        /// Configurable safe margin in pixels from screen edges. Default: 15px.
        /// </summary>
        public static double SafeMargin { get; set; } = 15.0;

        /// <summary>
        /// Optional viewport override for testing simulated screen sizes (e.g. 800x600, 1024x768).
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
        /// Gets the current active viewport bounds (working area excluding taskbar).
        /// Falls back gracefully and supports multi-monitor and simulated viewports.
        /// </summary>
        public static ViewportBounds GetViewport(int screenIndex = 0)
        {
            if (SimulatedViewport != null)
            {
                return SimulatedViewport;
            }

            try
            {
                var screens = Screen.AllScreens;
                if (screens.Length > 0 && screenIndex >= 0 && screenIndex < screens.Length)
                {
                    var area = screens[screenIndex].WorkingArea;
                    return new ViewportBounds(area.Left, area.Top, area.Width, area.Height);
                }

                if (Screen.PrimaryScreen != null)
                {
                    var area = Screen.PrimaryScreen.WorkingArea;
                    return new ViewportBounds(area.Left, area.Top, area.Width, area.Height);
                }

                // Fallback to WPF Primary WorkArea
                var wpfArea = SystemParameters.WorkArea;
                if (wpfArea.Width > 0 && wpfArea.Height > 0)
                {
                    return new ViewportBounds(wpfArea.Left, wpfArea.Top, wpfArea.Width, wpfArea.Height);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"Lỗi khi lấy thông số Viewport: {ex.Message}");
            }

            // Fallback safe resolution
            return new ViewportBounds(0, 0, 1920, 1080);
        }

        public static Rectangle GetWorkingArea(int screenIndex = 0)
        {
            var vp = GetViewport(screenIndex);
            return new Rectangle((int)vp.Left, (int)vp.Top, (int)vp.Width, (int)vp.Height);
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
        /// and without getting clipped by viewport edges.
        /// </summary>
        public static void PositionWindowNearPet(
            Window win, double petX, double petY, double petWidth, double petHeight, int screenIndex = 0)
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

            win.Left = Math.Max(vp.Left + m, Math.Min(targetLeft, vp.Right - win.Width - m));
            win.Top = Math.Max(minTop, Math.Min(targetTop, maxTop));
        }
    }
}
