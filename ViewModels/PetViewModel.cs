using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.ViewModels
{
    public class PetViewModel : ViewModelBase
    {
        private readonly GameSave _save;
        private readonly PetStatService _statService = new();
        private readonly PetAIService _aiService = new();
        private readonly DispatcherTimer _animTimer = new();
        private readonly DispatcherTimer _statTimer = new();
        private readonly DispatcherTimer _autoSaveTimer = new();
        private DateTime _lastFrameTime = DateTime.UtcNow;

        private Pet _pet;
        private PetSpecies? _species;
        private bool _isStatusPopupOpen;
        private string _emoteText = "❤️";
        private bool _isEmoteVisible;
        private DispatcherTimer? _emoteHideTimer;

        public Pet Pet => _pet;
        public PetSpecies? Species => _species;
        public GameSave GameSave => _save;

        public PetState State => _pet.State;
        public bool IsFacingLeft => _pet.IsFacingLeft;
        public double X => _pet.X;
        public double Y => _pet.Y;

        public double Health => Math.Round(_pet.Health, 1);
        public double Hunger => Math.Round(_pet.Hunger, 1);
        public double Happiness => Math.Round(_pet.Happiness, 1);
        public double Energy => Math.Round(_pet.Energy, 1);
        public double Cleanliness => Math.Round(_pet.Cleanliness, 1);
        public double Affection => Math.Round(_pet.Affection, 1);

        public int Level => _pet.Level;
        public int Exp => _pet.Exp;
        public int MaxExp => _pet.MaxExp;
        public double ExpPercent
        {
            get => _pet.MaxExp > 0 ? (double)_pet.Exp / _pet.MaxExp * 100.0 : 0.0;
            set { }
        }
        public int Coins => _save.Coins;

        public string DisplayName => $"{_pet.Name} ({_species?.Name ?? "Pet"})";
        public string BaseColor => _species?.BaseColor ?? "#FFA726";
        public string SecondaryColor => _species?.SecondaryColor ?? "#FFE082";

        public bool IsStatusPopupOpen
        {
            get => _isStatusPopupOpen;
            set => SetProperty(ref _isStatusPopupOpen, value);
        }

        public string EmoteText
        {
            get => _emoteText;
            set => SetProperty(ref _emoteText, value);
        }

        public bool IsEmoteVisible
        {
            get => _isEmoteVisible;
            set => SetProperty(ref _isEmoteVisible, value);
        }

        // Icon trang phục
        public string? EquippedHeadIcon
        {
            get
            {
                if (_pet.EquippedItems.TryGetValue("Head", out var itemId))
                    return DataManager.Instance.GetItem(itemId)?.Icon;
                return null;
            }
        }

        public string? EquippedEyesIcon
        {
            get
            {
                if (_pet.EquippedItems.TryGetValue("Eyes", out var itemId))
                    return DataManager.Instance.GetItem(itemId)?.Icon;
                return null;
            }
        }

        public string? EquippedBackIcon
        {
            get
            {
                if (_pet.EquippedItems.TryGetValue("Back", out var itemId))
                    return DataManager.Instance.GetItem(itemId)?.Icon;
                return null;
            }
        }

        // Commands
        public ICommand ClickPetCommand { get; }
        public ICommand ToggleStatusPopupCommand { get; }
        public ICommand FeedCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand BathCommand { get; }
        public ICommand SleepToggleCommand { get; }

        public event Action? RequestOpenShop;
        public event Action? RequestOpenInventory;
        public event Action? RequestOpenDashboard;
        public event Action? RequestOpenMiniGame;
        public event Action? RequestOpenSettings;

        public PetViewModel(GameSave save)
        {
            _save = save;
            _pet = _save.Pets.Find(p => p.Id == _save.ActivePetId) ?? _save.Pets.FirstOrDefault() ?? new Pet();
            _species = DataManager.Instance.GetSpecies(_pet.SpeciesId);

            _statService.OnLevelUp = OnPetLevelUp;
            _statService.OnEmoteTriggered = msg => ShowEmote(msg, 2.5);

            ClickPetCommand = new RelayCommand(OnPetClicked);
            ToggleStatusPopupCommand = new RelayCommand(() => IsStatusPopupOpen = !IsStatusPopupOpen);
            FeedCommand = new RelayCommand(FeedPet);
            PlayCommand = new RelayCommand(PlayWithPet);
            BathCommand = new RelayCommand(BathPet);
            SleepToggleCommand = new RelayCommand(ToggleSleep);

            // Timer hoạt ảnh và vật lý (~30 FPS)
            _animTimer.Interval = TimeSpan.FromMilliseconds(33);
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();

            // Timer tính toán chỉ số (mỗi 1 giây)
            _statTimer.Interval = TimeSpan.FromSeconds(1);
            _statTimer.Tick += OnStatTick;
            _statTimer.Start();

            // Timer tự động lưu (mỗi 60 giây)
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(60);
            _autoSaveTimer.Tick += (s, e) => SaveService.Instance.SaveGame(_save);
            _autoSaveTimer.Start();

            // Tính toán vị trí pet an toàn trên màn hình ngay khi khởi động
            RecalculatePosition();

            // Đăng ký nhận diện thay đổi độ phân giải màn hình / xoay màn hình / thay đổi DPI
            ViewportService.ViewportChanged += OnViewportChanged;
        }

        private void OnViewportChanged(ViewportBounds vp)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RecalculatePosition();
            });
        }

        public void RecalculatePosition()
        {
            var petWidth = 70.0 * _save.Settings.PetScale;
            var petHeight = 70.0 * _save.Settings.PetScale;
            var monitorIndex = _save.Settings.SelectedMonitorIndex;

            var (cx, cy) = ViewportService.ClampPosition(_pet.X, _pet.Y, petWidth, petHeight, monitorIndex);
            var groundY = ViewportService.GetGroundY(petHeight, monitorIndex);

            if (_pet.Y >= groundY - 15 || !_aiService.IsFalling)
            {
                cy = groundY;
            }

            _pet.X = cx;
            _pet.Y = cy;

            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
        }

        public void SwitchActivePet(string petId)
        {
            var found = _save.Pets.Find(p => p.Id == petId);
            if (found != null)
            {
                _save.ActivePetId = found.Id;
                _pet = found;
                _species = DataManager.Instance.GetSpecies(_pet.SpeciesId);
                SaveService.Instance.SaveGame(_save);
                NotifyAllProperties();
                ShowEmote($"Xin chào, mình là {_pet.Name}! ✨", 2.0);
            }
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var dt = (now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;

            if (dt > 0.2) dt = 0.033; // Giới hạn deltaTime khi máy giật lag

            var petWidth = 70.0 * _save.Settings.PetScale;
            var petHeight = 70.0 * _save.Settings.PetScale;

            var oldState = _pet.State;
            _aiService.UpdateAI(_pet, _species, dt, _save.Settings.SelectedMonitorIndex, petWidth, petHeight);

            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            if (_pet.State != oldState)
            {
                OnPropertyChanged(nameof(State));
            }
            OnPropertyChanged(nameof(IsFacingLeft));
        }

        private double _spontaneousNeedTimer = 0.0;
        private readonly Random _needRand = new();

        public bool IsNeedActive(string needType)
        {
            if (_pet.Needs.TryGetValue(needType, out var state))
            {
                if (state.Active && !state.Completed)
                {
                    // Hết hạn yêu cầu sau 45 giây nếu người chơi không đáp ứng
                    if (state.RequestedAtUtc.HasValue)
                    {
                        var elapsed = (DateTime.UtcNow - state.RequestedAtUtc.Value).TotalSeconds;
                        if (elapsed > 45.0)
                        {
                            state.Active = false;
                            return false;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public bool HasAnyActiveNeed()
        {
            return _pet.Needs.Keys.Any(k => IsNeedActive(k));
        }

        public string? GetActiveNeed()
        {
            return _pet.Needs.Keys.FirstOrDefault(k => IsNeedActive(k));
        }

        public bool TriggerNeed(string needType, string? customPrompt = null)
        {
            if (!_pet.Needs.TryGetValue(needType, out var state))
            {
                state = new PetNeedState();
                _pet.Needs[needType] = state;
            }

            state.Active = true;
            state.Completed = false;
            state.RequestedAtUtc = DateTime.UtcNow;

            string prompt = customPrompt ?? GetDefaultNeedPrompt(needType);
            ShowEmote(prompt, 15.0);

            return true;
        }

        public string GetDefaultNeedPrompt(string needType)
        {
            return needType switch
            {
                PetNeedTypes.Hunger => "Mimi đói bụng rồi, cho mình ăn cá/đồ ăn ngon nhé! 🍖",
                PetNeedTypes.Thirst => "Khát nước quá nè, cho mình bình sữa hoặc nước mát nhé! 🥛",
                PetNeedTypes.Play => "Mimi buồn chán quá, chơi bóng với mình đi! 🎾",
                PetNeedTypes.Bath => "Người mình lấm lem quá rồi, tắm cho mình sạch sẽ với! 🛁",
                PetNeedTypes.Sleep => "Oáp... buồn ngủ ríu mắt rồi, cho mình đi ngủ một giấc nhé! 💤",
                PetNeedTypes.Affection => "Sen ơi, vuốt ve xoa đầu mình một chút đi! 🥰💖",
                _ => "Mimi cần bạn chăm sóc nè! ✨"
            };
        }

        public bool CompleteNeed(string needType, int expReward, out int awardedExp)
        {
            awardedExp = 0;
            if (IsNeedActive(needType) && _pet.Needs.TryGetValue(needType, out var state))
            {
                state.Active = false;
                state.Completed = true;
                awardedExp = expReward;

                _statService.AddExp(_pet, awardedExp);
                NotifyStatProperties();
                SaveService.Instance.SaveGame(_save);
                return true;
            }
            return false;
        }

        public void CheckNeedsUpdate(double deltaSeconds)
        {
            // 1. Kiểm tra theo ngưỡng chỉ số suy giảm
            if (_pet.Hunger < 35)
            {
                if (!IsNeedActive(PetNeedTypes.Hunger) && !_pet.Needs[PetNeedTypes.Hunger].Completed)
                {
                    TriggerNeed(PetNeedTypes.Hunger, "Bụng mình đói cồn cào rồi, cho mình xin chút đồ ăn nhé! 🍖");
                    NotificationService.RequestNotification("hungry", "Thú cưng đói bụng!", $"{_pet.Name} đang kêu đói, hãy cho bé ăn một chút nhé! 🍖");
                }
            }
            else if (_pet.Hunger > 70)
            {
                _pet.Needs[PetNeedTypes.Hunger].Completed = false;
            }

            if (_pet.Cleanliness < 35)
            {
                if (!IsNeedActive(PetNeedTypes.Bath) && !_pet.Needs[PetNeedTypes.Bath].Completed)
                {
                    TriggerNeed(PetNeedTypes.Bath, "Người mình lấm lem quá rồi, tắm cho mình sạch sẽ với! 🛁");
                    NotificationService.RequestNotification("dirty", "Thú cưng lấm lem!", $"{_pet.Name} bị dính bẩn rồi, hãy tắm rửa sạch sẽ nhé! 🛁");
                }
            }
            else if (_pet.Cleanliness > 70)
            {
                _pet.Needs[PetNeedTypes.Bath].Completed = false;
            }

            if (_pet.Energy < 30 && _pet.State != PetState.Sleep)
            {
                if (!IsNeedActive(PetNeedTypes.Sleep) && !_pet.Needs[PetNeedTypes.Sleep].Completed)
                {
                    TriggerNeed(PetNeedTypes.Sleep, "Oáp... buồn ngủ ríu mắt rồi, cho mình đi ngủ một giấc nhé! 💤");
                    NotificationService.RequestNotification("sleepy", "Thú cưng mệt mỏi!", $"{_pet.Name} đang ngáp dài, hãy cho bé ngủ một giấc nhé! 💤");
                }
            }
            else if (_pet.Energy > 70)
            {
                _pet.Needs[PetNeedTypes.Sleep].Completed = false;
            }

            if (_pet.Happiness < 35)
            {
                if (!IsNeedActive(PetNeedTypes.Play) && !_pet.Needs[PetNeedTypes.Play].Completed)
                {
                    TriggerNeed(PetNeedTypes.Play, "Buồn chán quá... Chơi bóng với mình một lát nhé! 🎾");
                }
            }
            else if (_pet.Happiness > 70)
            {
                _pet.Needs[PetNeedTypes.Play].Completed = false;
            }

            if (_pet.Affection < 35)
            {
                if (!IsNeedActive(PetNeedTypes.Affection) && !_pet.Needs[PetNeedTypes.Affection].Completed)
                {
                    TriggerNeed(PetNeedTypes.Affection, "Sen ơi, vuốt ve xoa đầu mình một chút đi! 🥰");
                }
            }
            else if (_pet.Affection > 70)
            {
                _pet.Needs[PetNeedTypes.Affection].Completed = false;
            }

            // 2. Nhu cầu tự nhiên định kỳ khi rảnh rỗi (chỉ khi chỉ số chưa đầy)
            _spontaneousNeedTimer += deltaSeconds;
            if (_spontaneousNeedTimer >= 150.0)
            {
                _spontaneousNeedTimer = 0.0;
                if (!HasAnyActiveNeed() && _pet.State == PetState.Idle)
                {
                    var validCandidates = new List<string>();
                    if (_pet.Hunger < 70 && !_pet.Needs[PetNeedTypes.Hunger].Completed) validCandidates.Add(PetNeedTypes.Hunger);
                    if (_pet.Cleanliness < 70 && !_pet.Needs[PetNeedTypes.Bath].Completed) validCandidates.Add(PetNeedTypes.Bath);
                    if (_pet.Happiness < 70 && !_pet.Needs[PetNeedTypes.Play].Completed) validCandidates.Add(PetNeedTypes.Play);
                    if (_pet.Energy < 50 && !_pet.Needs[PetNeedTypes.Sleep].Completed) validCandidates.Add(PetNeedTypes.Sleep);
                    if (_pet.Affection < 60 && !_pet.Needs[PetNeedTypes.Affection].Completed) validCandidates.Add(PetNeedTypes.Affection);

                    if (validCandidates.Count > 0)
                    {
                        var chosen = validCandidates[_needRand.Next(validCandidates.Count)];
                        TriggerNeed(chosen);
                    }
                }
            }
        }

        private void OnStatTick(object? sender, EventArgs e)
        {
            _statService.UpdateStatsTick(_pet, _species, 1.0);
            CheckNeedsUpdate(1.0);
            NotifyStatProperties();
        }

        public void OnPetClicked()
        {
            AudioService.Instance.PlayClick();
            _pet.Affection = Math.Min(100.0, _pet.Affection + 1.0);
            _aiService.TriggerHappy(_pet);

            CheckAchievementProgress("affection_50", (int)_pet.Affection);
            CheckAchievementProgress("affection_100", (int)_pet.Affection);

            // KIỂM TRA NHU CẦU VUỐT VE / TƯƠNG TÁC (Quy tắc 1, 2, 3, 10)
            if (CompleteNeed(PetNeedTypes.Affection, 10, out int expGain))
            {
                ShowEmote($"Thích được vuốt ve xoa đầu nhất! (+{expGain} EXP) 🥰💖✨", 3.0);
            }
            else
            {
                // Click bình thường -> KHÔNG CỘNG EXP (Quy tắc 10)
                var emotes = new[] { "❤️", "💖", "✨", "🎵", "🥰", "🐾", "Meo meo~ 🐾", "Yêu bạn nhiều lắm! 💖" };
                var randomEmote = emotes[new Random().Next(emotes.Length)];
                ShowEmote(randomEmote, 2.0);
            }

            NotifyStatProperties();
        }

        public void FeedPet()
        {
            // Chế độ chill: Thức ăn vô hạn, ưu tiên món cá hoặc món đầu tiên
            var foodItem = DataManager.Instance.GetItem("fish") ?? DataManager.Instance.ItemsList.FirstOrDefault(i => i.Category == "Food");

            if (foodItem != null)
            {
                _statService.ApplyItemEffect(_pet, foodItem);
                _save.TotalFeedCount++;
                CheckAchievementProgress("first_feed", 1);
                CheckAchievementProgress("feed_50_times", 1);
                AudioService.Instance.PlayFeed();

                // KÍCH HOẠT ANIMATION EAT: Chuyển sang Eat.png và phát tuần tự các frame
                PlayAnimation("Eat");

                // KIỂM TRA NHU CẦU ĂN:
                if (CompleteNeed(PetNeedTypes.Hunger, 15, out int expGain))
                {
                    ShowEmote($"Yum! {foodItem.Icon} Đang đói được ăn no thích quá! (+{expGain} EXP) ✨💖", 5.0);
                }
                else
                {
                    // Pet không yêu cầu ăn hoặc đã hoàn thành nhu cầu này rồi -> KHÔNG CỘNG EXP
                    ShowEmote($"Yum! {foodItem.Icon} Măm măm ngon miệng!", 5.0);
                }

                NotifyStatProperties();
            }
        }

        public void PlayWithPet()
        {
            // Chế độ chill: Đồ chơi vô hạn, ưu tiên bóng tennis hoặc món đầu tiên
            var toyItem = DataManager.Instance.GetItem("ball") ?? DataManager.Instance.ItemsList.FirstOrDefault(i => i.Category == "Toy");

            if (toyItem != null)
            {
                _statService.ApplyItemEffect(_pet, toyItem);
                _save.TotalPlayCount++;
                CheckAchievementProgress("first_play", 1);
                CheckAchievementProgress("play_50_times", 1);
                _aiService.TriggerHappy(_pet);
                AudioService.Instance.PlayHappy();

                // KIỂM TRA NHU CẦU CHƠI:
                if (CompleteNeed(PetNeedTypes.Play, 20, out int expGain))
                {
                    ShowEmote($"Vui quá! {toyItem.Icon} Chơi đúng lúc thích mê! (+{expGain} EXP) 🎾✨", 3.0);
                }
                else
                {
                    ShowEmote($"Vui quá! {toyItem.Icon} Chơi đùa thích ghê!", 2.5);
                }

                NotifyStatProperties();
            }
        }

        public void BathPet()
        {
            _pet.Cleanliness = 100.0;
            _pet.Happiness = Math.Min(100.0, _pet.Happiness + 10.0);
            _save.TotalBathCount++;
            CheckAchievementProgress("first_bath", 1);

            _pet.State = PetState.Bath;
            AudioService.Instance.PlayBath();

            // KIỂM TRA NHU CẦU TẮM:
            if (CompleteNeed(PetNeedTypes.Bath, 15, out int expGain))
            {
                ShowEmote($"Tắm mát thơm tho sạch sẽ! (+{expGain} EXP) 🧼🫧✨", 3.0);
            }
            else
            {
                ShowEmote("Tắm mát thơm tho! 🧼🫧", 2.5);
            }

            NotifyStatProperties();
        }

        public void ToggleSleep()
        {
            if (_pet.State == PetState.Sleep)
            {
                _aiService.TriggerWakeUp(_pet);
                ShowEmote("Dậy rồi nè! Chào bạn! ☀️", 1.8);
            }
            else
            {
                _aiService.TriggerSleep(_pet);
                // KIỂM TRA NHU CẦU NGỦ:
                if (CompleteNeed(PetNeedTypes.Sleep, 15, out int expGain))
                {
                    ShowEmote($"Buồn ngủ được ngủ ngon quá! (+{expGain} EXP) 💤✨", 3.0);
                }
                else
                {
                    ShowEmote("Ngủ khò khò... 💤", 2.0);
                }
            }
            OnPropertyChanged(nameof(State));
        }

        public void StartFalling()
        {
            _aiService.IsFalling = true;
        }

        public void ShowEmote(string text, double durationSeconds = 2.0)
        {
            EmoteText = text;
            IsEmoteVisible = true;

            _emoteHideTimer?.Stop();
            _emoteHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
            _emoteHideTimer.Tick += (s, e) =>
            {
                IsEmoteVisible = false;
                _emoteHideTimer.Stop();
            };
            _emoteHideTimer.Start();
        }

        public event Action<string>? RequestPlayAnimation;

        public void PlayAnimation(string animationName)
        {
            if (string.Equals(animationName, "Eat", StringComparison.OrdinalIgnoreCase))
            {
                _aiService.TriggerEat(_pet, 5.5);
                OnPropertyChanged(nameof(State));
                RequestPlayAnimation?.Invoke(animationName);
            }
            else if (string.Equals(animationName, "Happy", StringComparison.OrdinalIgnoreCase))
            {
                _aiService.TriggerHappy(_pet);
                OnPropertyChanged(nameof(State));
                RequestPlayAnimation?.Invoke(animationName);
            }
            else if (string.Equals(animationName, "Idle", StringComparison.OrdinalIgnoreCase))
            {
                _pet.State = PetState.Idle;
                OnPropertyChanged(nameof(State));
                RequestPlayAnimation?.Invoke(animationName);
            }
        }

        public void FinishEatAnimation()
        {
            if (_pet.State == PetState.Eat)
            {
                _aiService.FinishEat(_pet);
                OnPropertyChanged(nameof(State));
            }
        }

        public void AddCoins(int amount)
        {
            // Chế độ chill: Không dùng tính năng tiền tệ
            _save.Coins = 0;
            OnPropertyChanged(nameof(Coins));
        }

        private void OnPetLevelUp(Pet pet, int bonusCoins)
        {
            ShowEmote($"✨ LÊN CẤP {pet.Level}! Mimi vui vẻ hơn! ✨", 3.5);
            CheckAchievementProgress("reach_level_5", pet.Level);
            CheckAchievementProgress("reach_level_10", pet.Level);
            NotifyStatProperties();
        }

        public void CheckAchievementProgress(string achievementId, int amountOrLevel)
        {
            var ach = DataManager.Instance.AchievementsList.FirstOrDefault(a => a.Id == achievementId);
            if (ach == null || ach.IsUnlocked || _save.UnlockedAchievementIds.Contains(achievementId)) return;

            if (achievementId.StartsWith("reach_level") || achievementId.StartsWith("affection_"))
            {
                ach.Current = amountOrLevel;
            }
            else
            {
                ach.Current += amountOrLevel;
            }

            _save.AchievementProgress[achievementId] = ach.Current;

            if (ach.IsCompleted)
            {
                ach.IsUnlocked = true;
                _save.UnlockedAchievementIds.Add(achievementId);
                AudioService.Instance.PlayLevelUp();
                ShowEmote($"🏆 DANH HIỆU MỚI: {ach.Title}! ✨", 4.0);
                SaveService.Instance.SaveGame(_save);
            }
        }

        public void NotifyStatProperties()
        {
            OnPropertyChanged(nameof(Health));
            OnPropertyChanged(nameof(Hunger));
            OnPropertyChanged(nameof(Happiness));
            OnPropertyChanged(nameof(Energy));
            OnPropertyChanged(nameof(Cleanliness));
            OnPropertyChanged(nameof(Affection));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(Exp));
            OnPropertyChanged(nameof(MaxExp));
            OnPropertyChanged(nameof(ExpPercent));
        }

        public void NotifyAllProperties()
        {
            NotifyStatProperties();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(BaseColor));
            OnPropertyChanged(nameof(SecondaryColor));
            OnPropertyChanged(nameof(Coins));
            OnPropertyChanged(nameof(EquippedHeadIcon));
            OnPropertyChanged(nameof(EquippedEyesIcon));
            OnPropertyChanged(nameof(EquippedBackIcon));
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsFacingLeft));
        }

        public void PetViewModel_ApplyItem(Item item)
        {
            if (item == null) return;

            _statService.ApplyItemEffect(_pet, item);

            bool isFood = item.Category == "Food";
            bool isToy = item.Category == "Toy";
            bool isDrink = item.Id == "milk" || item.Name.Contains("Sữa", StringComparison.OrdinalIgnoreCase);

            if (isFood || isDrink)
            {
                // KÍCH HOẠT ANIMATION EAT KHI DÙNG THỨC ĂN HOẶC ĐỒ UỐNG
                PlayAnimation("Eat");
            }

            int expGain = 0;
            bool needCompleted = false;

            if (isDrink && CompleteNeed(PetNeedTypes.Thirst, 15, out expGain))
            {
                needCompleted = true;
                ShowEmote($"Đã khát rồi! {item.Icon} Cảm ơn bạn! (+{expGain} EXP) 🥛✨", 3.0);
            }
            else if (isFood && CompleteNeed(PetNeedTypes.Hunger, 15, out expGain))
            {
                needCompleted = true;
                ShowEmote($"Yum! {item.Icon} Đang đói được ăn no nê! (+{expGain} EXP) ✨💖", 5.0);
            }
            else if (isToy && CompleteNeed(PetNeedTypes.Play, 20, out expGain))
            {
                needCompleted = true;
                ShowEmote($"Thích quá! {item.Icon} Chơi đúng lúc mê ghê! (+{expGain} EXP) 🎾✨", 3.0);
            }

            if (!needCompleted)
            {
                // Dùng item khi pet không yêu cầu -> Không cộng EXP
                if (isFood)
                {
                    _save.TotalFeedCount++;
                    CheckAchievementProgress("first_feed", 1);
                    CheckAchievementProgress("feed_50_times", 1);
                    AudioService.Instance.PlayFeed();
                    ShowEmote($"Yum! {item.Icon} Măm măm ngon miệng!", 5.0);
                }
                else if (isToy)
                {
                    _save.TotalPlayCount++;
                    CheckAchievementProgress("first_play", 1);
                    CheckAchievementProgress("play_50_times", 1);
                    _aiService.TriggerHappy(_pet);
                    AudioService.Instance.PlayHappy();
                    ShowEmote($"Vui ghê! {item.Icon}", 2.5);
                }
                else
                {
                    ShowEmote($"Đã dùng {item.Icon}! ✨", 2.0);
                }
            }

            NotifyStatProperties();
        }

        public void OpenShop() => RequestOpenShop?.Invoke();
        public void OpenInventory() => RequestOpenInventory?.Invoke();
        public void OpenDashboard() => RequestOpenDashboard?.Invoke();
        public void OpenMiniGame() => RequestOpenMiniGame?.Invoke();
        public void OpenSettings() => RequestOpenSettings?.Invoke();
    }
}
