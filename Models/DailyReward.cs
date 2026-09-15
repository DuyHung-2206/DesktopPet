namespace DesktopPet.Models
{
    public class DailyReward
    {
        public int DayNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "🎁";
        public int Coins { get; set; }
        public int Exp { get; set; }
        public string? ItemIdReward { get; set; }
        public string? ItemNameReward { get; set; }
        public int ItemQuantity { get; set; } = 1;
        public bool IsClaimed { get; set; }
        public bool IsCurrentDay { get; set; }

        public string RewardSummary => $"+{Coins} Xu, +{Exp} EXP, +{ItemQuantity} {ItemNameReward ?? "Vật phẩm"}";
    }
}
