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
        private double _happyElapsed = 0.0;
        private int _happyInitialDirection = 1; // 1: nhảy sang phải, -1: nhảy sang trái

        // Trạng thái rơi tự do (Physics)
        public bool IsFalling { get; set; } = false;
        private double _verticalVelocity = 0.0;
        private const double Gravity = 900.0; // px/s^2

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
                    pet.State = PetState.Idle;
                    _stateTimer = 1.5; // Dừng lại thở sau cú rơi
                    AudioService.Instance.PlayHappy();
                }

                (pet.X, pet.Y) = ViewportService.ClampPosition(pet.X, pet.Y, petWidth, petHeight, screenIndex);
                return;
            }

            // Đảm bảo pet không lơ lửng nếu không rơi (trừ khi đang nhảy Jump, Fall hoặc Happy)
            if (pet.Y < groundY && pet.State != PetState.Jump && pet.State != PetState.Fall && pet.State != PetState.Happy)
            {
                pet.Y = groundY;
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

                    if (Math.Abs(dx) <= 5.0 || _stateTimer <= 0)
                    {
                        // Đã đến điểm đích hoặc hết giờ đi -> Chuyển sang đứng yên (Idle)
                        pet.State = PetState.Idle;
                        _stateTimer = _rand.Next(30, 45);
                    }
                    else
                    {
                        pet.IsFacingLeft = dx < 0;
                        pet.X += Math.Sign(dx) * Math.Min(moveSpeed, Math.Abs(dx));
                    }
                    break;

                case PetState.Sit:
                case PetState.Dirty:
                case PetState.Sick:
                case PetState.Sad:
                    if (_stateTimer <= 0)
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
                        pet.State = def.CompletionState ?? PetState.Idle;
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

                        // Độ cao nhảy vòng cung lên khỏi mặt đất (12 DIP)
                        double jumpArc = Math.Sin(airProgress * Math.PI) * 12.0;
                        pet.Y = groundY - jumpArc;
                    }
                    else
                    {
                        pet.Y = groundY;
                    }

                    if (_stateTimer <= 0)
                    {
                        pet.Y = groundY;
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

        private void DecideNextAction(Pet pet, Rectangle workArea, double petWidth, double petHeight)
        {
            // Nếu quá mệt mỏi, tự động đi ngủ
            if (pet.IsSleepy)
            {
                pet.State = PetState.Sleep;
                AudioService.Instance.PlaySleep();
                return;
            }

            // Ngẫu nhiên chọn hành động tiếp theo
            var roll = _rand.Next(100);

            if (roll < 45)
            {
                // Đi dạo (Walk)
                pet.State = PetState.Walk;
                var minX = workArea.Left + ViewportService.SafeMargin;
                var maxX = workArea.Right - petWidth - ViewportService.SafeMargin;
                if (maxX < minX) maxX = minX;
                _targetX = _rand.Next((int)minX, (int)maxX + 1);
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

        public void CompleteOneShotAnimation(Pet pet, PetState finishedState)
        {
            if (pet == null) return;
            if (pet.State == finishedState)
            {
                var def = AnimationRegistry.GetDefinition(finishedState);
                pet.State = def.CompletionState ?? PetState.Idle;
                _stateTimer = _rand.Next(20, 35);
                _targetX = pet.X;
            }
        }

        public void TriggerState(Pet pet, PetState state, double customDuration = -1)
        {
            if (pet == null) return;
            pet.State = state;
            _targetX = pet.X;

            var def = AnimationRegistry.GetDefinition(state);
            double baseDuration = customDuration > 0 ? customDuration : def.TotalDurationSeconds;
            // Watchdog dự phòng thêm 1.0 giây để luôn nhường quyền cho Renderer completion event
            _stateTimer = baseDuration + 1.0;

            switch (state)
            {
                case PetState.Eat:
                    AudioService.Instance.PlayFeed();
                    break;
                case PetState.Bath:
                    AudioService.Instance.PlayBath();
                    break;
                case PetState.Happy:
                    _happyElapsed = 0.0;
                    _happyInitialDirection = pet.IsFacingLeft ? -1 : 1;
                    AudioService.Instance.PlayHappy();
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
