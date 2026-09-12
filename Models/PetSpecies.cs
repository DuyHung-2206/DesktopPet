using System.Text.Json.Serialization;

namespace DesktopPet.Models
{
    public class PetSpecies
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("hungerRate")]
        public double HungerRate { get; set; } = 1.0;

        [JsonPropertyName("happinessRate")]
        public double HappinessRate { get; set; } = 1.0;

        [JsonPropertyName("energyRate")]
        public double EnergyRate { get; set; } = 1.0;

        [JsonPropertyName("cleanlinessRate")]
        public double CleanlinessRate { get; set; } = 1.0;

        [JsonPropertyName("speed")]
        public double Speed { get; set; } = 2.0;

        [JsonPropertyName("favoriteFood")]
        public string FavoriteFood { get; set; } = string.Empty;

        [JsonPropertyName("favoriteToy")]
        public string FavoriteToy { get; set; } = string.Empty;

        [JsonPropertyName("unlockPrice")]
        public int UnlockPrice { get; set; } = 0;

        [JsonPropertyName("isUnlocked")]
        public bool IsUnlocked { get; set; } = false;

        [JsonPropertyName("baseColor")]
        public string BaseColor { get; set; } = "#FFA726";

        [JsonPropertyName("secondaryColor")]
        public string SecondaryColor { get; set; } = "#FFE082";
    }
}
