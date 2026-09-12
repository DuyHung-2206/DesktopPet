using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DesktopPet.Models;

namespace DesktopPet.Services
{
    public class DataManager
    {
        private static DataManager? _instance;
        public static DataManager Instance => _instance ??= new DataManager();

        public List<PetSpecies> SpeciesList { get; private set; } = new();
        public List<Item> ItemsList { get; private set; } = new();
        public List<Achievement> AchievementsList { get; private set; } = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private DataManager()
        {
            LoadAllData();
        }

        public void LoadAllData()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var dataDir = Path.Combine(baseDir, "Data");

                // Nạp Pets
                var petsPath = Path.Combine(dataDir, "pets.json");
                if (File.Exists(petsPath))
                {
                    var json = File.ReadAllText(petsPath);
                    SpeciesList = JsonSerializer.Deserialize<List<PetSpecies>>(json, _jsonOptions) ?? new();
                    LoggerService.Info($"Đã tải {SpeciesList.Count} loài thú cưng từ pets.json");
                }
                else
                {
                    LoggerService.Warn($"Không tìm thấy file pets.json tại {petsPath}, sử dụng dữ liệu mặc định");
                    SpeciesList = GetDefaultSpecies();
                }

                // Nạp Items
                var itemsPath = Path.Combine(dataDir, "items.json");
                if (File.Exists(itemsPath))
                {
                    var json = File.ReadAllText(itemsPath);
                    ItemsList = JsonSerializer.Deserialize<List<Item>>(json, _jsonOptions) ?? new();
                    LoggerService.Info($"Đã tải {ItemsList.Count} vật phẩm từ items.json");
                }
                else
                {
                    LoggerService.Warn($"Không tìm thấy file items.json tại {itemsPath}, sử dụng dữ liệu mặc định");
                    ItemsList = GetDefaultItems();
                }

                // Nạp Achievements
                var achPath = Path.Combine(dataDir, "achievements.json");
                if (File.Exists(achPath))
                {
                    var json = File.ReadAllText(achPath);
                    AchievementsList = JsonSerializer.Deserialize<List<Achievement>>(json, _jsonOptions) ?? new();
                    LoggerService.Info($"Đã tải {AchievementsList.Count} thành tựu từ achievements.json");
                }
                else
                {
                    LoggerService.Warn($"Không tìm thấy achievements.json, sử dụng danh sách mặc định");
                    AchievementsList = GetDefaultAchievements();
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Lỗi trong quá trình nạp dữ liệu game", ex);
                SpeciesList = GetDefaultSpecies();
                ItemsList = GetDefaultItems();
                AchievementsList = GetDefaultAchievements();
            }
        }

        public PetSpecies? GetSpecies(string id) => SpeciesList.Find(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        public Item? GetItem(string id) => ItemsList.Find(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        private List<PetSpecies> GetDefaultSpecies() => new()
        {
            new() { Id = "cat", Name = "Cat", DisplayName = "Mèo Mimi 🐱", Description = "Bé mèo mini siêu đáng yêu, thích cá và thích được vuốt ve.", IsUnlocked = true, BaseColor = "#FFA726" }
        };

        private List<Item> GetDefaultItems() => new()
        {
            new() { Id = "apple", Name = "Táo đỏ", Category = "Food", Icon = "🍎", Price = 10, HungerRestore = 15, HappinessBonus = 5 },
            new() { Id = "fish", Name = "Cá tươi", Category = "Food", Icon = "🐟", Price = 25, HungerRestore = 35, HappinessBonus = 20 },
            new() { Id = "ball", Name = "Bóng tennis", Category = "Toy", Icon = "🎾", Price = 30, HappinessBonus = 25, EnergyBonus = -10 }
        };

        private List<Achievement> GetDefaultAchievements() => new()
        {
            new() { Id = "first_feed", Title = "Bữa Ăn Đầu Tiên", Description = "Cho thú cưng ăn lần đầu tiên", Icon = "🍖", Target = 1, RewardCoins = 50 },
            new() { Id = "first_play", Title = "Khoảnh Khắc Vui Vẻ", Description = "Chơi với thú cưng lần đầu tiên", Icon = "🎾", Target = 1, RewardCoins = 50 }
        };
    }
}
