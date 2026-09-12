using System;
using DesktopPet.Models;

namespace DesktopPet.Services
{
    public class OfflineTimeResult
    {
        public TimeSpan Elapsed { get; set; }
        public double HungerLost { get; set; }
        public double EnergyGained { get; set; }
        public double CleanlinessLost { get; set; }
        public double HappinessLost { get; set; }
        public string SummaryMessage { get; set; } = string.Empty;
    }

    public static class OfflineTimeService
    {
        // Giới hạn thời gian tính toán tối đa 24 giờ để tránh cạn kiệt chỉ số quá mức
        private static readonly TimeSpan MaxOfflineTime = TimeSpan.FromHours(24);

        public static OfflineTimeResult CalculateAndApply(GameSave save)
        {
            var result = new OfflineTimeResult();
            var now = DateTime.UtcNow;
            var elapsed = now - save.LastPlayedUtc;

            // Bỏ qua nếu thời gian trôi quá ngắn (dưới 2 phút)
            if (elapsed.TotalMinutes < 2)
            {
                result.Elapsed = elapsed;
                result.SummaryMessage = "Chào mừng bạn quay lại ngay lập tức!";
                return result;
            }

            var cappedElapsed = elapsed > MaxOfflineTime ? MaxOfflineTime : elapsed;
            result.Elapsed = elapsed;

            var totalHours = cappedElapsed.TotalHours;

            // Mỗi giờ offline:
            // - Đói tăng (Hunger giảm 2.5 điểm/giờ)
            // - Năng lượng hồi phục nếu nghỉ ngơi (Energy tăng 4 điểm/giờ)
            // - Vệ sinh giảm nhẹ (1.5 điểm/giờ)
            // - Hạnh phúc giảm nhẹ (1.0 điểm/giờ)
            result.HungerLost = Math.Round(totalHours * 2.5, 1);
            result.EnergyGained = Math.Round(totalHours * 4.0, 1);
            result.CleanlinessLost = Math.Round(totalHours * 1.5, 1);
            result.HappinessLost = Math.Round(totalHours * 1.0, 1);

            foreach (var pet in save.Pets)
            {
                var species = DataManager.Instance.GetSpecies(pet.SpeciesId);
                var hungerMultiplier = species?.HungerRate ?? 1.0;
                var energyMultiplier = species?.EnergyRate ?? 1.0;

                pet.Hunger = Math.Max(10, pet.Hunger - (result.HungerLost * hungerMultiplier));
                pet.Energy = Math.Min(100, pet.Energy + (result.EnergyGained / energyMultiplier));
                pet.Cleanliness = Math.Max(15, pet.Cleanliness - result.CleanlinessLost);
                pet.Happiness = Math.Max(20, pet.Happiness - result.HappinessLost);

                // Nếu đói quá lâu (Hunger <= 15), giảm nhẹ Health nhưng tối thiểu còn 25 để không chết
                if (pet.Hunger <= 15)
                {
                    pet.Health = Math.Max(25, pet.Health - (totalHours * 1.0));
                }
            }

            var hoursDisplay = (int)elapsed.TotalHours;
            var minutesDisplay = elapsed.Minutes;
            var timeText = hoursDisplay > 0 ? $"{hoursDisplay} giờ {minutesDisplay} phút" : $"{minutesDisplay} phút";

            result.SummaryMessage = $"Bạn đã vắng mặt trong {timeText}.\nThú cưng đã nghỉ ngơi hồi phục thể lực nhưng hiện đang hơi đói!";
            LoggerService.Info($"Đã xử lý thời gian offline: {timeText}. HungerLost={result.HungerLost}, EnergyGained={result.EnergyGained}");

            return result;
        }
    }
}
