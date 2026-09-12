namespace DesktopPet.Models
{
    public class DailyReward
    {
        public int DayNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "🎁";
        public int Coins { get; set; }
        public string? ItemIdReward { get; set; }
        public bool IsClaimed { get; set; }
        public bool IsCurrentDay { get; set; }
    }
}
