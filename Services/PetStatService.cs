using System;
using DesktopPet.Models;

namespace DesktopPet.Services
{
    public class PetStatService
    {
        public Action<Pet, int>? OnLevelUp;
        public Action<string>? OnEmoteTriggered;

        public void UpdateStatsTick(Pet pet, PetSpecies? species, double elapsedSeconds)
        {
            if (pet == null) return;

            var hungerRate = species?.HungerRate ?? 1.0;
            var energyRate = species?.EnergyRate ?? 1.0;
            var cleanlinessRate = species?.CleanlinessRate ?? 1.0;

            // Tính toán tốc độ suy giảm theo giây
            // Trung bình 1 giờ (3600s) giảm khoảng 15 - 20 điểm
            var factor = elapsedSeconds / 3600.0;

            if (pet.State == PetState.Sleep)
            {
                // Hồi phục năng lượng khi ngủ
                pet.Energy = Math.Min(100.0, pet.Energy + (factor * 60.0));
                // Khi ngủ giảm đói chậm hơn
                pet.Hunger = Math.Max(0.0, pet.Hunger - (factor * 8.0 * hungerRate));

                // Nếu đã hồi đầy năng lượng, đánh thức dậy
                if (pet.Energy >= 98.0)
                {
                    pet.State = PetState.WakeUp;
                    OnEmoteTriggered?.Invoke("⚡ Đã ngủ đẫy giấc!");
                }
            }
            else
            {
                // Hoạt động bình thường
                pet.Hunger = Math.Max(0.0, pet.Hunger - (factor * 18.0 * hungerRate));
                pet.Energy = Math.Max(0.0, pet.Energy - (factor * 12.0 * energyRate));
                pet.Cleanliness = Math.Max(0.0, pet.Cleanliness - (factor * 10.0 * cleanlinessRate));
                pet.Happiness = Math.Max(0.0, pet.Happiness - (factor * 8.0));
            }

            // Tác động lên sức khỏe nếu đói hoặc bẩn quá mức
            if (pet.Hunger < 15 || pet.Cleanliness < 15)
            {
                pet.Health = Math.Max(20.0, pet.Health - (factor * 10.0));
            }
            else if (pet.Hunger > 60 && pet.Cleanliness > 60 && pet.Health < 100.0)
            {
                pet.Health = Math.Min(100.0, pet.Health + (factor * 5.0));
            }

            // Kiểm tra thông báo trạng thái
            if (pet.IsHungry)
            {
                NotificationService.RequestNotification("hungry", "Thú cưng đói bụng!", $"{pet.Name} đang kêu đói, hãy cho bé ăn một chút nhé! 🍖");
            }
            else if (pet.IsSleepy && pet.State != PetState.Sleep)
            {
                NotificationService.RequestNotification("sleepy", "Thú cưng mệt mỏi!", $"{pet.Name} đang ngáp dài, hãy cho bé ngủ một giấc nhé! 💤");
            }
            else if (pet.IsDirty)
            {
                NotificationService.RequestNotification("dirty", "Thú cưng lấm lem!", $"{pet.Name} bị dính bẩn rồi, hãy tắm rửa sạch sẽ nhé! 🛁");
            }
        }

        public void AddExp(Pet pet, int amount, Action<int>? addCoinsCallback = null)
        {
            if (pet == null || amount <= 0) return;

            pet.Exp += amount;
            while (pet.Exp >= pet.MaxExp)
            {
                pet.Exp -= pet.MaxExp;
                pet.Level++;

                AudioService.Instance.PlayLevelUp();
                OnLevelUp?.Invoke(pet, 0);
                LoggerService.Info($"Pet {pet.Name} đạt Level {pet.Level}!");
            }
        }

        public void ApplyItemEffect(Pet pet, Item item, Action<int>? addCoinsCallback = null)
        {
            if (pet == null || item == null) return;

            pet.Hunger = Math.Clamp(pet.Hunger + item.HungerRestore, 0.0, 100.0);
            pet.Happiness = Math.Clamp(pet.Happiness + item.HappinessBonus, 0.0, 100.0);
            pet.Energy = Math.Clamp(pet.Energy + item.EnergyBonus, 0.0, 100.0);
            pet.Cleanliness = Math.Clamp(pet.Cleanliness + item.CleanlinessImpact, 0.0, 100.0);
            pet.Affection = Math.Clamp(pet.Affection + 2.0, 0.0, 100.0);
        }
    }
}
