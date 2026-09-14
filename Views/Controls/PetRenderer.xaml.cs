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

        public static int MapFrameIndex(int petFrameIndex, int petFrameCount, int accessoryFrameCount)
        {
            if (accessoryFrameCount <= 1 || petFrameCount <= 1) return 0;
            if (accessoryFrameCount == petFrameCount) return Math.Clamp(petFrameIndex, 0, accessoryFrameCount - 1);

            int target = (petFrameIndex * accessoryFrameCount) / petFrameCount;
            return Math.Clamp(target, 0, accessoryFrameCount - 1);
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

        private string? _equippedHatId;
        private string? _equippedGlassesId;
        private string? _equippedBowId;
        private string? _equippedBackpackId;

        private readonly Dictionary<string, BitmapSource> _overlayBitmapCache = new(StringComparer.OrdinalIgnoreCase);

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

            // Đồng bộ vị trí, góc nghiêng và tỉ lệ của tất cả các lớp phụ kiện theo frame hoạt ảnh hiện tại
            var (hat, glasses, bow, backpack) = GetFrameTransforms(state.State, state.FrameIndex, state.CellDimension);

            ApplySlotTransform(HatOverlayImage, HatScale, HatRotate, HatOffset, hat, state.FrameIndex, state.PetFrameCount);
            ApplySlotTransform(GlassesOverlayImage, GlassesScale, GlassesRotate, GlassesOffset, glasses, state.FrameIndex, state.PetFrameCount);
            ApplySlotTransform(BowOverlayImage, BowScale, BowRotate, BowOffset, bow, state.FrameIndex, state.PetFrameCount);
            ApplySlotTransform(BackpackOverlayImage, BackpackScale, BackpackRotate, BackpackOffset, backpack, state.FrameIndex, state.PetFrameCount);

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

        private void ApplySlotTransform(
            System.Windows.Controls.Image overlayImage,
            ScaleTransform scale,
            RotateTransform rotate,
            TranslateTransform translate,
            OverlayTransform t,
            int petFrameIndex,
            int petFrameCount)
        {
            if (overlayImage.Visibility != Visibility.Visible || overlayImage.Tag is not BitmapSource fullStrip)
            {
                return;
            }

            // Nếu phụ kiện là sprite strip có nhiều frame matching pet animation -> crop frame tương ứng qua MapFrameIndex
            if (fullStrip.PixelWidth > fullStrip.PixelHeight)
            {
                int fw = (int)fullStrip.PixelHeight;
                int count = Math.Max(1, (int)(fullStrip.PixelWidth / fw));
                int targetIdx = MapFrameIndex(petFrameIndex, petFrameCount, count);
                int fx = targetIdx * fw;
                if (fx + fw <= fullStrip.PixelWidth)
                {
                    overlayImage.Source = new CroppedBitmap(fullStrip, new Int32Rect(fx, 0, fw, fw));
                }
            }
            else
            {
                if (overlayImage.Source != fullStrip)
                {
                    overlayImage.Source = fullStrip;
                }
            }

            scale.ScaleX = t.ScaleX;
            scale.ScaleY = t.ScaleY;
            rotate.Angle = t.Angle;
            rotate.CenterX = t.CenterX;
            rotate.CenterY = t.CenterY;
            translate.X = t.X;
            translate.Y = t.Y;
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
                    // Cập nhật phụ kiện trang phục (chỉ cập nhật nếu item thay đổi)
                    UpdateAccessories(pet);
                    // Render đồng thời pet và phụ kiện tại đúng frame hiện tại
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

                    // Cập nhật phụ kiện trang phục
                    UpdateAccessories(pet);

                    // Render đồng thời pet và phụ kiện tại frame 0
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
            _isFacingLeft = isFacingLeft;
            FlipScale.ScaleX = isFacingLeft ? -1.0 : 1.0;
        }

        private void UpdateAccessories(Pet pet)
        {
            if (pet == null) return;

            // 1. Hat
            var hatId = pet.GetEquippedItem(EquipmentSlots.Hat);
            if (hatId != _equippedHatId)
            {
                _equippedHatId = hatId;
                UpdateSlotOverlay(hatId, HatOverlayImage, HeadAccessoryText);
            }

            // 2. Glasses
            var glassesId = pet.GetEquippedItem(EquipmentSlots.Glasses);
            if (glassesId != _equippedGlassesId)
            {
                _equippedGlassesId = glassesId;
                UpdateSlotOverlay(glassesId, GlassesOverlayImage, EyesAccessoryText);
            }

            // 3. Bow
            var bowId = pet.GetEquippedItem(EquipmentSlots.Bow);
            if (bowId != _equippedBowId)
            {
                _equippedBowId = bowId;
                UpdateSlotOverlay(bowId, BowOverlayImage, null);
            }

            // 4. Backpack
            var backpackId = pet.GetEquippedItem(EquipmentSlots.Backpack);
            if (backpackId != _equippedBackpackId)
            {
                _equippedBackpackId = backpackId;
                UpdateSlotOverlay(backpackId, BackpackOverlayImage, BackAccessoryText);
            }
        }

        private void UpdateSlotOverlay(string? itemId, System.Windows.Controls.Image overlayImage, TextBlock? vectorFallbackText)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                overlayImage.Source = null;
                overlayImage.Tag = null;
                overlayImage.Visibility = Visibility.Collapsed;
                if (vectorFallbackText != null) vectorFallbackText.Visibility = Visibility.Collapsed;
                return;
            }

            var item = Services.DataManager.Instance.GetItem(itemId);
            if (item == null)
            {
                overlayImage.Source = null;
                overlayImage.Tag = null;
                overlayImage.Visibility = Visibility.Collapsed;
                if (vectorFallbackText != null) vectorFallbackText.Visibility = Visibility.Collapsed;
                return;
            }

            if (!string.IsNullOrEmpty(item.OverlayAsset))
            {
                var fullPath = ResolveAssetFilePath(item.OverlayAsset);
                if (!string.IsNullOrEmpty(fullPath))
                {
                    var bmp = GetCachedOverlayBitmap(fullPath);
                    if (bmp != null)
                    {
                        overlayImage.Tag = bmp;
                        overlayImage.Source = bmp;
                        overlayImage.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        overlayImage.Tag = null;
                        overlayImage.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    overlayImage.Tag = null;
                    overlayImage.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                overlayImage.Tag = null;
                overlayImage.Visibility = Visibility.Collapsed;
            }

            // Fallback cho vector pet
            if (vectorFallbackText != null)
            {
                vectorFallbackText.Text = item.Icon;
                vectorFallbackText.Visibility = Visibility.Visible;
            }
        }

        private static string? ResolveAssetFilePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath),
                Path.Combine(Environment.CurrentDirectory, relativePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", relativePath)
            };

            foreach (var c in candidates)
            {
                try
                {
                    var norm = Path.GetFullPath(c);
                    if (File.Exists(norm)) return norm;
                }
                catch { }
            }
            return null;
        }

        private BitmapSource? GetCachedOverlayBitmap(string filePath)
        {
            if (_overlayBitmapCache.TryGetValue(filePath, out var cached))
            {
                return cached;
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

                _overlayBitmapCache[filePath] = bi;
                return bi;
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error($"Không thể nạp overlay image: {filePath}", ex);
                return null;
            }
        }

        public readonly struct OverlayTransform
        {
            public readonly double X;
            public readonly double Y;
            public readonly double Angle;
            public readonly double ScaleX;
            public readonly double ScaleY;
            public readonly double CenterX;
            public readonly double CenterY;

            public OverlayTransform(double x, double y, double angle, double scaleX, double scaleY, double centerX, double centerY)
            {
                X = x;
                Y = y;
                Angle = angle;
                ScaleX = scaleX;
                ScaleY = scaleY;
                CenterX = centerX;
                CenterY = centerY;
            }
        }

        private static (OverlayTransform Hat, OverlayTransform Glasses, OverlayTransform Bow, OverlayTransform Backpack) GetFrameTransforms(PetState state, int frameIndex, int cellDim)
        {
            // Semantic State check: Eat/Drink
            if (state == PetState.Eat || state == PetState.Drink)
            {
                double sx = 64.0 / cellDim;
                double sy = 64.0 / cellDim;
                double factor = 128.0 / cellDim;

                // Tọa độ frame ăn (5 frames, cell 96x96):
                // Mũ sử dụng scale 1.0 (vì mèo trong Eat được vẽ to hơn ~40% trong cell 96x96)
                // và tọa độ dịch chuyển trực tiếp trong không gian 128x128 để bám sát nhịp cúi đầu ăn
                var (hdx, hdy, hang, gdx, gdy, gang, bdx, bdy, bang, bpdx, bpdy, bpang) = Math.Clamp(frameIndex, 0, 4) switch
                {
                    0 => (14.0, 16.0, -3.0, 21.0, 4.5, -10.0, 21.5, 16.0, -10.0, 15.0, 12.0, -10.0),
                    1 => (18.0, 24.0, -18.0, 33.0, 13.5, -26.0, 26.0, 24.0, -26.0, 18.0, 16.0, -26.0),
                    2 => (18.0, 24.0, -18.0, 24.5, 14.0, -22.0, 24.0, 23.0, -22.0, 17.0, 15.0, -22.0),
                    3 => (18.0, 25.0, -20.0, 24.5, 14.0, -24.0, 25.0, 23.5, -24.0, 17.5, 15.5, -24.0),
                    _ => (15.0, 16.0, -3.0, 19.5, 5.5, -10.0, 20.5, 16.5, -10.0, 14.0, 12.5, -10.0)
                };

                return (
                    new OverlayTransform(hdx, hdy, hang, 1.0, 1.0, 64.0, 44.0),
                    new OverlayTransform(gdx * factor, gdy * factor, gang, sx, sy, 33.0 * factor, 33.0 * factor),
                    new OverlayTransform(bdx * factor, bdy * factor, bang, sx, sy, 33.0 * factor, 44.0 * factor),
                    new OverlayTransform(bpdx * factor, bpdy * factor, bpang, sx, sy, 16.0 * factor, 41.0 * factor)
                );
            }

            // Với các sprite 64x64 chuẩn (Idle, Walk, Run, Sleep, Happy, Hurt, Bath):
            // factor = 128.0 / 64.0 = 2.0 DIP/pixel.
            const double f = 2.0;

            var (h_dx, h_dy, h_ang, g_dx, g_dy, g_ang, b_dx, b_dy, b_ang, bp_dx, bp_dy, bp_ang) = state switch
            {
                PetState.Idle or PetState.Sit or PetState.WakeUp => (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),

                PetState.Walk => Math.Clamp(frameIndex, 0, 5) switch
                {
                    0 => (7.5, 2.0, -4.0,  8.0, -0.5, -4.0,  7.0, 1.0, -4.0,  4.0, 1.0, -4.0),
                    1 => (6.5, 1.0, -2.0,  6.5, -2.5, -2.0,  6.0, 0.5, -2.0,  3.5, 0.5, -2.0),
                    2 => (7.0, 1.0, 0.0,   5.5, -3.0, 0.0,   6.5, 1.0, 0.0,   4.0, 1.0, 0.0),
                    3 => (8.0, 1.0, -3.0,  7.0, -2.5, -3.0,  7.5, 1.0, -3.0,  4.5, 1.0, -3.0),
                    4 => (7.5, 1.0, -2.0,  9.5, -1.5, -2.0,  7.0, 0.5, -2.0,  4.0, 0.5, -2.0),
                    _ => (9.0, 0.0, -4.0,  10.5, -2.5, -4.0, 8.5, 0.0, -4.0,  5.0, 0.0, -4.0)
                },

                PetState.Run or PetState.Jump => Math.Clamp(frameIndex, 0, 5) switch
                {
                    0 => (11.0, -1.0, -8.0,  11.5, -3.5, -8.0,  10.0, -1.0, -8.0,  6.0, -1.0, -8.0),
                    1 => (11.5, -1.0, -10.0, 13.5, -2.5, -10.0, 10.5, -1.0, -10.0, 6.5, -1.0, -10.0),
                    2 => (9.5, -1.0, -6.0,   9.5, -4.0, -6.0,   9.0, -1.0, -6.0,   5.5, -1.0, -6.0),
                    3 => (10.5, -1.0, -8.0,  12.5, -2.5, -8.0,  10.0, -1.0, -8.0,  6.0, -1.0, -8.0),
                    4 => (10.5, -1.0, -8.0,  12.8, -2.8, -8.0,  10.0, -1.0, -8.0,  6.0, -1.0, -8.0),
                    _ => (11.0, -1.0, -8.0,  11.3, -2.2, -8.0,  10.5, -1.0, -8.0,  6.5, -1.0, -8.0)
                },

                PetState.Sleep => Math.Clamp(frameIndex, 0, 4) switch
                {
                    0 => (4.5, 9.0, -12.0,  7.5, 6.0, -12.0,   5.0, 8.0, -12.0,   2.0, 6.0, -12.0),
                    1 => (2.5, 12.0, -15.0, 0.0, 9.5, -15.0,   3.0, 10.0, -15.0,  1.0, 8.0, -15.0),
                    2 => (1.0, 15.0, -18.0, -3.5, 15.5, -18.0, 1.5, 12.0, -18.0,  0.5, 9.0, -18.0),
                    3 => (2.0, 13.0, -15.0, -2.0, 13.0, -15.0, 2.0, 11.0, -15.0,  1.0, 8.5, -15.0),
                    _ => (3.0, 11.0, -12.0, 2.0, 10.0, -12.0,  3.0, 9.5, -12.0,   1.5, 7.5, -12.0)
                },

                PetState.Happy or PetState.Play or PetState.Dance => Math.Clamp(frameIndex, 0, 5) switch
                {
                    0 => (0.0, 0.0, 0.0,    0.0, -0.5, 0.0,   0.0, 0.0, 0.0,    0.0, 0.0, 0.0),
                    1 => (0.0, -1.0, 0.0,   0.0, -1.5, 0.0,   0.0, -1.0, 0.0,   0.0, -0.5, 0.0),
                    2 => (0.0, -2.5, -1.0,  0.0, -2.5, -1.0,  0.0, -2.5, -1.0,  -0.5, -2.0, -1.0),
                    3 => (0.0, -1.5, 0.0,   0.0, -1.5, 0.0,   0.0, -1.5, 0.0,   -0.5, -1.5, 0.0),
                    4 => (0.5, 0.0, 0.0,    0.5, 0.0, 0.0,    0.5, 0.0, 0.0,    0.0, 0.0, 0.0),
                    _ => (0.0, 1.0, 0.0,    0.0, 0.0, 0.0,    0.0, 1.0, 0.0,    0.0, 0.5, 0.0)
                },

                PetState.Hurt or PetState.Sick or PetState.Sad or PetState.Angry or PetState.Fall => Math.Clamp(frameIndex, 0, 4) switch
                {
                    0 => (2.0, 5.0, 6.0,   0.5, 3.0, 6.0,    2.0, 4.0, 6.0,   1.0, 3.0, 6.0),
                    1 => (2.0, 9.0, 8.0,   -3.0, 4.5, 8.0,   1.5, 7.0, 8.0,   1.0, 5.0, 8.0),
                    2 => (4.0, 9.0, 4.0,   -5.5, 5.5, 4.0,   3.0, 7.0, 4.0,   2.0, 5.0, 4.0),
                    3 => (3.5, 7.0, -4.0,  4.0, 7.5, -4.0,   3.0, 6.0, -4.0,  2.0, 4.0, -4.0),
                    _ => (1.0, 12.0, 10.0, -3.5, 11.0, 10.0, 1.0, 9.0, 10.0,  0.5, 7.0, 10.0)
                },

                PetState.Bath or PetState.Dirty => Math.Clamp(frameIndex, 0, 5) switch
                {
                    0 => (1.0, 2.0, 0.0,  2.7, -0.5, 0.0,  1.0, 1.0, 0.0,  0.5, 1.0, 0.0),
                    1 => (1.0, 2.0, 0.0,  1.4, -4.3, 0.0,  1.0, 1.0, 0.0,  0.5, 1.0, 0.0),
                    2 => (0.0, 3.0, 0.0,  1.3, -0.7, 0.0,  0.0, 2.0, 0.0,  0.0, 2.0, 0.0),
                    3 => (-4.0, 2.0, 0.0, -6.6, 0.7, 0.0,  -3.0, 2.0, 0.0, -2.0, 1.5, 0.0),
                    4 => (-3.0, 2.0, 0.0, -5.5, -0.8, 0.0, -2.5, 2.0, 0.0, -1.5, 1.5, 0.0),
                    _ => (3.0, 3.0, 0.0,  3.9, 2.2, 0.0,   2.5, 2.5, 0.0,  1.5, 2.0, 0.0)
                },

                _ => (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
            };

            return (
                new OverlayTransform(h_dx * f, h_dy * f, h_ang, 1.0, 1.0, 64.0, 44.0),
                new OverlayTransform(g_dx * f, g_dy * f, g_ang, 1.0, 1.0, 66.0, 66.0),
                new OverlayTransform(b_dx * f, b_dy * f, b_ang, 1.0, 1.0, 66.0, 88.0),
                new OverlayTransform(bp_dx * f, bp_dy * f, bp_ang, 1.0, 1.0, 32.0, 82.0)
            );
        }


        public static int GetIntervalMs(PetState state) => state switch
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

        public bool PlayAnimation(string animationName)
        {
            if (Enum.TryParse<PetState>(animationName, true, out var state))
            {
                _currentState = state;
                var spriteFilePath = ResolveSpriteFilePath(_speciesFolder, state);
                if (!string.IsNullOrEmpty(spriteFilePath))
                {
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

        private bool LoadSpriteAnimation(string filePath, PetState state, bool restartAnimation = true)
        {
            _currentState = state;

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

                _spriteTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
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

                // Mỗi frame có kích thước vuông theo chiều cao (chuẩn 64x64)
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
