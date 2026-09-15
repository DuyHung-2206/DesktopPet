using System;
using System.Drawing;
using DesktopPet.Models;

namespace DesktopPet.Services
{
    public class PetAIService
    {
        private readonly Random _rand = new();
        private double _stateTimer = 0.0;
        private double _targetX = 0.0;
        private double _targetY = 0.0;
        private double _happyElapsed = 0.0;
        private double _happyBaseY = 0.0;
        private int _happyInitialDirection = 1; // 1: nhảy sang phải, -1: nhảy sang trái

        // Ngưỡng vận tốc thả kéo nhanh để kích hoạt rơi (DIP/giây)
        public static double FastDragReleaseThreshold { get; set; } = 650.0;

        public static double CalculateDragVelocity(double displacementDip, double elapsedSeconds)
        {
            if (elapsedSeconds <= 0.001) return 0.0;
            return displacementDip / elapsedSeconds;
        }

        public static bool IsFastDragRelease(double velocityDipPerSec) => velocityDipPerSec >= FastDragReleaseThreshold;

        // Trạng thái rơi tự do (Physics)
        public bool IsFalling { get; set; } = false;
        private double _verticalVelocity = 0.0;
        private const double Gravity = 900.0; // px/s^2

        public void StartFalling(Pet pet)
        {
            IsFalling = true;
            _verticalVelocity = 0.0;
            pet.State = PetState.Fall;
        }

        // Vật phẩm mục tiêu cần chạy đến (thức ăn / đồ chơi rơi trên desktop)
        public PointF? FoodTarget { get; set; }

        public static double EatDurationSeconds => AnimationRegistry.GetAuthoritativeDurationSeconds(PetState.Eat);
        public static double BathDurationSeconds => AnimationRegistry.GetAuthoritativeDurationSeconds(PetState.Bath);
        public static double HappyDurationSeconds => AnimationRegistry.GetAuthoritativeDurationSeconds(PetState.Happy);

        public bool IsPaused { get; set; } = false;

        public void UpdateAI(Pet pet, PetSpecies? species, double deltaTime, int screenIndex, double petWidth, double petHeight)
        {
            if (pet == null || IsPaused) return;

            var groundY = ViewportService.GetGroundY(petHeight, screenIndex);
            var workArea = ViewportService.GetWorkingArea(screenIndex);

            // 1. XỬ LÝ VẬT LÝ RƠI (FALL & GRAVITY)
            if (IsFalling)
            {
                pet.State = PetState.Fall;
                _verticalVelocity += Gravity * deltaTime;
                pet.Y += _verticalVelocity * deltaTime;

                if (pet.Y >= groundY)
                {
                    pet.Y = groundY;
                    _verticalVelocity = 0.0;
                    IsFalling = false;
                    TriggerHurt(pet);
                }

                (pet.X, pet.Y) = ViewportService.ClampPosition(pet.X, pet.Y, petWidth, petHeight, screenIndex);
                return;
            }

            // 2. NẾU CÓ THỨC ĂN HOẶC ĐỒ CHƠI ĐANG RƠI TRÊN MÀN HÌNH -> CHẠY ĐẾN ĐÓ
            if (FoodTarget.HasValue)
            {
                var target = FoodTarget.Value;
                var dist = target.X - pet.X;
                var speed = (species?.Speed ?? 2.0) * 120.0 * deltaTime;

                if (Math.Abs(dist) > 20.0)
                {
                    pet.State = PetState.Run;
                    pet.IsFacingLeft = dist < 0;
                    pet.X += Math.Sign(dist) * Math.Min(speed, Math.Abs(dist));
                }
                else
                {
                    // Đã đến chỗ thức ăn!
                    FoodTarget = null;
                    TriggerEat(pet);
                    AudioService.Instance.PlayFeed();
                }

                (pet.X, pet.Y) = ScreenService.ClampToScreen(pet.X, pet.Y, petWidth, petHeight, screenIndex);
                return;
            }

            // 3. XỬ LÝ THEO TRẠNG THÁI HIỆN TẠI
            _stateTimer -= deltaTime;

            switch (pet.State)
            {
                case PetState.Idle:
                    if (_stateTimer <= 0)
                    {
                        DecideNextAction(pet, workArea, petWidth, petHeight);
                    }
                    break;

                case PetState.Walk:
                case PetState.Run:
                    var moveSpeed = (species?.Speed ?? 2.0) * (pet.State == PetState.Run ? 100.0 : 60.0) * deltaTime;
                    var dx = _targetX - pet.X;
                    var dy = _targetY - pet.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist <= 5.0 || _stateTimer <= 0)
                    {
                        // Đã đến điểm đích hoặc hết giờ đi -> Chuyển sang đứng yên (Idle)
                        pet.State = PetState.Idle;
                        _stateTimer = _rand.Next(30, 45);
                    }
                    else
                    {
                        if (Math.Abs(dx) > 1.0)
                        {
                            pet.IsFacingLeft = dx < 0;
                        }
                        var step = Math.Min(moveSpeed, dist);
                        pet.X += (dx / dist) * step;
                        pet.Y += (dy / dist) * step;
                    }
                    break;

                case PetState.Sit:
                case PetState.Sad:
                    if (_stateTimer <= 0)
                    {
                        pet.State = GetDefaultNextState(pet);
                        _stateTimer = _rand.Next(25, 40);
                    }
                    break;

                case PetState.Dirty:
                    // Trong khi Cleanliness < 35, duy trì trạng thái Dirty (Rule 4)
                    if (pet.Cleanliness >= 35 && _stateTimer <= 0)
                    {
                        pet.State = PetState.Idle;
                        _stateTimer = _rand.Next(25, 40);
                    }
                    break;

                case PetState.Sick:
                    // Sick là trạng thái lặp
                    if (!pet.IsSick && _stateTimer <= 0)
                    {
                        pet.State = PetState.Idle;
                        _stateTimer = _rand.Next(25, 40);
                    }
                    break;

                case PetState.Sleep:
                    // Đang ngủ, duy trì cho đến khi người dùng đánh thức hoặc năng lượng đầy
                    break;

                case PetState.Eat:
                case PetState.Drink:
                case PetState.Bath:
                case PetState.WakeUp:
                case PetState.Hurt:
                case PetState.Play:
                case PetState.Dance:
                case PetState.Jump:
                case PetState.Angry:
                    // Hoạt ảnh OneShot do renderer làm chủ và phát sự kiện kết thúc.
                    // _stateTimer ở đây đóng vai trò watchdog an toàn.
                    if (_stateTimer <= 0)
                    {
                        var def = AnimationRegistry.GetDefinition(pet.State);
                        var nextState = def.CompletionState ?? PetState.Idle;
                        if (nextState == PetState.Idle)
                        {
                            nextState = GetDefaultNextState(pet);
                        }
                        pet.State = nextState;
                        _stateTimer = _rand.Next(20, 35);
                    }
                    break;

                case PetState.Fall:
                    // Do khối vật lý IsFalling phía trên điều khiển
                    break;

                case PetState.Happy:
                    _happyElapsed += deltaTime;

                    // Kiểm tra biên màn hình ở nhịp đầu tiên để hướng nhảy không đâm vào mép
                    if (_happyElapsed <= deltaTime * 2.0)
                    {
                        if (pet.X + 80 > workArea.Right - 50)
                            _happyInitialDirection = -1;
                        else if (pet.X - 80 < workArea.Left + 50)
                            _happyInitialDirection = 1;
                    }

                    const double cycleDuration = 0.72; // 6 frames x 120ms mỗi chu kỳ hoạt ảnh Happy
                    int cycleIndex = (int)(_happyElapsed / cycleDuration);
                    double cycleTime = _happyElapsed - (cycleIndex * cycleDuration);

                    // Đổi hướng nhảy qua nhảy lại giữa các chu kỳ
                    int currentDir = (cycleIndex % 2 == 0) ? _happyInitialDirection : -_happyInitialDirection;
                    pet.IsFacingLeft = (currentDir < 0);

                    // Khoảng thời gian trên không (Airborne) trong mỗi chu kỳ 0.72s:
                    if (cycleTime >= 0.12 && cycleTime <= 0.50)
                    {
                        double airProgress = (cycleTime - 0.12) / (0.50 - 0.12); // 0.0 -> 1.0
                        double jumpSpeed = Math.Sin(airProgress * Math.PI) * 170.0; // Vận tốc ngang hình sin
                        pet.X += currentDir * jumpSpeed * deltaTime;

                        // Độ cao nhảy vòng cung lên khỏi vị trí đặt (12 DIP)
                        double jumpArc = Math.Sin(airProgress * Math.PI) * 12.0;
                        pet.Y = _happyBaseY - jumpArc;
                    }
                    else
                    {
                        pet.Y = _happyBaseY;
                    }

                    if (_stateTimer <= 0)
                    {
                        pet.Y = _happyBaseY;
                        pet.State = PetState.Idle;
                        _stateTimer = _rand.Next(25, 40);
                    }
                    break;

                default:
                    if (_stateTimer <= 0)
                    {
                        pet.State = PetState.Idle;
                        _stateTimer = 25.0;
                    }
                    break;
            }

            (pet.X, pet.Y) = ViewportService.ClampPosition(pet.X, pet.Y, petWidth, petHeight, screenIndex);
        }

        public void DecideNextAction(Pet pet, Rectangle workArea = default, double petWidth = 70.0, double petHeight = 70.0)
        {
            // Rule 7 & 17: Không ngắt quãng trạng thái One-Shot hoặc trạng thái ưu tiên cao
            if (IsOneShotOrHighPriority(pet.State)) return;

            // Rule 8: Nếu pet đang ốm -> Khóa toàn bộ hành vi bình thường, chỉ duy trì trạng thái Sick
            if (pet.IsSick)
            {
                pet.State = PetState.Sick;
                _stateTimer = 30.0;
                return;
            }

            if (workArea.Width <= 0 || workArea.Height <= 0)
            {
                workArea = ViewportService.GetWorkingArea(0);
            }

            // 1. Kiểm tra trạng thái suy giảm chỉ số / hành vi ưu tiên
            if (pet.IsSleepy)
            {
                pet.State = PetState.Sleep;
                AudioService.Instance.PlaySleep();
                return;
            }

            if (pet.Cleanliness < 35)
            {
                // Dirty is directly controlled by Cleanliness < 35 (Rule 4)
                pet.State = PetState.Dirty;
                _stateTimer = _rand.Next(15, 30);
                return;
            }

            // Ngẫu nhiên chọn hành động tiếp theo
            var roll = _rand.Next(100);

            if (roll < 45)
            {
                // Đi dạo tự do trên desktop (Walk - 2D Desktop Roaming)
                pet.State = PetState.Walk;
                var minX = workArea.Left + ViewportService.SafeMargin;
                var maxX = workArea.Right - petWidth - ViewportService.SafeMargin;
                if (maxX < minX) maxX = minX;
                _targetX = _rand.Next((int)minX, (int)maxX + 1);

                var minY = workArea.Top + ViewportService.SafeMargin;
                var maxY = workArea.Bottom - petHeight - ViewportService.SafeMargin;
                if (maxY < minY) maxY = minY;
                _targetY = _rand.Next((int)minY, (int)maxY + 1);

                _stateTimer = _rand.Next(4, 9);
            }
            else if (roll < 70)
            {
                // Ngồi nghỉ ngơi (Sit)
                pet.State = PetState.Sit;
                _stateTimer = _rand.Next(4, 10);
            }
            else if (roll < 85)
            {
                // Hào hứng nhảy nhót (Happy)
                TriggerHappy(pet);
            }
            else
            {
                // Đứng quan sát nhìn người dùng (Idle)
                pet.State = PetState.Idle;
                _stateTimer = _rand.Next(30, 45);
            }
        }

        public PetState GetDefaultNextState(Pet pet)
        {
            if (pet.IsSick) return PetState.Sick;
            if (pet.Cleanliness < 35) return PetState.Dirty;
            return PetState.Idle;
        }

        public static bool IsOneShotOrHighPriority(PetState state)
        {
            return state is PetState.Fall or PetState.Hurt or PetState.Bath or PetState.Eat
                or PetState.Drink or PetState.WakeUp or PetState.Happy or PetState.Dance
                or PetState.Play or PetState.Jump or PetState.Angry or PetState.Sleep;
        }

        public void CompleteOneShotAnimation(Pet pet, PetState finishedState)
        {
            if (pet == null) return;
            if (pet.State == finishedState)
            {
                var def = AnimationRegistry.GetDefinition(finishedState);
                var nextState = def.CompletionState ?? PetState.Idle;
                if (nextState == PetState.Idle)
                {
                    nextState = GetDefaultNextState(pet);
                }
                pet.State = nextState;
                _stateTimer = _rand.Next(20, 35);
                _targetX = pet.X;
                _targetY = pet.Y;
            }
        }

        public void TriggerState(Pet pet, PetState state, double customDuration = -1)
        {
            if (pet == null) return;
            pet.State = state;
            _targetX = pet.X;
            _targetY = pet.Y;
            if (state == PetState.Happy)
            {
                _happyBaseY = pet.Y;
            }

            var def = AnimationRegistry.GetDefinition(state);
            double baseDuration = customDuration > 0 ? customDuration : def.TotalDurationSeconds;
            // Watchdog dự phòng thêm 1.0 giây để luôn nhường quyền cho Renderer completion event
            _stateTimer = baseDuration + 1.0;

            switch (state)
            {
                case PetState.Eat:
                    AudioService.Instance.PlayFeed();
                    break;
                case PetState.Drink:
                    AudioService.Instance.PlayDrink();
                    break;
                case PetState.Play:
                    AudioService.Instance.PlayHappy();
                    break;
                case PetState.Bath:
                    AudioService.Instance.PlayBath();
                    break;
                case PetState.Happy:
                    _happyElapsed = 0.0;
                    _happyInitialDirection = pet.IsFacingLeft ? -1 : 1;
                    AudioService.Instance.PlayHappy();
                    break;
                case PetState.Dance:
                    AudioService.Instance.PlayHappy();
                    break;
                case PetState.Hurt:
                    AudioService.Instance.PlayHurt();
                    break;
                case PetState.Sleep:
                    AudioService.Instance.PlaySleep();
                    break;
                case PetState.WakeUp:
                    AudioService.Instance.PlayWakeUp();
                    break;
            }
        }

        public void TriggerEat(Pet pet, double durationSeconds = -1)
        {
            TriggerState(pet, PetState.Eat, durationSeconds);
        }

        public void TriggerDrink(Pet pet, double durationSeconds = -1)
        {
            TriggerState(pet, PetState.Drink, durationSeconds);
        }

        public void TriggerPlay(Pet pet, double durationSeconds = -1)
        {
            TriggerState(pet, PetState.Play, durationSeconds);
        }

        public void TriggerBath(Pet pet, double durationSeconds = -1)
        {
            TriggerState(pet, PetState.Bath, durationSeconds);
        }

        public void FinishEat(Pet pet)
        {
            CompleteOneShotAnimation(pet, PetState.Eat);
        }

        public void TriggerHappy(Pet pet)
        {
            TriggerState(pet, PetState.Happy);
        }

        public void TriggerDance(Pet pet)
        {
            TriggerState(pet, PetState.Dance);
        }

        public void TriggerHurt(Pet pet)
        {
            TriggerState(pet, PetState.Hurt);
        }

        public void TriggerSleep(Pet pet)
        {
            pet.State = PetState.Sleep;
            AudioService.Instance.PlaySleep();
        }

        public void TriggerWakeUp(Pet pet)
        {
            TriggerState(pet, PetState.WakeUp);
        }
    }
}
