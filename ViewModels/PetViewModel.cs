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
        private double _sickHealthTimer = 0.0;
        private double _recoveryHealthTimer = 0.0;

        public Pet Pet => _pet;
        public PetSpecies? Species => _species;
        public GameSave GameSave => _save;
        public PetAIService AIService => _aiService;

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
        public double PetScale => _save.Settings?.PetScale ?? 1.0;

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

        public event Action? AppearanceChanged;

        public void NotifyAppearanceChanged()
        {
            AppearanceChanged?.Invoke();
        }

        // Commands
        public ICommand ClickPetCommand { get; }
        public ICommand ToggleStatusPopupCommand { get; }
        public ICommand FeedCommand { get; }
        public ICommand DrinkCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand BathCommand { get; }
        public ICommand SleepToggleCommand { get; }

        public event Action? RequestOpenShop;
        public event Action? RequestOpenInventory;
        public event Action? RequestOpenDashboard;
        public event Action? RequestOpenCollection;
        public event Action? RequestOpenSettings;
        public event Action? InventoryChanged;

        public void TriggerInventoryChanged() => InventoryChanged?.Invoke();

        public PetViewModel(GameSave save)
        {
            _save = save;
            _pet = _save.Pets.Find(p => p.Id == _save.ActivePetId) ?? _save.Pets.FirstOrDefault() ?? new Pet();
            _species = DataManager.Instance.GetSpecies(_pet.SpeciesId);

            if (_pet.IsSick)
            {
                _pet.State = PetState.Sick;
            }

            _statService.OnLevelUp = OnPetLevelUp;
            _statService.OnEmoteTriggered = msg => ShowEmote(msg, 2.5);

            ClickPetCommand = new RelayCommand(OnPetClicked);
            ToggleStatusPopupCommand = new RelayCommand(() => IsStatusPopupOpen = !IsStatusPopupOpen);
            FeedCommand = new RelayCommand(FeedPet);
            DrinkCommand = new RelayCommand(DrinkPet);
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
            var oldFacing = _pet.IsFacingLeft;
            _aiService.UpdateAI(_pet, _species, dt, _save.Settings.SelectedMonitorIndex, petWidth, petHeight);

            UpdateInactivity(dt);
            CheckSickCondition();

            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            if (_pet.State != oldState)
            {
                OnPropertyChanged(nameof(State));
            }
            if (_pet.IsFacingLeft != oldFacing)
            {
                OnPropertyChanged(nameof(IsFacingLeft));
            }
        }

        private double _spontaneousNeedTimer = 0.0;
        private readonly Random _needRand = new();

        public bool IsNeedActive(string needType)
        {
            if (_pet.Needs.TryGetValue(needType, out var state))
            {
                if (state.Active && !state.Completed)
                {
                    // Hết hạn yêu cầu sau 45 giây nếu người chơi không đáp ứng (Rule 1)
                    if (state.RequestedAtUtc.HasValue)
                    {
                        var elapsed = (DateTime.UtcNow - state.RequestedAtUtc.Value).TotalSeconds;
                        if (elapsed > 45.0)
                        {
                            if (!state.ExpiredCounted)
                            {
                                state.ExpiredCounted = true;
                                state.Active = false;
                                RecordFailedRequest();
                            }
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
            state.ExpiredCounted = false;
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

        public bool CompleteNeed(string needType, int expReward, out int awardedExp, int coinReward = 10)
        {
            awardedExp = 0;
            if (IsNeedActive(needType) && _pet.Needs.TryGetValue(needType, out var state))
            {
                state.Active = false;
                state.Completed = true;
                state.ExpiredCounted = false;
                awardedExp = expReward;

                _statService.AddExp(_pet, awardedExp);
                if (coinReward > 0)
                {
                    _save.Coins += coinReward;
                    OnPropertyChanged(nameof(Coins));
                }
                NotifyStatProperties();
                SaveService.Instance.SaveGame(_save);
                return true;
            }
            return false;
        }

        public void AddExp(int amount)
        {
            if (amount <= 0) return;
            _statService.AddExp(_pet, amount);
            NotifyStatProperties();
            SaveService.Instance.SaveGame(_save);
        }

        public event Action<string, int>? QuestProgressUpdated;

        public void CheckQuestProgress(string questId, int count = 1)
        {
            if (_save.ClaimedQuestIds == null) _save.ClaimedQuestIds = new List<string>();
            if (_save.QuestProgress == null) _save.QuestProgress = new Dictionary<string, int>();

            if (_save.ClaimedQuestIds.Contains(questId)) return;

            if (!_save.QuestProgress.TryGetValue(questId, out var current))
            {
                current = 0;
            }
            _save.QuestProgress[questId] = current + count;
            QuestProgressUpdated?.Invoke(questId, _save.QuestProgress[questId]);
            SaveService.Instance.SaveGame(_save);
        }

        public void RecordFailedRequest()
        {
            _save.FailedRequestCount++;
            LoggerService.Info($"Yêu cầu của thú cưng đã hết hạn. Tổng số lần bỏ lỡ: {_save.FailedRequestCount}/15");
            if (_save.FailedRequestCount >= 15)
            {
                _save.FailedRequestCount = 0;
                TriggerAngry();
            }
            SaveService.Instance.SaveGame(_save);
        }

        public void TriggerAngry()
        {
            if (_aiService.IsFalling || _pet.State == PetState.Hurt) return;
            _aiService.TriggerState(_pet, PetState.Angry);
            ShowEmote("Hứ! Sao gọi mãi mà Sen không thèm quan tâm gì hết á! 😾💢", 4.0);
            OnPropertyChanged(nameof(State));
            RequestPlayAnimation?.Invoke("Angry");
        }

        public void CheckNeedsUpdate(double deltaSeconds)
        {
            // 0. Kiểm tra các yêu cầu đã quá hạn mà người chơi không đáp ứng (Rule 1)
            foreach (var state in _pet.Needs.Values)
            {
                if (state.Active && !state.Completed && state.RequestedAtUtc.HasValue)
                {
                    var elapsed = (DateTime.UtcNow - state.RequestedAtUtc.Value).TotalSeconds;
                    if (elapsed > 45.0 && !state.ExpiredCounted)
                    {
                        state.ExpiredCounted = true;
                        state.Active = false;
                        state.Completed = true;
                        RecordFailedRequest();
                    }
                }
            }

            // Rule 18: Không để nhu cầu mới ngắt quãng các hoạt ảnh OneShot đang diễn ra
            if (PetAIService.IsOneShotOrHighPriority(_pet.State)) return;

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

            // THIRST NEED: Kích hoạt khi IsThirsty (Energy < 45)
            if (_pet.IsThirsty)
            {
                if (!IsNeedActive(PetNeedTypes.Thirst) && !_pet.Needs[PetNeedTypes.Thirst].Completed)
                {
                    TriggerNeed(PetNeedTypes.Thirst, "Khát nước quá nè, cho mình bình sữa hoặc nước mát nhé! 🥛");
                    NotificationService.RequestNotification("thirst", "Thú cưng khát nước!", $"{_pet.Name} đang khát nước, hãy cho bé uống sữa hoặc nước nhé! 🥛");
                }
            }
            else if (_pet.Energy > 70)
            {
                _pet.Needs[PetNeedTypes.Thirst].Completed = false;
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

            if (_pet.Happiness < 70)
            {
                if (!IsNeedActive(PetNeedTypes.Play) && !_pet.Needs[PetNeedTypes.Play].Completed)
                {
                    TriggerNeed(PetNeedTypes.Play, "Buồn chán quá... Chơi bóng với mình một lát nhé! 🎾");
                }
            }
            else if (_pet.Happiness >= 85)
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
                    if (_pet.Energy < 70 && !_pet.Needs[PetNeedTypes.Thirst].Completed) validCandidates.Add(PetNeedTypes.Thirst);
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

        private double _clickInactivityTimer = 0.0;
        public const double InactivityThresholdSeconds = 3600.0;
        public double ClickInactivityTimer => _clickInactivityTimer;

        public void ResetInactivityTimer()
        {
            _clickInactivityTimer = 0.0;
        }

        public void UpdateInactivity(double deltaSeconds)
        {
            _clickInactivityTimer += deltaSeconds;

            if (_clickInactivityTimer >= InactivityThresholdSeconds)
            {
                // Chỉ kích hoạt Sad nếu pet đang ở trạng thái AI thông thường và không trong One-shot/Sleep (Rule 5 & 7)
                if (!PetAIService.IsOneShotOrHighPriority(_pet.State) && _pet.Cleanliness >= 35 && !_pet.IsSleepy)
                {
                    if (_pet.State != PetState.Sad)
                    {
                        _pet.State = PetState.Sad;
                        ShowEmote("Sen bỏ quên Mimi rồi sao... Buồn thiu luôn á... 😿💧", 4.0);
                        OnPropertyChanged(nameof(State));
                    }
                }
            }
        }

        public void CheckSickCondition()
        {
            // Trigger Sick ONCE when TotalPlayCount >= 50 for this episode
            if (_save.TotalPlayCount >= 50 && !_save.HasTriggered50PlaySick)
            {
                _save.HasTriggered50PlaySick = true;
                _pet.IsSick = true;
                _pet.SickPenaltyApplied = false;
                _pet.State = PetState.Sick;
                _sickHealthTimer = 0.0;
                _recoveryHealthTimer = 0.0;
                ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 4.0);
                OnPropertyChanged(nameof(State));
                RequestPlayAnimation?.Invoke("Sick");
                SaveService.Instance.SaveGame(_save);
            }
        }

        private void OnStatTick(object? sender, EventArgs e)
        {
            _statService.UpdateStatsTick(_pet, _species, 1.0);
            CheckNeedsUpdate(1.0);

            if (_pet.IsSick)
            {
                _sickHealthTimer += 1.0;
                if (_sickHealthTimer >= 300.0)
                {
                    _sickHealthTimer = 0.0;
                    _pet.Health = Math.Max(0.0, _pet.Health - 2.0);
                    if (_pet.Health <= 0.0 && !_pet.SickPenaltyApplied)
                    {
                        _pet.SickPenaltyApplied = true;
                        _pet.Hunger = Math.Max(0.0, _pet.Hunger * 0.5);
                        _pet.Happiness = Math.Max(0.0, _pet.Happiness * 0.5);
                        _pet.Energy = Math.Max(0.0, _pet.Energy * 0.5);
                        _pet.Cleanliness = Math.Max(0.0, _pet.Cleanliness * 0.5);
                        _pet.Affection = Math.Max(0.0, _pet.Affection * 0.5);
                    }
                }
            }
            else
            {
                // Post-cure recovery: +2 Health every 5 minutes until 100
                if (_pet.Health < 100.0)
                {
                    _recoveryHealthTimer += 1.0;
                    if (_recoveryHealthTimer >= 300.0)
                    {
                        _recoveryHealthTimer = 0.0;
                        _pet.Health = Math.Min(100.0, _pet.Health + 2.0);
                    }
                }
                else
                {
                    _recoveryHealthTimer = 0.0;
                }
            }

            NotifyStatProperties();
        }

        public void OnPetClicked()
        {
            if (_pet.IsSick || _pet.State == PetState.Sick)
            {
                ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 2.5);
                return;
            }

            // Reset timer không tương tác (Rule 5)
            ResetInactivityTimer();

            AudioService.Instance.PlayClick();
            _pet.Affection = Math.Min(100.0, _pet.Affection + 1.0);

            CheckAchievementProgress("affection_50", (int)_pet.Affection);
            CheckAchievementProgress("affection_100", (int)_pet.Affection);
            CheckQuestProgress("quest_petting", 1);

            // 50% Happy, 50% Dance (Rule 2)
            bool playHappy = _needRand.Next(2) == 0;
            if (playHappy)
            {
                _aiService.TriggerHappy(_pet);
                RequestPlayAnimation?.Invoke("Happy");
            }
            else
            {
                _aiService.TriggerDance(_pet);
                RequestPlayAnimation?.Invoke("Dance");
            }
            OnPropertyChanged(nameof(State));

            // KIỂM TRA NHU CẦU VUỐT VE / TƯƠNG TÁC (Quy tắc 1, 2, 3, 10)
            if (CompleteNeed(PetNeedTypes.Affection, 10, out int expGain, 5))
            {
                ShowEmote($"Thích được vuốt ve xoa đầu nhất! (+{expGain} EXP, +5 Xu) 🥰💖✨", 3.0);
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

        public InventoryItem? GetAvailableInventoryItem(string category, string? preferredId = null)
        {
            var validItems = _save.Inventory
                .Where(i => i.Quantity > 0)
                .Select(i => new { Inv = i, Def = DataManager.Instance.GetItem(i.ItemId) })
                .Where(x => x.Def != null && x.Def.Category == category)
                .ToList();

            if (category == "Food")
            {
                // Loại trừ đồ uống (sữa) khỏi danh sách thức ăn
                validItems = validItems.Where(x => x.Inv.ItemId != "milk" && !x.Def!.Name.Contains("Sữa", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(preferredId))
            {
                var preferred = validItems.FirstOrDefault(x => x.Inv.ItemId == preferredId);
                if (preferred != null) return preferred.Inv;
            }

            return validItems.FirstOrDefault()?.Inv;
        }

        public void FeedPet()
        {
            // PART 1 & 2: Kiểm tra thức ăn thực tế trong túi đồ
            var invFood = GetAvailableInventoryItem("Food", "fish");
            if (invFood == null || invFood.Quantity <= 0)
            {
                // Hết thức ăn: Không cho ăn, không phát animation, không tăng chỉ số, không hoàn thành nhiệm vụ/need
                ShowEmote("Đã hết thức ăn, hãy vào Cửa hàng để mua thêm.", 3.0);
                return;
            }

            var foodItem = DataManager.Instance.GetItem(invFood.ItemId);
            if (foodItem == null) return;

            // Tiêu hao đúng 1 món ăn từ túi đồ
            invFood.Quantity--;
            if (invFood.Quantity <= 0)
            {
                _save.Inventory.Remove(invFood);
            }
            SaveService.Instance.SaveGame(_save);
            TriggerInventoryChanged();

            // Áp dụng hiệu ứng thức ăn (bảo toàn chỉ số hồi đói)
            _statService.ApplyItemEffect(_pet, foodItem);
            _save.TotalFeedCount++;
            CheckAchievementProgress("first_feed", 1);
            CheckAchievementProgress("feed_50_times", 1);
            CheckQuestProgress("quest_feed", 1);
            AudioService.Instance.PlayFeed();

            // KÍCH HOẠT ANIMATION EAT: Chuyển sang Eat.png và phát tuần tự các frame
            _aiService.TriggerEat(_pet);
            PlayAnimation("Eat");

            // KIỂM TRA NHU CẦU ĂN:
            if (CompleteNeed(PetNeedTypes.Hunger, 15, out int expGain, 10))
            {
                ShowEmote($"Yum! {foodItem.Icon} Đang đói được ăn no thích quá! (+{expGain} EXP, +10 Xu) ✨💖", 5.0);
            }
            else
            {
                // Pet không yêu cầu ăn hoặc đã hoàn thành nhu cầu này rồi -> KHÔNG CỘNG EXP
                ShowEmote($"Yum! {foodItem.Icon} Măm măm ngon miệng!", 5.0);
            }

            NotifyStatProperties();
        }

        public void PlayWithPet()
        {
            // PART 6: Nếu đang ốm -> Chặn chơi đùa
            if (_pet.IsSick || _pet.State == PetState.Sick)
            {
                ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 2.5);
                return;
            }

            // PART 4: Kiểm tra đồ chơi thực tế trong túi đồ
            var invToy = GetAvailableInventoryItem("Toy", "ball");
            if (invToy == null || invToy.Quantity <= 0)
            {
                // Hết đồ chơi: Không chơi, không phát animation Play, không tăng TotalPlayCount, không hoàn thành need
                ShowEmote("Đã hết đồ chơi, hãy vào Cửa hàng để mua thêm.", 3.0);
                return;
            }

            var toyItem = DataManager.Instance.GetItem(invToy.ItemId);
            if (toyItem == null) return;

            // Tiêu hao đúng 1 đồ chơi từ túi đồ
            invToy.Quantity--;
            if (invToy.Quantity <= 0)
            {
                _save.Inventory.Remove(invToy);
            }
            SaveService.Instance.SaveGame(_save);
            TriggerInventoryChanged();

            _statService.ApplyItemEffect(_pet, toyItem);
            _save.TotalPlayCount++;
            CheckAchievementProgress("first_play", 1);
            CheckAchievementProgress("play_50_times", 1);
            CheckQuestProgress("quest_play", 1);
            CheckSickCondition();

            if (_pet.State != PetState.Sick)
            {
                _aiService.TriggerPlay(_pet);
                PlayAnimation("Play");
                AudioService.Instance.PlayHappy();
            }

            // KIỂM TRA NHU CẦU CHƠI:
            if (CompleteNeed(PetNeedTypes.Play, 20, out int expGain, 10))
            {
                ShowEmote($"Vui quá! {toyItem.Icon} Chơi đúng lúc thích mê! (+{expGain} EXP, +10 Xu) 🎾✨", 3.0);
            }
            else
            {
                if (_pet.State != PetState.Sick)
                {
                    ShowEmote($"Vui quá! {toyItem.Icon} Chơi đùa thích ghê!", 2.5);
                }
            }

            NotifyStatProperties();
        }

        public void DrinkPet()
        {
            if (_pet.IsSick || _pet.State == PetState.Sick)
            {
                ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 2.5);
                return;
            }

            var drinkItem = DataManager.Instance.GetItem("milk") ?? DataManager.Instance.ItemsList.FirstOrDefault(i => i.Id == "milk" || i.Name.Contains("Sữa", StringComparison.OrdinalIgnoreCase));

            if (drinkItem != null)
            {
                PetViewModel_ApplyItem(drinkItem);
            }
        }

        public void BathPet()
        {
            if (_pet.IsSick || _pet.State == PetState.Sick)
            {
                ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 2.5);
                return;
            }

            _pet.Cleanliness = 100.0;
            _pet.Happiness = Math.Min(100.0, _pet.Happiness + 10.0);
            _save.TotalBathCount++;
            CheckAchievementProgress("first_bath", 1);
            CheckQuestProgress("quest_bath", 1);

            _aiService.TriggerBath(_pet);
            OnPropertyChanged(nameof(State));
            RequestPlayAnimation?.Invoke("Bath");

            // KIỂM TRA NHU CẦU TẮM:
            if (CompleteNeed(PetNeedTypes.Bath, 15, out int expGain, 10))
            {
                ShowEmote($"Tắm mát thơm tho sạch sẽ! (+{expGain} EXP, +10 Xu) 🧼🫧✨", 3.0);
            }
            else
            {
                ShowEmote("Tắm mát thơm tho sạch sẽ! 🧼🫧✨", 2.5);
            }

            NotifyStatProperties();
        }

        public void ToggleSleep()
        {
            if (_pet.IsSick || _pet.State == PetState.Sick)
            {
                ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 2.5);
                return;
            }

            if (_pet.State == PetState.Sleep)
            {
                _aiService.TriggerWakeUp(_pet);
                ShowEmote("Dậy rồi nè! Chào bạn! ☀️", 1.8);
            }
            else
            {
                _aiService.TriggerSleep(_pet);
                // KIỂM TRA NHU CẦU NGỦ:
                if (CompleteNeed(PetNeedTypes.Sleep, 15, out int expGain, 10))
                {
                    ShowEmote($"Buồn ngủ được ngủ ngon quá! (+{expGain} EXP, +10 Xu) 💤✨", 3.0);
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
            _pet.State = PetState.Fall;
            OnPropertyChanged(nameof(State));
            RequestPlayAnimation?.Invoke("Fall");
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
            if (Enum.TryParse<PetState>(animationName, true, out var state))
            {
                _aiService.TriggerState(_pet, state);
                OnPropertyChanged(nameof(State));
                RequestPlayAnimation?.Invoke(animationName);
            }
        }

        public void OnAnimationCompleted(PetState finishedState)
        {
            if (_pet.State == finishedState)
            {
                if (finishedState == PetState.Bath)
                {
                    FinishBathAnimation();
                }
                else if (finishedState == PetState.Eat)
                {
                    FinishEatAnimation();
                }
                else
                {
                    _aiService.CompleteOneShotAnimation(_pet, finishedState);
                    OnPropertyChanged(nameof(State));
                }
            }
        }

        public void FinishBathAnimation()
        {
            if (_pet.State == PetState.Bath)
            {
                _pet.Cleanliness = 100.0; // Rule 4: Cleanliness restored, Dirty cleared immediately!
                _aiService.CompleteOneShotAnimation(_pet, PetState.Bath);
                OnPropertyChanged(nameof(State));
                NotifyStatProperties();
                SaveService.Instance.SaveGame(_save);
            }
        }

        public void FinishEatAnimation()
        {
            if (_pet.State == PetState.Eat)
            {
                if (_pet.IsSick)
                {
                    _pet.State = PetState.Sick;
                    OnPropertyChanged(nameof(State));
                    RequestPlayAnimation?.Invoke("Sick");
                }
                else
                {
                    _aiService.FinishEat(_pet);
                    OnPropertyChanged(nameof(State));
                }
            }
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _save.Coins += amount;
            OnPropertyChanged(nameof(Coins));
            SaveService.Instance.SaveGame(_save);
        }

        private void OnPetLevelUp(Pet pet, int bonusCoins)
        {
            if (bonusCoins > 0)
            {
                AddCoins(bonusCoins);
            }
            ShowEmote($"✨ LÊN CẤP {pet.Level}! Mimi vui vẻ hơn! {(bonusCoins > 0 ? $"+{bonusCoins} xu" : "")} ✨", 3.5);
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
                if (ach.RewardCoins > 0)
                {
                    AddCoins(ach.RewardCoins);
                }
                ShowEmote($"🏆 DANH HIỆU MỚI: {ach.Title}! {(ach.RewardCoins > 0 ? $"(+{ach.RewardCoins} xu) " : "")}✨", 4.0);
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
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsFacingLeft));
            AppearanceChanged?.Invoke();
        }

        public void PetViewModel_ApplyItem(Item item)
        {
            if (item == null) return;

            if (item.Category == "Medicine" || item.Id == "medicine")
            {
                if (!_pet.IsSick && _pet.State != PetState.Sick)
                {
                    ShowEmote("Mimi đang khỏe mạnh, không cần uống thuốc đâu! ✨", 2.5);
                    return;
                }

                _pet.IsSick = false;
                _pet.SickPenaltyApplied = false;
                _sickHealthTimer = 0.0;
                _recoveryHealthTimer = 0.0;
                _pet.State = PetState.Idle;

                _statService.ApplyItemEffect(_pet, item);
                ShowEmote("Cảm ơn Sen nhiều nha! Mimi đã khỏi ốm rồi nè, cảm thấy khỏe khoắn hẳn ra! ✨💖💊", 4.0);
                OnPropertyChanged(nameof(State));
                RequestPlayAnimation?.Invoke("Idle");
                SaveService.Instance.SaveGame(_save);
                NotifyStatProperties();
                return;
            }

            bool isDrink = item.Id == "milk" || item.Name.Contains("Sữa", StringComparison.OrdinalIgnoreCase);
            bool isToy = item.Category == "Toy";
            bool isFood = item.Category == "Food" && !isDrink;

            if (_pet.IsSick || _pet.State == PetState.Sick)
            {
                if (!isFood)
                {
                    ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 2.5);
                    return;
                }
            }

            _statService.ApplyItemEffect(_pet, item);

            if (isDrink)
            {
                _aiService.TriggerDrink(_pet);
                PlayAnimation("Drink");
                AudioService.Instance.PlayDrink();
                CheckQuestProgress("quest_drink", 1);
            }
            else if (isFood)
            {
                _aiService.TriggerEat(_pet);
                PlayAnimation("Eat");
                AudioService.Instance.PlayFeed();
                _save.TotalFeedCount++;
                CheckAchievementProgress("first_feed", 1);
                CheckAchievementProgress("feed_50_times", 1);
                CheckQuestProgress("quest_feed", 1);
            }
            else if (isToy)
            {
                _save.TotalPlayCount++;
                CheckAchievementProgress("first_play", 1);
                CheckAchievementProgress("play_50_times", 1);
                CheckSickCondition();
                CheckQuestProgress("quest_play", 1);

                if (_pet.State != PetState.Sick)
                {
                    _aiService.TriggerPlay(_pet);
                    PlayAnimation("Play");
                    AudioService.Instance.PlayHappy();
                }
            }

            int expGain = 0;
            bool needCompleted = false;

            if (isDrink && CompleteNeed(PetNeedTypes.Thirst, 15, out expGain, 10))
            {
                needCompleted = true;
                ShowEmote($"Đã khát rồi! {item.Icon} Cảm ơn bạn! (+{expGain} EXP, +10 Xu) 🥛✨", 3.0);
            }
            else if (isFood && CompleteNeed(PetNeedTypes.Hunger, 15, out expGain, 10))
            {
                needCompleted = true;
                ShowEmote($"Yum! {item.Icon} Đang đói được ăn no nê! (+{expGain} EXP, +10 Xu) ✨💖", 5.0);
            }
            else if (isToy && CompleteNeed(PetNeedTypes.Play, 20, out expGain, 10))
            {
                needCompleted = true;
                ShowEmote($"Thích quá! {item.Icon} Chơi đúng lúc mê ghê! (+{expGain} EXP, +10 Xu) 🎾✨", 3.0);
            }

            if (!needCompleted)
            {
                // Dùng item khi pet không yêu cầu -> Không cộng EXP
                if (isDrink)
                {
                    ShowEmote($"Uống ừng ực ngon quá! {item.Icon} ✨", 3.0);
                }
                else if (isFood)
                {
                    ShowEmote($"Yum! {item.Icon} Măm măm ngon miệng!", 5.0);
                }
                else if (isToy)
                {
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
        public void OpenCollection() => RequestOpenCollection?.Invoke();
        public void OpenSettings() => RequestOpenSettings?.Invoke();
    }
}
