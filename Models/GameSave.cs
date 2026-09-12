using System;
using System.Collections.Generic;

namespace DesktopPet.Models
{
    public class GameSettings
    {
        public double SoundVolume { get; set; } = 0.8;
        public double MusicVolume { get; set; } = 0.5;
        public bool IsMuted { get; set; } = false;
        public double PetScale { get; set; } = 1.0; // 0.75, 1.0, 1.25, 1.5
        public int SelectedMonitorIndex { get; set; } = 0;
        public bool AlwaysOnTop { get; set; } = true;
        public bool NotificationsEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool PerformanceMode { get; set; } = false;
    }

    public class GameSave
    {
        public int Version { get; set; } = 1;
        public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;

        // Kinh tế
        public int Coins { get; set; } = 150;

        // Thú cưng
        public string ActivePetId { get; set; } = string.Empty;
        public List<Pet> Pets { get; set; } = new();
        public List<string> UnlockedSpeciesIds { get; set; } = new() { "cat" };

        // Kho đồ & Trang bị
        public List<InventoryItem> Inventory { get; set; } = new();

        // Tiến trình & Danh hiệu
        public Dictionary<string, int> AchievementProgress { get; set; } = new();
        public List<string> UnlockedAchievementIds { get; set; } = new();

        // Điểm danh hằng ngày
        public int DailyRewardStreak { get; set; } = 1;
        public DateTime? LastDailyRewardUtc { get; set; }

        // Thống kê tổng
        public int TotalFeedCount { get; set; } = 0;
        public int TotalPlayCount { get; set; } = 0;
        public int TotalBathCount { get; set; } = 0;
        public int TotalMiniGameScore { get; set; } = 0;

        // Cài đặt
        public GameSettings Settings { get; set; } = new();
    }
}
