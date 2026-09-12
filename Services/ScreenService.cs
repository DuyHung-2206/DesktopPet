using System;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopPet.Services
{
    public static class ScreenService
    {
        public static Rectangle GetWorkingArea(int screenIndex = 0) => ViewportService.GetWorkingArea(screenIndex);

        public static int GetScreenCount() => ViewportService.GetScreenCount();

        public static (double clampedX, double clampedY) ClampToScreen(double x, double y, double petWidth, double petHeight, int screenIndex = 0)
            => ViewportService.ClampPosition(x, y, petWidth, petHeight, screenIndex);

        public static double GetGroundY(double petHeight, int screenIndex = 0)
            => ViewportService.GetGroundY(petHeight, screenIndex);
    }
}
