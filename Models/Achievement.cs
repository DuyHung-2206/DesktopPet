using System.Text.Json.Serialization;

namespace DesktopPet.Models
{
    public class Achievement
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "🏆";

        [JsonPropertyName("target")]
        public int Target { get; set; } = 1;

        [JsonPropertyName("current")]
        public int Current { get; set; } = 0;

        [JsonPropertyName("rewardCoins")]
        public int RewardCoins { get; set; } = 50;

        [JsonPropertyName("rewardExp")]
        public int RewardExp { get; set; } = 30;

        [JsonPropertyName("isUnlocked")]
        public bool IsUnlocked { get; set; } = false;

        public bool IsCompleted => Current >= Target;
    }
}
