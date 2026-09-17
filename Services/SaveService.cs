using System;
using System.IO;
using System.Text.Json;
using DesktopPet.Models;

namespace DesktopPet.Services
{
    public class SaveService
    {
        private static SaveService? _instance;
        public static SaveService Instance => _instance ??= new SaveService();

        private readonly string _saveFolder;
        private readonly string _saveFilePath;
        private readonly string _backupFilePath;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private SaveService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _saveFolder = Path.Combine(appData, "DesktopPetWorld");
            _saveFilePath = Path.Combine(_saveFolder, "savegame.json");
            _backupFilePath = Path.Combine(_saveFolder, "savegame.json.bak");

            try
            {
                if (!Directory.Exists(_saveFolder))
                {
                    Directory.CreateDirectory(_saveFolder);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Không thể tạo thư mục lưu trữ save game", ex);
            }
        }

        public GameSave LoadGame()
        {
            try
            {
                if (File.Exists(_saveFilePath))
                {
                    var json = File.ReadAllText(_saveFilePath);
                    var save = JsonSerializer.Deserialize<GameSave>(json, _jsonOptions);
                    if (save != null)
                    {
                        LoggerService.Info("Đã nạp file savegame.json thành công.");
                        EnsureValidSave(save);
                        return save;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Lỗi khi đọc file savegame.json. Đang thử nạp từ file backup...", ex);
                try
                {
                    if (File.Exists(_backupFilePath))
                    {
                        var backupJson = File.ReadAllText(_backupFilePath);
                        var backupSave = JsonSerializer.Deserialize<GameSave>(backupJson, _jsonOptions);
                        if (backupSave != null)
                        {
                            LoggerService.Info("Khôi phục thành công từ file backup savegame.json.bak");
                            EnsureValidSave(backupSave);
                            return backupSave;
                        }
                    }
                }
                catch (Exception bEx)
                {
                    LoggerService.Error("Cả file chính và backup đều bị lỗi", bEx);
                }
            }

            LoggerService.Info("Khởi tạo dữ liệu save mới cho người chơi lần đầu.");
            var newSave = CreateDefaultSave();
            SaveGame(newSave);
            return newSave;
        }

        public bool SaveGame(GameSave save)
        {
            if (save == null) return false;

            try
            {
                save.LastPlayedUtc = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(save, _jsonOptions);

                // Nếu file save cũ tồn tại, sao lưu vào .bak trước
                if (File.Exists(_saveFilePath))
                {
                    try
                    {
                        File.Copy(_saveFilePath, _backupFilePath, true);
                    }
                    catch
                    {
                        // Bỏ qua lỗi ghi backup nếu bị lock
                    }
                }

                File.WriteAllText(_saveFilePath, json);
                LoggerService.Debug("Đã lưu tiến trình game thành công.");
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Error("Lỗi khi ghi file save game", ex);
                return false;
            }
        }

        public GameSave CreateDefaultSave()
        {
            var defaultPos = ViewportService.GetDefaultSpawnPosition(70.0, 70.0);
            var defaultPet = new Pet
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Mimi",
                SpeciesId = "cat",
                Level = 0,
                Exp = 0,
                Health = 100,
                Hunger = 85,
                Happiness = 90,
                Energy = 90,
                Cleanliness = 95,
                Affection = 50,
                X = defaultPos.spawnX,
                Y = defaultPos.spawnY,
                State = PetState.Idle
            };

            return new GameSave
            {
                Version = 1,
                Coins = 1000,
                ActivePetId = defaultPet.Id,
                Pets = new List<Pet> { defaultPet },
                UnlockedSpeciesIds = new List<string> { "cat", "dog" },
                Inventory = new List<InventoryItem>
                {
                    new() { ItemId = "apple", Quantity = 3 },
                    new() { ItemId = "fish", Quantity = 2 },
                    new() { ItemId = "ball", Quantity = 1 }
                },
                DailyRewardStreak = 1,
                LastDailyRewardUtc = null,
                Settings = new GameSettings()
            };
        }

        /// <summary>
        /// Đặt lại toàn bộ dữ liệu tiến trình của người chơi về trạng thái ban đầu:
        /// Coins = 1000, Level = 0, Exp = 0, Inventory khởi tạo mới, nhiệm vụ và điểm danh reset.
        /// Bảo toàn toàn bộ cài đặt ứng dụng (SoundVolume, IsMuted, v.v.).
        /// </summary>
        public bool ResetGameData(GameSave currentSave)
        {
            if (currentSave == null) return false;

            try
            {
                // 1. Bảo toàn cấu hình cài đặt người chơi
                var preservedSettings = currentSave.Settings ?? new GameSettings();

                // 2. Khởi tạo cấu trúc dữ liệu mới chuẩn
                var defaultSave = CreateDefaultSave();
                defaultSave.Coins = 1000;
                if (defaultSave.Pets.Count > 0)
                {
                    defaultSave.Pets[0].Level = 0;
                    defaultSave.Pets[0].Exp = 0;
                }

                // 3. Cập nhật in-place vào currentSave để bảo toàn tham chiếu bộ nhớ
                currentSave.Version = defaultSave.Version;
                currentSave.Coins = 1000;
                currentSave.ActivePetId = defaultSave.ActivePetId;

                currentSave.Pets.Clear();
                foreach (var p in defaultSave.Pets)
                {
                    currentSave.Pets.Add(p);
                }

                currentSave.UnlockedSpeciesIds.Clear();
                foreach (var sp in defaultSave.UnlockedSpeciesIds)
                {
                    currentSave.UnlockedSpeciesIds.Add(sp);
                }

                currentSave.Inventory.Clear();
                foreach (var item in defaultSave.Inventory)
                {
                    currentSave.Inventory.Add(new InventoryItem
                    {
                        ItemId = item.ItemId,
                        Quantity = item.Quantity
                    });
                }

                currentSave.DailyRewardStreak = 1;
                currentSave.LastDailyRewardUtc = null;

                currentSave.QuestProgress?.Clear();
                currentSave.ClaimedQuestIds?.Clear();
                currentSave.AchievementProgress?.Clear();
                currentSave.UnlockedAchievementIds?.Clear();

                currentSave.TotalFeedCount = 0;
                currentSave.TotalPlayCount = 0;
                currentSave.TotalBathCount = 0;
                currentSave.FailedRequestCount = 0;
                currentSave.HasTriggered50PlaySick = false;

                // 4. Giữ nguyên cấu hình cài đặt ứng dụng
                currentSave.Settings = preservedSettings;

                // 5. Lưu lại dữ liệu mới vào ổ đĩa ngay lập tức
                var saved = SaveGame(currentSave);
                LoggerService.Info("Đã reset toàn bộ dữ liệu game về mặc định (Coins: 1000, Level: 0, Exp: 0, Inventory khởi tạo).");
                return saved;
            }
            catch (Exception ex)
            {
                LoggerService.Error("Lỗi khi thực hiện ResetGameData", ex);
                return false;
            }
        }

        public GameSave ResetGameData()
        {
            var save = LoadGame();
            ResetGameData(save);
            return save;
        }

        private void EnsureValidSave(GameSave save)
        {
            save.Pets ??= new List<Pet>();
            save.UnlockedSpeciesIds ??= new List<string>();
            if (!save.UnlockedSpeciesIds.Contains("cat"))
            {
                save.UnlockedSpeciesIds.Add("cat");
            }
            if (!save.UnlockedSpeciesIds.Contains("dog"))
            {
                save.UnlockedSpeciesIds.Add("dog");
            }
            save.Inventory ??= new List<InventoryItem>();
            save.AchievementProgress ??= new Dictionary<string, int>();
            save.UnlockedAchievementIds ??= new List<string>();
            save.QuestProgress ??= new Dictionary<string, int>();
            save.ClaimedQuestIds ??= new List<string>();
            save.Settings ??= new GameSettings();
            save.Settings.SoundVolume = Math.Clamp(save.Settings.SoundVolume, 0.0, 1.0);
            AudioService.Instance.Volume = save.Settings.SoundVolume;
            AudioService.Instance.IsMuted = save.Settings.IsMuted;

            // Nếu Coins < 0 (dữ liệu hỏng) thì mới chuẩn hóa về 0, bảo toàn số xu người chơi
            if (save.Coins < 0) save.Coins = 0;
            if (save.FailedRequestCount < 0) save.FailedRequestCount = 0;

            if (save.Pets.Count == 0)
            {
                var p = new Pet { Name = "Mimi", SpeciesId = "cat" };
                save.Pets.Add(p);
                save.ActivePetId = p.Id;
            }
            else if (string.IsNullOrEmpty(save.ActivePetId) || !save.Pets.Exists(p => p.Id == save.ActivePetId))
            {
                save.ActivePetId = save.Pets[0].Id;
            }

            var activePet = save.Pets.Find(p => p.Id == save.ActivePetId) ?? save.Pets[0];
            var scale = save.Settings?.PetScale ?? 1.0;
            var petW = 70.0 * scale;
            var petH = 70.0 * scale;
            var monitorIdx = save.Settings?.SelectedMonitorIndex ?? 0;

            if (activePet.X <= 0 && activePet.Y <= 0)
            {
                var spawn = ViewportService.GetDefaultSpawnPosition(petW, petH, monitorIdx);
                activePet.X = spawn.spawnX;
                activePet.Y = spawn.spawnY;
            }
            else
            {
                var (clampedX, clampedY) = ViewportService.ClampPosition(activePet.X, activePet.Y, petW, petH, monitorIdx);
                activePet.X = clampedX;
                activePet.Y = clampedY;
            }

            // Đảm bảo hệ thống nhu cầu (Needs) được khởi tạo đầy đủ
            activePet.Needs ??= new Dictionary<string, PetNeedState>();
            foreach (var needType in PetNeedTypes.All)
            {
                if (!activePet.Needs.ContainsKey(needType))
                {
                    activePet.Needs[needType] = new PetNeedState();
                }
            }

            // Chuẩn hóa Inventory: loại bỏ các bản ghi không hợp lệ hoặc số lượng âm
            save.Inventory.RemoveAll(i => string.IsNullOrEmpty(i.ItemId) || i.Quantity <= 0);
        }
    }
}
