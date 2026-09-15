using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.ViewModels
{
    public class MainDashboardViewModel : ViewModelBase
    {
        private readonly PetViewModel _petVM;
        private readonly GameSave _save;

        public PetViewModel PetVM => _petVM;
        public GameSave GameSave => _save;

        public ObservableCollection<DailyReward> DailyRewards { get; } = new();
        public ObservableCollection<Quest> Quests { get; } = new();
        public ObservableCollection<Achievement> Achievements { get; } = new();

        public bool CanClaimDailyReward { get; private set; }
        public string DailyRewardStatusText { get; private set; } = string.Empty;

        public ICommand ClaimDailyRewardCommand { get; }
        public ICommand ClaimQuestCommand { get; }
        public ICommand OpenShopCommand { get; }
        public ICommand OpenInventoryCommand { get; }
        public ICommand OpenCollectionCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public MainDashboardViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            ClaimDailyRewardCommand = new RelayCommand(ClaimDailyReward, () => CanClaimDailyReward);
            ClaimQuestCommand = new RelayCommand<Quest>(ClaimQuest);
            OpenShopCommand = new RelayCommand(() => _petVM.OpenShop());
            OpenInventoryCommand = new RelayCommand(() => _petVM.OpenInventory());
            OpenCollectionCommand = new RelayCommand(() => _petVM.OpenDashboard());
            OpenSettingsCommand = new RelayCommand(() => _petVM.OpenSettings());

            _petVM.QuestProgressUpdated += OnQuestProgressUpdated;

            LoadDailyRewards();
            LoadQuests();
            LoadAchievements();
        }

        private void OnQuestProgressUpdated(string questId, int current)
        {
            var q = Quests.FirstOrDefault(x => x.Id == questId);
            if (q != null)
            {
                q.Current = current;
            }
        }

        private void LoadDailyRewards()
        {
            DailyRewards.Clear();
            var today = DateTime.UtcNow.Date;
            var lastClaimDate = _save.LastDailyRewardUtc?.Date;

            CanClaimDailyReward = lastClaimDate == null || lastClaimDate < today;

            // Nếu bỏ lỡ hơn 1 ngày, reset streak về ngày 1
            if (lastClaimDate != null && (today - lastClaimDate.Value).TotalDays > 1)
            {
                _save.DailyRewardStreak = 1;
            }

            var streak = Math.Clamp(_save.DailyRewardStreak, 1, 7);

            // Mỗi ngày điểm danh thưởng: Xu + EXP + Thức ăn/Đồ chơi
            var rewardsConfig = new (int Day, string Title, string Icon, int Coins, int Exp, string ItemId, string ItemName, int Quantity)[]
            {
                (1, "Ngày 1: Sữa & Xu", "🥛", 50, 25, "milk", "Sữa Tươi", 1),
                (2, "Ngày 2: Táo & Xu", "🍎", 60, 30, "apple", "Táo Đỏ", 1),
                (3, "Ngày 3: Bóng & Xu", "🎾", 75, 35, "ball", "Bóng Tennis", 1),
                (4, "Ngày 4: Cá & Xu", "🐟", 90, 40, "fish", "Cá Thu Tươi", 1),
                (5, "Ngày 5: Ghép Hình & Xu", "🧩", 110, 50, "puzzle", "Hộp Ghép Hình", 1),
                (6, "Ngày 6: Bánh Quy & Xu", "🍪", 135, 65, "cookie", "Bánh Quy", 1),
                (7, "Ngày 7: Gà Nướng & Xu", "🍗", 200, 100, "chicken", "Đùi Gà Nướng", 1)
            };

            foreach (var r in rewardsConfig)
            {
                var isClaimed = r.Day < streak || (r.Day == streak && !CanClaimDailyReward);
                var isCurrent = r.Day == streak;

                DailyRewards.Add(new DailyReward
                {
                    DayNumber = r.Day,
                    Title = r.Title,
                    Icon = r.Icon,
                    Coins = r.Coins,
                    Exp = r.Exp,
                    ItemIdReward = r.ItemId,
                    ItemNameReward = r.ItemName,
                    ItemQuantity = r.Quantity,
                    IsClaimed = isClaimed,
                    IsCurrentDay = isCurrent
                });
            }

            DailyRewardStatusText = CanClaimDailyReward
                ? $"Điểm danh Ngày {streak} để nhận ngay Xu, EXP và Thức ăn/Đồ chơi cho bé!"
                : "Hôm nay bạn đã nhận quà cùng bé rồi. Hãy quay lại vào ngày mai nhé!";

            OnPropertyChanged(nameof(CanClaimDailyReward));
            OnPropertyChanged(nameof(DailyRewardStatusText));
        }

        private void ClaimDailyReward()
        {
            if (!CanClaimDailyReward) return;

            var streak = Math.Clamp(_save.DailyRewardStreak, 1, 7);
            var currentReward = DailyRewards.FirstOrDefault(r => r.DayNumber == streak);

            if (currentReward != null)
            {
                _petVM.Pet.Affection = Math.Min(100.0, _petVM.Pet.Affection + 10.0);
                _petVM.Pet.Happiness = Math.Min(100.0, _petVM.Pet.Happiness + 15.0);

                // Thưởng Coins
                if (currentReward.Coins > 0)
                {
                    _petVM.AddCoins(currentReward.Coins);
                }

                // Thưởng EXP trực tiếp cho active pet
                if (currentReward.Exp > 0)
                {
                    _petVM.AddExp(currentReward.Exp);
                }

                // Thưởng Thức ăn / Đồ chơi vào Inventory (không giới hạn 50)
                if (!string.IsNullOrEmpty(currentReward.ItemIdReward))
                {
                    var existing = _save.Inventory.FirstOrDefault(i => i.ItemId == currentReward.ItemIdReward);
                    int qty = Math.Max(1, currentReward.ItemQuantity);
                    if (existing != null)
                    {
                        existing.Quantity += qty;
                    }
                    else
                    {
                        _save.Inventory.Add(new InventoryItem { ItemId = currentReward.ItemIdReward, Quantity = qty });
                    }
                }

                _save.LastDailyRewardUtc = DateTime.UtcNow;
                if (_save.DailyRewardStreak >= 7) _save.DailyRewardStreak = 1;
                else _save.DailyRewardStreak++;

                AudioService.Instance.PlayLevelUp();
                _petVM.ShowEmote($"🎁 Nhận quà Ngày {streak}: {currentReward.RewardSummary}! ✨", 3.5);

                SaveService.Instance.SaveGame(_save);
                _petVM.NotifyStatProperties();
                LoadDailyRewards();
            }
        }

        public void LoadQuests()
        {
            Quests.Clear();
            if (_save.QuestProgress == null) _save.QuestProgress = new Dictionary<string, int>();
            if (_save.ClaimedQuestIds == null) _save.ClaimedQuestIds = new List<string>();

            var questDefs = new[]
            {
                new Quest
                {
                    Id = "quest_feed",
                    Title = "Bữa Ăn Ngon Miệng",
                    Description = "Cho thú cưng ăn 1 lần",
                    Icon = "🍖",
                    Target = 1,
                    RewardCoins = 30,
                    RewardExp = 20,
                    RewardItemId = "milk",
                    RewardItemName = "Sữa Tươi",
                    RewardItemQuantity = 1
                },
                new Quest
                {
                    Id = "quest_drink",
                    Title = "Nước Mát Thanh Lành",
                    Description = "Cho thú cưng uống sữa/nước 1 lần",
                    Icon = "🥛",
                    Target = 1,
                    RewardCoins = 30,
                    RewardExp = 20,
                    RewardItemId = "apple",
                    RewardItemName = "Táo Đỏ",
                    RewardItemQuantity = 1
                },
                new Quest
                {
                    Id = "quest_play",
                    Title = "Giờ Chơi Vui Nhộn",
                    Description = "Chơi đùa cùng thú cưng 1 lần",
                    Icon = "🎾",
                    Target = 1,
                    RewardCoins = 40,
                    RewardExp = 25,
                    RewardItemId = "ball",
                    RewardItemName = "Bóng Tennis",
                    RewardItemQuantity = 1
                },
                new Quest
                {
                    Id = "quest_bath",
                    Title = "Tắm Gội Thơm Tho",
                    Description = "Tắm rửa sạch sẽ cho thú cưng 1 lần",
                    Icon = "🛁",
                    Target = 1,
                    RewardCoins = 35,
                    RewardExp = 20,
                    RewardItemId = "fish",
                    RewardItemName = "Cá Thu Tươi",
                    RewardItemQuantity = 1
                },
                new Quest
                {
                    Id = "quest_petting",
                    Title = "Yêu Thương Gắn Kết",
                    Description = "Vuốt ve xoa đầu thú cưng 3 lần",
                    Icon = "🥰",
                    Target = 3,
                    RewardCoins = 25,
                    RewardExp = 15,
                    RewardItemId = "cookie",
                    RewardItemName = "Bánh Quy",
                    RewardItemQuantity = 1
                }
            };

            foreach (var q in questDefs)
            {
                q.Current = _save.QuestProgress.TryGetValue(q.Id, out var cur) ? cur : 0;
                q.IsClaimed = _save.ClaimedQuestIds.Contains(q.Id);
                Quests.Add(q);
            }
        }

        private void ClaimQuest(Quest? quest)
        {
            if (quest == null || !quest.CanClaim) return;

            if (!_save.ClaimedQuestIds.Contains(quest.Id))
            {
                _save.ClaimedQuestIds.Add(quest.Id);
            }
            quest.IsClaimed = true;

            // Thưởng Coins
            if (quest.RewardCoins > 0)
            {
                _petVM.AddCoins(quest.RewardCoins);
            }

            // Thưởng EXP trực tiếp cho active pet
            if (quest.RewardExp > 0)
            {
                _petVM.AddExp(quest.RewardExp);
            }

            // Thưởng Food/Toy thêm vào Inventory (không giới hạn 50)
            if (!string.IsNullOrEmpty(quest.RewardItemId))
            {
                var existing = _save.Inventory.FirstOrDefault(i => i.ItemId == quest.RewardItemId);
                int qty = Math.Max(1, quest.RewardItemQuantity);
                if (existing != null)
                {
                    existing.Quantity += qty;
                }
                else
                {
                    _save.Inventory.Add(new InventoryItem { ItemId = quest.RewardItemId, Quantity = qty });
                }
            }

            AudioService.Instance.PlayLevelUp();
            _petVM.ShowEmote($"🎯 Hoàn thành: {quest.Title}! (+{quest.RewardCoins} Xu, +{quest.RewardExp} EXP, +{quest.RewardItemQuantity} {quest.RewardItemName}) ✨", 3.5);

            SaveService.Instance.SaveGame(_save);
            _petVM.NotifyStatProperties();
        }

        private void LoadAchievements()
        {
            Achievements.Clear();
            foreach (var ach in DataManager.Instance.AchievementsList)
            {
                if (_save.AchievementProgress.TryGetValue(ach.Id, out var cur))
                {
                    ach.Current = cur;
                }
                ach.IsUnlocked = _save.UnlockedAchievementIds.Contains(ach.Id);
                Achievements.Add(ach);
            }
        }
    }
}
