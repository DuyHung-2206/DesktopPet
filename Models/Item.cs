using System.Text.Json.Serialization;

namespace DesktopPet.Models
{
    public class Item
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "Food"; // Food, Toy, Accessory

        [JsonPropertyName("slot")]
        public string? Slot { get; set; } // Hat, Glasses, Bow, Backpack

        [JsonPropertyName("overlayAsset")]
        public string? OverlayAsset { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "🍎";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public int Price { get; set; } = 10;

        [JsonPropertyName("hungerRestore")]
        public double HungerRestore { get; set; } = 0;

        [JsonPropertyName("happinessBonus")]
        public double HappinessBonus { get; set; } = 0;

        [JsonPropertyName("energyBonus")]
        public double EnergyBonus { get; set; } = 0;

        [JsonPropertyName("cleanlinessImpact")]
        public double CleanlinessImpact { get; set; } = 0;
    }
}
