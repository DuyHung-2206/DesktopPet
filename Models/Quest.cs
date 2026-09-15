namespace DesktopPet.Models
{
    public class Quest
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "🎯";
        public int Target { get; set; } = 1;
        public int Current { get; set; } = 0;
        public int RewardCoins { get; set; } = 30;
        public int RewardExp { get; set; } = 20;
        public string? RewardItemId { get; set; }
        public string? RewardItemName { get; set; }
        public int RewardItemQuantity { get; set; } = 1;
        public bool IsClaimed { get; set; } = false;

        public bool IsCompleted => Current >= Target;
        public bool CanClaim => IsCompleted && !IsClaimed;
        public string ProgressText => $"({(Current > Target ? Target : Current)}/{Target})";
        public string RewardText => $"+{RewardCoins} Xu, +{RewardExp} EXP, +{RewardItemQuantity} {(RewardItemName ?? "Vật phẩm")}";
        public string ButtonText => IsClaimed ? "Đã Nhận" : (IsCompleted ? "Nhận Thưởng" : "Chưa Xong");
        public string ButtonBackground => IsClaimed ? "#9E9E9E" : (IsCompleted ? "#4CAF50" : "#B0BEC5");
    }
}
