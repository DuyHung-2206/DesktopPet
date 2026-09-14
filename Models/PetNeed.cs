using System;
using System.Text.Json.Serialization;

namespace DesktopPet.Models
{
    public static class PetNeedTypes
    {
        public const string Hunger = "hunger";
        public const string Thirst = "thirst";
        public const string Play = "play";
        public const string Bath = "bath";
        public const string Sleep = "sleep";
        public const string Affection = "affection";

        public static readonly string[] All = new[]
        {
            Hunger,
            Thirst,
            Play,
            Bath,
            Sleep,
            Affection
        };
    }

    public class PetNeedState
    {
        [JsonPropertyName("active")]
        public bool Active { get; set; } = false;

        [JsonPropertyName("completed")]
        public bool Completed { get; set; } = false;

        [JsonPropertyName("requestedAtUtc")]
        public DateTime? RequestedAtUtc { get; set; }

        [JsonPropertyName("expiredCounted")]
        public bool ExpiredCounted { get; set; } = false;

        public PetNeedState() { }

        public PetNeedState(bool active, bool completed)
        {
            Active = active;
            Completed = completed;
            ExpiredCounted = false;
            if (active)
            {
                RequestedAtUtc = DateTime.UtcNow;
            }
        }
    }
}
