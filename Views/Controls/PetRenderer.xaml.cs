using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Models;

namespace DesktopPet.Views.Controls
{
    public partial class PetRenderer : System.Windows.Controls.UserControl
    {
        private Storyboard? _breatheAnim;
        private Storyboard? _walkAnim;
        private Storyboard? _blinkAnim;
        private Storyboard? _tailAnim;
        private readonly DispatcherTimer _blinkTimer = new();
        private readonly DispatcherTimer _spriteTimer = new();
        private readonly Random _rand = new();

        private PetState _currentState = PetState.Idle;

        private BitmapSource? _currentSpriteStrip;
        private int _frameCount = 1;
        private int _currentFrameIndex = 0;
        private string? _currentLoadedFile;
        private string _speciesFolder = "Cat";
        private DateTime _eatStartTime = DateTime.MinValue;

        public PetRenderer()
        {
            InitializeComponent();

            _breatheAnim = TryFindResource("BreatheAnimation") as Storyboard;
            _walkAnim = TryFindResource("WalkAnimation") as Storyboard;
            _blinkAnim = TryFindResource("BlinkAnimation") as Storyboard;
            _tailAnim = TryFindResource("TailWagAnimation") as Storyboard;

            _breatheAnim?.Begin();
            _tailAnim?.Begin();

            // Timer hoạt ảnh frame sprite sheet (8-10 FPS)
            _spriteTimer.Tick += OnSpriteTimerTick;

            // Timer chớp mắt ngẫu nhiên cho vector fallback
            _blinkTimer.Interval = TimeSpan.FromSeconds(3.5);
            _blinkTimer.Tick += (s, e) =>
            {
                if (_currentState != PetState.Sleep)
                {
                    _blinkAnim?.Begin();
                }
                _blinkTimer.Interval = TimeSpan.FromSeconds(_rand.Next(3, 7));
            };
            _blinkTimer.Start();
        }

        public Action<PetState>? OnAnimationCompleted;

        private void OnSpriteTimerTick(object? sender, EventArgs e)
        {
            if (_currentSpriteStrip == null || _frameCount <= 1) return;

            // Xử lý animation Eat: Đúng 5 frame, mỗi frame hiển thị đúng 1.0 giây (tổng cộng 5.0 giây theo sơ đồ)
            if (_currentState == PetState.Eat)
            {
                if (_currentFrameIndex >= _frameCount - 1)
                {
                    _spriteTimer.Stop();
                    RenderCurrentFrame();
                    OnAnimationCompleted?.Invoke(PetState.Eat);
                    return;
                }

                _currentFrameIndex++;
                RenderCurrentFrame();
                return;
            }

            _currentFrameIndex = (_currentFrameIndex + 1) % _frameCount;
            RenderCurrentFrame();
        }

        private void RenderCurrentFrame()
        {
            if (_currentSpriteStrip == null) return;

            if (_frameCount <= 1)
            {
                SpriteImage.Source = _currentSpriteStrip;
                return;
            }

            int frameWidth = (int)(_currentSpriteStrip.PixelWidth / _frameCount);
            int frameHeight = (int)_currentSpriteStrip.PixelHeight;
            int x = _currentFrameIndex * frameWidth;

            if (x + frameWidth <= _currentSpriteStrip.PixelWidth)
            {
                var crop = new CroppedBitmap(_currentSpriteStrip, new Int32Rect(x, 0, frameWidth, frameHeight));
                SpriteImage.Source = crop;
            }
        }

        public void UpdateAppearance(Pet pet, PetSpecies? species)
        {
            if (pet == null) return;

            // 1. Kiểm tra Sprite ảnh ngoài nếu có trong Assets/Pets/[Loài]/[State].png
            _speciesFolder = species?.Name ?? "Cat";
            var spriteFilePath = ResolveSpriteFilePath(_speciesFolder, pet.State);

            if (!string.IsNullOrEmpty(spriteFilePath))
            {
                // Nếu đang ở cùng trạng thái và đã nạp sprite này rồi -> Chỉ lật hướng nếu cần, không nạp lại làm reset animation
                if (_currentState == pet.State && _currentLoadedFile == spriteFilePath && _currentSpriteStrip != null)
                {
                    ApplyFlip(pet.IsFacingLeft);
                    return;
                }

                bool success = LoadSpriteAnimation(spriteFilePath, pet.State);
                if (success)
                {
                    SpriteImage.Visibility = Visibility.Visible;
                    VectorPetCanvas.Visibility = Visibility.Collapsed;
                    _walkAnim?.Stop();
                    RootRotate.Angle = 0;
                    BodyTranslate.Y = 0;
                    ApplyFlip(pet.IsFacingLeft);
                    return;
                }
            }

            // Fallback: Dùng vector pet nếu không có file ảnh sprite
            _spriteTimer.Stop();
            _currentSpriteStrip = null;
            _currentLoadedFile = null;
            SpriteImage.Visibility = Visibility.Collapsed;
            VectorPetCanvas.Visibility = Visibility.Visible;

            // 2. Màu sắc cơ bản & loài
            var baseColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(species?.BaseColor ?? "#FFA726");
            var secColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(species?.SecondaryColor ?? "#FFE082");

            var baseBrush = new SolidColorBrush(baseColor);
            var secBrush = new SolidColorBrush(secColor);

            TailPath.Fill = baseBrush;
            LeftEarPath.Fill = baseBrush;
            RightEarPath.Fill = baseBrush;
            LeftInnerEar.Fill = secBrush;
            RightInnerEar.Fill = secBrush;

            // 3. Hình dáng tai mèo nhọn tam giác & râu mèo Mimi
            LeftEarPath.Data = Geometry.Parse("M 15,25 L 25,2 L 38,22 Z");
            LeftInnerEar.Data = Geometry.Parse("M 18,23 L 25,7 L 34,21 Z");
            RightEarPath.Data = Geometry.Parse("M 52,22 L 65,2 L 75,25 Z");
            RightInnerEar.Data = Geometry.Parse("M 56,21 L 65,7 L 72,23 Z");
            WhiskersPath.Visibility = Visibility.Visible;

            // 4. Trạng thái hoạt ảnh
            ApplyState(pet.State);

            // 5. Hướng quay mặt
            ApplyFlip(pet.IsFacingLeft);

            // 6. Phụ kiện trang phục
            UpdateAccessories(pet);
        }

        private void ApplyState(PetState state)
        {
            _currentState = state;

            // Xử lý mắt mở / nhắm
            if (state == PetState.Sleep)
            {
                LeftEyeOpen.Visibility = Visibility.Collapsed;
                RightEyeOpen.Visibility = Visibility.Collapsed;
                EyesClosedPath.Visibility = Visibility.Visible;
                BathBubblesCanvas.Visibility = Visibility.Collapsed;
                _walkAnim?.Stop();
            }
            else if (state == PetState.Happy || state == PetState.Eat)
            {
                LeftEyeOpen.Visibility = Visibility.Collapsed;
                RightEyeOpen.Visibility = Visibility.Collapsed;
                EyesClosedPath.Visibility = Visibility.Visible;
                BathBubblesCanvas.Visibility = Visibility.Collapsed;
                _walkAnim?.Stop();
            }
            else
            {
                LeftEyeOpen.Visibility = Visibility.Visible;
                RightEyeOpen.Visibility = Visibility.Visible;
                EyesClosedPath.Visibility = Visibility.Collapsed;
            }

            // Hiệu ứng tắm
            BathBubblesCanvas.Visibility = state == PetState.Bath ? Visibility.Visible : Visibility.Collapsed;

            // Hiệu ứng bước đi / chạy
            if (state == PetState.Walk || state == PetState.Run)
            {
                _walkAnim?.Begin();
            }
            else
            {
                _walkAnim?.Stop();
                RootRotate.Angle = 0;
                BodyTranslate.Y = 0;
            }
        }

        private void ApplyFlip(bool isFacingLeft)
        {
            FlipScale.ScaleX = isFacingLeft ? -1.0 : 1.0;
        }

        private void UpdateAccessories(Pet pet)
        {
            // Head
            if (pet.EquippedItems.TryGetValue("Head", out var headId))
            {
                var item = Services.DataManager.Instance.GetItem(headId);
                HeadAccessoryText.Text = item?.Icon ?? "";
                HeadAccessoryText.Visibility = Visibility.Visible;
            }
            else
            {
                HeadAccessoryText.Visibility = Visibility.Collapsed;
            }

            // Eyes
            if (pet.EquippedItems.TryGetValue("Eyes", out var eyesId))
            {
                var item = Services.DataManager.Instance.GetItem(eyesId);
                EyesAccessoryText.Text = item?.Icon ?? "";
                EyesAccessoryText.Visibility = Visibility.Visible;
            }
            else
            {
                EyesAccessoryText.Visibility = Visibility.Collapsed;
            }

            // Back
            if (pet.EquippedItems.TryGetValue("Back", out var backId))
            {
                var item = Services.DataManager.Instance.GetItem(backId);
                BackAccessoryText.Text = item?.Icon ?? "";
                BackAccessoryText.Visibility = Visibility.Visible;
            }
            else
            {
                BackAccessoryText.Visibility = Visibility.Collapsed;
            }
        }

        public bool PlayAnimation(string animationName)
        {
            if (Enum.TryParse<PetState>(animationName, true, out var state))
            {
                _currentState = state;
                var spriteFilePath = ResolveSpriteFilePath(_speciesFolder, state);
                if (!string.IsNullOrEmpty(spriteFilePath))
                {
                    if (state == PetState.Eat)
                    {
                        LoadSpriteAnimation(spriteFilePath, state);
                        _currentFrameIndex = 0;
                        _spriteTimer.Interval = TimeSpan.FromMilliseconds(1000);
                        RenderCurrentFrame();
                        _spriteTimer.Start();
                        SpriteImage.Visibility = Visibility.Visible;
                        VectorPetCanvas.Visibility = Visibility.Collapsed;
                        _walkAnim?.Stop();
                        RootRotate.Angle = 0;
                        BodyTranslate.Y = 0;
                        return true;
                    }

                    bool success = LoadSpriteAnimation(spriteFilePath, state);
                    if (success)
                    {
                        SpriteImage.Visibility = Visibility.Visible;
                        VectorPetCanvas.Visibility = Visibility.Collapsed;
                        _walkAnim?.Stop();
                        RootRotate.Angle = 0;
                        BodyTranslate.Y = 0;
                        return true;
                    }
                }
            }
            return false;
        }

        private string? ResolveSpriteFilePath(string speciesFolder, PetState state)
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Pets", speciesFolder),
                Path.Combine(Environment.CurrentDirectory, "Assets", "Pets", speciesFolder),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Pets", speciesFolder)
            };

            string? baseDir = null;
            foreach (var dir in candidates)
            {
                if (Directory.Exists(dir))
                {
                    baseDir = dir;
                    break;
                }
            }

            if (string.IsNullOrEmpty(baseDir)) return null;

            // 1. Khớp chính xác tên trạng thái (Idle.png, Walk.png, Run.png, Eat.png, Bath.png, Sleep.png, Happy.png, Hurt.png)
            var direct = Path.Combine(baseDir, $"{state}.png");
            if (File.Exists(direct)) return direct;

            // 2. Ghép trạng thái tương tự
            string mappedName = state switch
            {
                PetState.Drink => "Eat.png",
                PetState.Jump => "Run.png",
                PetState.Play or PetState.Dance => "Happy.png",
                PetState.Hurt or PetState.Sad or PetState.Angry or PetState.Sick or PetState.Fall => "Hurt.png",
                PetState.Dirty => "Bath.png",
                PetState.Sit or PetState.WakeUp => "Idle.png",
                _ => "Idle.png"
            };

            var mapped = Path.Combine(baseDir, mappedName);
            if (File.Exists(mapped)) return mapped;

            // 3. Fallback mặc định về Idle.png
            var idle = Path.Combine(baseDir, "Idle.png");
            return File.Exists(idle) ? idle : null;
        }

        private bool LoadSpriteAnimation(string filePath, PetState state)
        {
            _currentState = state;

            if (state == PetState.Eat)
            {
                _eatStartTime = DateTime.UtcNow;
            }

            if (_currentLoadedFile == filePath && _currentSpriteStrip != null)
            {
                if (!_spriteTimer.IsEnabled && _frameCount > 1)
                {
                    _currentFrameIndex = 0;
                    RenderCurrentFrame();
                    _spriteTimer.Start();
                }
                return true;
            }

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(filePath, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.None;
                bi.EndInit();
                bi.Freeze();

                _currentSpriteStrip = bi;
                _currentLoadedFile = filePath;

                // Mỗi frame có kích thước vuông theo chiều cao (chuẩn 64x64)
                int cellDim = (int)bi.PixelHeight > 0 ? (int)bi.PixelHeight : 64;
                _frameCount = Math.Max(1, (int)(bi.PixelWidth / cellDim));
                _currentFrameIndex = 0;

                // Tốc độ khung hình (Frame rate) tối ưu theo từng hành động
                int intervalMs = state switch
                {
                    PetState.Idle or PetState.Sit or PetState.WakeUp => 10000, // 10 giây giữa 3 hành động (Frame 0 -> Frame 1 -> Frame 2) theo yêu cầu
                    PetState.Run => 95,
                    PetState.Walk => 125,
                    PetState.Sleep => 240,
                    PetState.Eat or PetState.Drink => 1000, // Đúng 1.0 giây mỗi frame theo chuẩn sơ đồ (0.0s, 1.0s, 2.0s, 3.0s, 4.0s -> 5.0s chuyển Idle)
                    PetState.Bath => 130,
                    PetState.Happy or PetState.Play or PetState.Dance => 120,
                    PetState.Hurt or PetState.Sick or PetState.Sad or PetState.Angry => 150,
                    _ => 140
                };

                _spriteTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
                RenderCurrentFrame();

                if (_frameCount > 1)
                {
                    _spriteTimer.Start();
                }
                else
                {
                    _spriteTimer.Stop();
                }

                return true;
            }
            catch
            {
                _spriteTimer.Stop();
                _currentSpriteStrip = null;
                _currentLoadedFile = null;
                return false;
            }
        }
    }
}
