using System;
using System.Collections.Generic;

namespace DesktopPet.Models
{
    public class Pet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Mimi";
        public string SpeciesId { get; set; } = "cat";

        // Chỉ số cơ bản (0 - 100)
        public double Health { get; set; } = 100.0;
        public double Hunger { get; set; } = 80.0;
        public double Happiness { get; set; } = 85.0;
        public double Energy { get; set; } = 80.0;
        public double Cleanliness { get; set; } = 90.0;
        public double Affection { get; set; } = 50.0;

        // Cấp độ và Kinh nghiệm
        public int Level { get; set; } = 1;
        public int Exp { get; set; } = 0;
        public int MaxExp => Level * 100;

        // Tuổi và Ngày sinh
        public DateTime Birthday { get; set; } = DateTime.UtcNow;
        public int AgeDays => Math.Max(1, (int)(DateTime.UtcNow - Birthday).TotalDays);

        // Vị trí và Hướng
        public double X { get; set; } = 200;
        public double Y { get; set; } = 500;
        public bool IsFacingLeft { get; set; } = false;

        // Trạng thái hiện tại
        public PetState State { get; set; } = PetState.Idle;

        // Trang phục đang mặc (Key = Slot như Head, Eyes, Back)
        public Dictionary<string, string> EquippedItems { get; set; } = new();

        // Hệ thống nhu cầu (Needs)
        public Dictionary<string, PetNeedState> Needs { get; set; } = new()
        {
            { PetNeedTypes.Hunger, new PetNeedState() },
            { PetNeedTypes.Thirst, new PetNeedState() },
            { PetNeedTypes.Play, new PetNeedState() },
            { PetNeedTypes.Bath, new PetNeedState() },
            { PetNeedTypes.Sleep, new PetNeedState() },
            { PetNeedTypes.Affection, new PetNeedState() }
        };

        // Kiểm tra ngưỡng trạng thái
        public bool IsHungry => Hunger < 30;
        public bool IsSleepy => Energy < 25;
        public bool IsDirty => Cleanliness < 30;
        public bool IsSad => Happiness < 30;
        public bool IsSick => Health < 35;

        // Quản lý trang bị (EquippedItems helper methods)
        [System.Text.Json.Serialization.JsonIgnore]
        public EquippedItems EquippedSlots
        {
            get => new EquippedItems
            {
                Hat = GetEquippedItem(EquipmentSlots.Hat),
                Glasses = GetEquippedItem(EquipmentSlots.Glasses),
                Bow = GetEquippedItem(EquipmentSlots.Bow),
                Backpack = GetEquippedItem(EquipmentSlots.Backpack)
            };
            set
            {
                if (value == null) return;
                if (!string.IsNullOrEmpty(value.Hat)) EquipItem(EquipmentSlots.Hat, value.Hat);
                if (!string.IsNullOrEmpty(value.Glasses)) EquipItem(EquipmentSlots.Glasses, value.Glasses);
                if (!string.IsNullOrEmpty(value.Bow)) EquipItem(EquipmentSlots.Bow, value.Bow);
                if (!string.IsNullOrEmpty(value.Backpack)) EquipItem(EquipmentSlots.Backpack, value.Backpack);
            }
        }

        public string? GetEquippedItem(string slot)
        {
            if (EquippedItems == null) return null;
            var norm = EquipmentSlots.NormalizeSlot(slot);
            if (EquippedItems.TryGetValue(norm, out var id)) return id;
            foreach (var kvp in EquippedItems)
            {
                if (EquipmentSlots.NormalizeSlot(kvp.Key) == norm)
                    return kvp.Value;
            }
            return null;
        }

        public void EquipItem(string slot, string itemId)
        {
            EquippedItems ??= new Dictionary<string, string>();
            var norm = EquipmentSlots.NormalizeSlot(slot);
            var keysToRemove = new List<string>();
            foreach (var key in EquippedItems.Keys)
            {
                if (EquipmentSlots.NormalizeSlot(key) == norm)
                    keysToRemove.Add(key);
            }
            foreach (var k in keysToRemove) EquippedItems.Remove(k);
            EquippedItems[norm] = itemId;
        }

        public bool UnequipItem(string slotOrItemId)
        {
            if (EquippedItems == null) return false;
            var norm = EquipmentSlots.NormalizeSlot(slotOrItemId);
            var keysToRemove = new List<string>();
            foreach (var kvp in EquippedItems)
            {
                if (EquipmentSlots.NormalizeSlot(kvp.Key) == norm || kvp.Value == slotOrItemId)
                    keysToRemove.Add(kvp.Key);
            }
            bool removed = false;
            foreach (var k in keysToRemove)
            {
                EquippedItems.Remove(k);
                removed = true;
            }
            return removed;
        }

        public bool IsItemEquipped(string itemId)
        {
            if (EquippedItems == null) return false;
            return EquippedItems.ContainsValue(itemId);
        }
    }
}
