using System;
using System.Collections.Generic;
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
        public static bool IsAnimationDebugEnabled { get; set; } = false;

        public sealed class PetAnimationState
        {
            public string AnimationName { get; init; } = string.Empty;
            public PetState State { get; init; }
            public int FrameIndex { get; init; }
            public int PetFrameCount { get; init; }
            public bool IsFlipped { get; init; }
            public double PetScale { get; init; }
            public int CellDimension { get; init; }

            public PetAnimationState(string animationName, PetState state, int frameIndex, int petFrameCount, bool isFlipped, double petScale, int cellDimension)
            {
                AnimationName = animationName;
                State = state;
                FrameIndex = frameIndex;
                PetFrameCount = petFrameCount;
                IsFlipped = isFlipped;
                PetScale = petScale;
                CellDimension = cellDimension;
            }
        }

        public readonly struct PetAnimationFrame
        {
            public readonly PetState State;
            public readonly int FrameIndex;
            public readonly int FrameCount;
            public readonly bool IsFacingLeft;
            public readonly int CellDimension;

            public PetAnimationFrame(PetState state, int frameIndex, int frameCount, bool isFacingLeft, int cellDimension)
            {
                State = state;
                FrameIndex = frameIndex;
                FrameCount = frameCount;
                IsFacingLeft = isFacingLeft;
                CellDimension = cellDimension;
            }
        }

        private readonly DispatcherTimer _blinkTimer = new();
        private readonly DispatcherTimer _spriteTimer = new();
        private readonly Random _rand = new();

        private PetState _currentState = PetState.Idle;
        private bool _isFacingLeft = false;
        private double _petScale = 1.0;

        public double PetScale
        {
            get => _petScale;
            set => _petScale = value > 0 ? value : 1.0;
        }

        public PetAnimationState CurrentAnimationState => new(
            _currentState.ToString(),
            _currentState,
            _currentFrameIndex,
            _frameCount,
            _isFacingLeft,
            _petScale,
            _currentSpriteStrip != null && (int)_currentSpriteStrip.PixelHeight > 0 ? (int)_currentSpriteStrip.PixelHeight : 64
        );

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
        private int _cyclesCompleted = 0;

        private void OnSpriteTimerTick(object? sender, EventArgs e)
        {
            if (_currentSpriteStrip == null || _frameCount <= 1) return;

            var def = Services.AnimationRegistry.GetDefinition(_currentState);

            if (def.LoopMode == Services.AnimationLoopMode.OneShot)
            {
                if (_currentFrameIndex >= _frameCount - 1)
                {
                    _cyclesCompleted++;
                    if (_cyclesCompleted >= def.RepeatCycles)
                    {
                        _spriteTimer.Stop();
                        RenderCurrentFrame();
                        var finished = _currentState;
                        OnAnimationCompleted?.Invoke(finished);
                        return;
                    }

                    _currentFrameIndex = 0;
                    RenderCurrentFrame();
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
            RenderAnimationFrame(CurrentAnimationState);
        }

        public void RenderAnimationFrame(PetAnimationState state)
        {
            if (_currentSpriteStrip != null)
            {
                if (state.PetFrameCount <= 1)
                {
                    SpriteImage.Source = _currentSpriteStrip;
                }
                else
                {
                    int frameWidth = (int)(_currentSpriteStrip.PixelWidth / state.PetFrameCount);
                    int frameHeight = (int)_currentSpriteStrip.PixelHeight;
                    int x = state.FrameIndex * frameWidth;

                    if (x + frameWidth <= _currentSpriteStrip.PixelWidth)
                    {
                        var crop = new CroppedBitmap(_currentSpriteStrip, new Int32Rect(x, 0, frameWidth, frameHeight));
                        SpriteImage.Source = crop;
                    }
                }
            }

            FlipScale.ScaleX = state.IsFlipped ? -1.0 : 1.0;

            if (IsAnimationDebugEnabled)
            {
                Services.LoggerService.Debug($"[AnimState] State={state.State} Anim={state.AnimationName} Frame={state.FrameIndex}/{state.PetFrameCount} IsFlipped={state.IsFlipped} Scale={state.PetScale}");
            }
        }

        public void RenderAnimationFrame(PetAnimationFrame frame)
        {
            RenderAnimationFrame(new PetAnimationState(
                frame.State.ToString(),
                frame.State,
                frame.FrameIndex,
                frame.FrameCount,
                frame.IsFacingLeft,
                _petScale,
                frame.CellDimension
            ));
        }

        public void UpdateAppearance(Pet pet, PetSpecies? species, double petScale = 1.0)
        {
            if (pet == null) return;

            // 1. Cập nhật scale & hướng quay mặt trước
            _petScale = petScale > 0 ? petScale : 1.0;
            ApplyFlip(pet.IsFacingLeft);

            // 2. Xác định file sprite tương ứng với trạng thái logic của pet
            _speciesFolder = species?.Name ?? "Cat";
            var spriteFilePath = ResolveSpriteFilePath(_speciesFolder, pet.State);

            if (!string.IsNullOrEmpty(spriteFilePath))
            {
                bool isSameStateAndFile = (_currentState == pet.State && _currentLoadedFile == spriteFilePath && _currentSpriteStrip != null);

                if (isSameStateAndFile)
                {
                    // Trạng thái và sprite không đổi -> Giữ nguyên frame hiện tại (không reset về 0)
                    RenderCurrentFrame();
                    return;
                }

                // Trạng thái hoặc file sprite đã thay đổi -> Nạp sprite mới và reset về frame 0
                bool success = LoadSpriteAnimation(spriteFilePath, pet.State, restartAnimation: true);
                if (success)
                {
                    SpriteImage.Visibility = Visibility.Visible;
                    VectorPetCanvas.Visibility = Visibility.Collapsed;
                    _walkAnim?.Stop();
                    RootRotate.Angle = 0;
                    BodyTranslate.Y = 0;

                    // Render tại frame 0
                    RenderCurrentFrame();
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
            _isFacingLeft = isFacingLeft;
            FlipScale.ScaleX = isFacingLeft ? -1.0 : 1.0;
        }


        public static int GetIntervalMs(PetState state) => Services.AnimationRegistry.GetIntervalMs(state);

        public bool PlayAnimation(string animationName)
        {
            if (Enum.TryParse<PetState>(animationName, true, out var state))
            {
                _currentState = state;
                _cyclesCompleted = 0;
                var spriteFilePath = ResolveSpriteFilePath(_speciesFolder, state);
                if (!string.IsNullOrEmpty(spriteFilePath))
                {
                    bool success = LoadSpriteAnimation(spriteFilePath, state, restartAnimation: true);
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

            // 1. Khớp chính xác tên file của trạng thái từ AnimationRegistry (e.g. Idle.png, Walk.png, Sit.png, Drink.png, WakeUp.png, etc.)
            var def = Services.AnimationRegistry.GetDefinition(state);
            var direct = Path.Combine(baseDir, def.SpriteFileName);
            if (File.Exists(direct)) return direct;

            var directByState = Path.Combine(baseDir, $"{state}.png");
            if (File.Exists(directByState)) return directByState;

            // 2. Fallback mặc định về Idle.png nếu loài chưa hỗ trợ đủ các trạng thái
            var idle = Path.Combine(baseDir, "Idle.png");
            return File.Exists(idle) ? idle : null;
        }

        private bool LoadSpriteAnimation(string filePath, PetState state, bool restartAnimation = true)
        {
            _currentState = state;
            _cyclesCompleted = 0;

            if (state == PetState.Eat)
            {
                _eatStartTime = DateTime.UtcNow;
            }

            int intervalMs = GetIntervalMs(state);

            if (_currentLoadedFile == filePath && _currentSpriteStrip != null)
            {
                if (restartAnimation)
                {
                    _currentFrameIndex = 0;
                }
                else if (_frameCount > 0)
                {
                    _currentFrameIndex = Math.Clamp(_currentFrameIndex, 0, _frameCount - 1);
                }

                int cachedEffectiveIntervalMs = (state == PetState.Eat && _frameCount > 0)
                    ? Math.Max(100, (int)(Services.AnimationRegistry.GetDefinition(state).TotalDurationSeconds * 1000 / _frameCount))
                    : intervalMs;

                _spriteTimer.Interval = TimeSpan.FromMilliseconds(cachedEffectiveIntervalMs);
                RenderCurrentFrame();
                if (_frameCount > 1 && !_spriteTimer.IsEnabled)
                {
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

                int cellDim = (int)bi.PixelHeight > 0 ? (int)bi.PixelHeight : 64;
                _frameCount = Math.Max(1, (int)(bi.PixelWidth / cellDim));
                if (restartAnimation)
                {
                    _currentFrameIndex = 0;
                }
                else
                {
                    _currentFrameIndex = Math.Clamp(_currentFrameIndex, 0, _frameCount - 1);
                }

                int effectiveIntervalMs = (state == PetState.Eat && _frameCount > 0)
                    ? Math.Max(100, (int)(Services.AnimationRegistry.GetDefinition(state).TotalDurationSeconds * 1000 / _frameCount))
                    : intervalMs;

                _spriteTimer.Interval = TimeSpan.FromMilliseconds(effectiveIntervalMs);
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
