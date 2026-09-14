using System;
using System.Collections.Generic;

namespace DesktopPet.Models
{
    public static class EquipmentSlots
    {
        public const string Hat = "Hat";
        public const string Glasses = "Glasses";
        public const string Bow = "Bow";
        public const string Backpack = "Backpack";

        public static readonly string[] All = { Hat, Glasses, Bow, Backpack };

        public static string NormalizeSlot(string? slot)
        {
            if (string.IsNullOrWhiteSpace(slot)) return string.Empty;
            return slot.Trim().ToLowerInvariant() switch
            {
                "hat" or "head" => Hat,
                "glasses" or "eyes" => Glasses,
                "bow" or "neck" or "ribbon" => Bow,
                "backpack" or "back" => Backpack,
                _ => slot.Trim()
            };
        }
    }

    public class EquippedItems
    {
        public string? Hat { get; set; }
        public string? Glasses { get; set; }
        public string? Bow { get; set; }
        public string? Backpack { get; set; }

        public string? GetSlot(string slot)
        {
            var norm = EquipmentSlots.NormalizeSlot(slot);
            return norm switch
            {
                EquipmentSlots.Hat => Hat,
                EquipmentSlots.Glasses => Glasses,
                EquipmentSlots.Bow => Bow,
                EquipmentSlots.Backpack => Backpack,
                _ => null
            };
        }

        public void SetSlot(string slot, string? itemId)
        {
            var norm = EquipmentSlots.NormalizeSlot(slot);
            switch (norm)
            {
                case EquipmentSlots.Hat:
                    Hat = itemId;
                    break;
                case EquipmentSlots.Glasses:
                    Glasses = itemId;
                    break;
                case EquipmentSlots.Bow:
                    Bow = itemId;
                    break;
                case EquipmentSlots.Backpack:
                    Backpack = itemId;
                    break;
            }
        }

        public bool IsEquipped(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return string.Equals(Hat, itemId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Glasses, itemId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Bow, itemId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Backpack, itemId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
