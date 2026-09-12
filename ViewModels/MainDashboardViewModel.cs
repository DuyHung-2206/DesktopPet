using System;
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
        public ObservableCollection<Achievement> Achievements { get; } = new();

        public bool CanClaimDailyReward { get; private set; }
        public string DailyRewardStatusText { get; private set; } = string.Empty;

        public ICommand ClaimDailyRewardCommand { get; }
        public ICommand OpenShopCommand { get; }
        public ICommand OpenInventoryCommand { get; }
        public ICommand OpenCollectionCommand { get; }
        public ICommand OpenMiniGameCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public MainDashboardViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            ClaimDailyRewardCommand = new RelayCommand(ClaimDailyReward, () => CanClaimDailyReward);
            OpenShopCommand = new RelayCommand(() => _petVM.OpenShop());
            OpenInventoryCommand = new RelayCommand(() => _petVM.OpenInventory());
            OpenCollectionCommand = new RelayCommand(() => _petVM.OpenDashboard());
            OpenMiniGameCommand = new RelayCommand(() => _petVM.OpenMiniGame());
            OpenSettingsCommand = new RelayCommand(() => _petVM.OpenSettings());

            LoadDailyRewards();
            LoadAchievements();
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

            var rewardsConfig = new[]
            {
                (1, "+50 EXP", "⭐", 0, null),
                (2, "Táo Đỏ", "🍎", 0, "apple"),
                (3, "Bóng Tennis", "🎾", 0, "ball"),
                (4, "Nơ Hồng", "🎀", 0, "ribbon_pink"),
                (5, "+100 EXP", "⭐", 0, null),
                (6, "Kính Mát", "🕶️", 0, "sunglasses_cool"),
                (7, "Vương Miện", "👑", 0, "crown_gold")
            };

            foreach (var r in rewardsConfig)
            {
                var isClaimed = r.Item1 < streak || (r.Item1 == streak && !CanClaimDailyReward);
                var isCurrent = r.Item1 == streak;

                DailyRewards.Add(new DailyReward
                {
                    DayNumber = r.Item1,
                    Title = r.Item2,
                    Icon = r.Item3,
                    Coins = 0,
                    ItemIdReward = r.Item5,
                    IsClaimed = isClaimed,
                    IsCurrentDay = isCurrent
                });
            }

            DailyRewardStatusText = CanClaimDailyReward
                ? $"Điểm danh ngày {streak} để cùng bé nhận quà ngay hôm nay!"
                : "Hôm nay bạn đã điểm danh cùng bé rồi. Hẹn gặp lại ngày mai!";

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

                _save.LastDailyRewardUtc = DateTime.UtcNow;
                if (_save.DailyRewardStreak >= 7) _save.DailyRewardStreak = 1;
                else _save.DailyRewardStreak++;

                AudioService.Instance.PlayLevelUp();
                _petVM.ShowEmote($"🎁 Nhận quà Ngày {streak}: Mimi vui vẻ vô cùng! ✨", 3.0);

                SaveService.Instance.SaveGame(_save);
                _petVM.NotifyStatProperties();
                LoadDailyRewards();
            }
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
