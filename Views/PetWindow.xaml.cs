using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.ViewModels;
using DesktopPet.Views.Controls;

namespace DesktopPet.Views
{
    public partial class PetWindow : Window
    {
        private readonly PetViewModel _viewModel;
        private bool _isDragging = false;
        private System.Windows.Point _dragStartScreenPoint;
        private System.Windows.Point _petStartPoint;
        private bool _hasDraggedSignificantly = false;

        public PetViewModel ViewModel => _viewModel;

        public PetWindow(PetViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            PetRendererControl.OnAnimationCompleted = finishedState =>
            {
                Dispatcher.Invoke(() => _viewModel.OnAnimationCompleted(finishedState));
            };

            _viewModel.RequestPlayAnimation += OnRequestPlayAnimation;
            _viewModel.AppearanceChanged += OnAppearanceChanged;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            Loaded += (s, e) =>
            {
                UpdatePosition();
                UpdateRenderer();
            };

            ViewportService.ViewportChanged += OnViewportChanged;
            Closed += (s, e) =>
            {
                ViewportService.ViewportChanged -= OnViewportChanged;
                _viewModel.RequestPlayAnimation -= OnRequestPlayAnimation;
                _viewModel.AppearanceChanged -= OnAppearanceChanged;
            };
        }

        private void OnAppearanceChanged()
        {
            Dispatcher.Invoke(UpdateRenderer);
        }


        private void OnRequestPlayAnimation(string animName)
        {
            Dispatcher.Invoke(() => PetRendererControl.PlayAnimation(animName));
        }

        private void OnViewportChanged(ViewportBounds vp)
        {
            Dispatcher.Invoke(UpdatePosition);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PetViewModel.X) || e.PropertyName == nameof(PetViewModel.Y))
            {
                if (!_isDragging)
                {
                    UpdatePosition();
                }
            }
            else if (e.PropertyName == nameof(PetViewModel.IsStatusPopupOpen) ||
                     e.PropertyName == nameof(PetViewModel.IsEmoteVisible) ||
                     e.PropertyName == nameof(PetViewModel.EmoteText))
            {
                UpdatePosition();
            }
            else if (e.PropertyName == nameof(PetViewModel.State) ||
                     e.PropertyName == nameof(PetViewModel.IsFacingLeft) ||
                     e.PropertyName == nameof(PetViewModel.BaseColor) ||
                     e.PropertyName == nameof(PetViewModel.PetScale))
            {
                UpdatePosition();
                UpdateRenderer();
            }
        }

        public const double WindowWidth = 520.0;
        public const double WindowHeight = 460.0;
        private const double PetBaseSize = 70.0;

        private void UpdatePosition()
        {
            var monitorIdx = _viewModel.GameSave.Settings.SelectedMonitorIndex;
            var vp = ViewportService.GetViewport(monitorIdx);
            var m = ViewportService.SafeMargin;

            var scale = _viewModel.GameSave.Settings.PetScale;
            var petSize = PetBaseSize * scale;
            var petX = _viewModel.Pet.X;
            var petY = _viewModel.Pet.Y;

            PetRendererControl.Width = petSize;
            PetRendererControl.Height = petSize;

            // Ideal pet location inside the 520x460 window:
            // Centered horizontally, and placed around Y = 240 (leaving 240px above for overlays, and 150px below)
            double desiredPetCanvasX = (WindowWidth - petSize) / 2.0;
            double desiredPetCanvasY = 240.0;

            double targetLeft = petX - desiredPetCanvasX;
            double targetTop = petY - desiredPetCanvasY;

            double finalLeft = Math.Max(vp.Left + m, Math.Min(targetLeft, vp.Right - m - WindowWidth));
            double finalTop = Math.Max(vp.Top + m, Math.Min(targetTop, vp.Bottom - m - WindowHeight));

            ViewportService.SetWindowPosition(this, finalLeft, finalTop, monitorIdx);

            // Sync pet renderer exact canvas position
            double petCanvasX = petX - finalLeft;
            double petCanvasY = petY - finalTop;

            Canvas.SetLeft(PetRendererControl, petCanvasX);
            Canvas.SetTop(PetRendererControl, petCanvasY);

            Rect petRect = new Rect(petCanvasX, petCanvasY, petSize, petSize);

            // 1. POSITION STATUS POPUP
            Rect popupRect = Rect.Empty;
            if (_viewModel.IsStatusPopupOpen)
            {
                StatusPopupControl.Measure(new System.Windows.Size(200, 200));
                double popupW = 175.0;
                double popupH = StatusPopupControl.ActualHeight > 50.0 ? StatusPopupControl.ActualHeight : 135.0;

                // Horizontally: center over pet, clamped inside window canvas and screen
                double idealPopupX = petCanvasX + (petSize - popupW) / 2.0;
                double minPopupX = Math.Max(5.0, vp.Left + m - finalLeft);
                double maxPopupX = Math.Min(WindowWidth - popupW - 5.0, vp.Right - m - popupW - finalLeft);
                double popupX = Math.Max(minPopupX, Math.Min(idealPopupX, maxPopupX));

                // Vertically: prefer above pet if space permits, else below pet
                double spaceAbovePet = petY - (vp.Top + m);
                double popupY;
                if (spaceAbovePet >= popupH + 8.0)
                {
                    popupY = Math.Max(5.0, petCanvasY - popupH - 8.0);
                }
                else
                {
                    popupY = Math.Min(WindowHeight - popupH - 5.0, petCanvasY + petSize + 8.0);
                }

                Canvas.SetLeft(StatusPopupControl, popupX);
                Canvas.SetTop(StatusPopupControl, popupY);

                // Add 6px padding to popup collision box to completely protect all buttons, borders and shadows
                popupRect = new Rect(popupX - 4.0, popupY - 4.0, popupW + 8.0, popupH + 8.0);
            }

            // 2. DYNAMICALLY POSITION EMOTE BUBBLE (COLLISION AVOIDANCE)
            if (_viewModel.IsEmoteVisible)
            {
                EmoteBubbleControl.Measure(new System.Windows.Size(260, 250));
                double bubbleW = EmoteBubbleControl.DesiredSize.Width > 0 ? EmoteBubbleControl.DesiredSize.Width : 160.0;
                double bubbleH = EmoteBubbleControl.DesiredSize.Height > 0 ? EmoteBubbleControl.DesiredSize.Height : 45.0;
                bubbleW = Math.Min(260.0, Math.Max(60.0, bubbleW));
                bubbleH = Math.Max(35.0, bubbleH);

                PositionEmoteBubble(bubbleW, bubbleH, petCanvasX, petCanvasY, petSize, popupRect, petRect, vp, finalLeft, finalTop, m);
            }
        }

        private void PositionEmoteBubble(
            double bubbleW, double bubbleH,
            double petCanvasX, double petCanvasY, double petSize,
            Rect popupRect, Rect petRect,
            ViewportBounds vp, double finalLeft, double finalTop, double m)
        {
            var res = CalculateEmoteBubblePosition(bubbleW, bubbleH, petCanvasX, petCanvasY, petSize, popupRect, petRect, vp, finalLeft, finalTop, m);
            Canvas.SetLeft(EmoteBubbleControl, res.x);
            Canvas.SetTop(EmoteBubbleControl, res.y);
            EmoteBubbleControl.SetPointerPosition(res.pointer);
        }

        public static (double x, double y, BubblePointerPosition pointer) CalculateEmoteBubblePosition(
            double bubbleW, double bubbleH,
            double petCanvasX, double petCanvasY, double petSize,
            Rect popupRect, Rect petRect,
            ViewportBounds vp, double finalLeft, double finalTop, double m)
        {
            double centerPetX = petCanvasX + (petSize - bubbleW) / 2.0;
            double minSafeCanvasX = Math.Max(5.0, vp.Left + m - finalLeft);
            double maxSafeCanvasX = Math.Min(WindowWidth - bubbleW - 5.0, vp.Right - m - bubbleW - finalLeft);
            double clampedCenterPetX = Math.Max(minSafeCanvasX, Math.Min(centerPetX, maxSafeCanvasX));

            // Candidate positions in priority order:
            // 1. Above pet (preferred when status popup is not overlapping)
            // 2. Above status popup (when status popup is open above pet, and screen has room above)
            // 3. Upper-right safe area
            // 4. Upper-left safe area
            // 5. Side right of pet
            // 6. Side left of pet
            // 7. Side right of status popup
            // 8. Side left of status popup
            // 9. Below pet (if no status popup below)
            // 10. Below status popup (if status popup is below pet)

            var candidates = new List<(double x, double y, BubblePointerPosition pointer)>();

            // 1. Above pet
            candidates.Add((clampedCenterPetX, petCanvasY - bubbleH - 8.0, BubblePointerPosition.Down));

            if (!popupRect.IsEmpty)
            {
                // 2. Above Status Popup
                if (popupRect.Top < petCanvasY)
                {
                    double centerPopupX = popupRect.Left + (popupRect.Width - bubbleW) / 2.0;
                    double clampedPopupX = Math.Max(minSafeCanvasX, Math.Min(centerPopupX, maxSafeCanvasX));
                    candidates.Add((clampedPopupX, popupRect.Top - bubbleH - 8.0, BubblePointerPosition.Down));
                }
            }

            // 3. Upper-Right of Pet
            candidates.Add((petCanvasX + petSize + 10.0, petCanvasY - bubbleH * 0.4, BubblePointerPosition.None));

            // 4. Upper-Left of Pet
            candidates.Add((petCanvasX - bubbleW - 10.0, petCanvasY - bubbleH * 0.4, BubblePointerPosition.None));

            // 5. Side Right of Pet
            candidates.Add((petCanvasX + petSize + 10.0, petCanvasY + (petSize - bubbleH) / 2.0, BubblePointerPosition.None));

            // 6. Side Left of Pet
            candidates.Add((petCanvasX - bubbleW - 10.0, petCanvasY + (petSize - bubbleH) / 2.0, BubblePointerPosition.None));

            if (!popupRect.IsEmpty)
            {
                // 7. Side Right of Status Popup
                candidates.Add((popupRect.Right + 8.0, popupRect.Top + 10.0, BubblePointerPosition.None));

                // 8. Side Left of Status Popup
                candidates.Add((popupRect.Left - bubbleW - 8.0, popupRect.Top + 10.0, BubblePointerPosition.None));
            }

            // 9. Below Pet
            candidates.Add((clampedCenterPetX, petCanvasY + petSize + 8.0, BubblePointerPosition.Up));

            if (!popupRect.IsEmpty && popupRect.Bottom > petCanvasY + petSize)
            {
                // 10. Below Status Popup
                double centerPopupX = popupRect.Left + (popupRect.Width - bubbleW) / 2.0;
                double clampedPopupX = Math.Max(minSafeCanvasX, Math.Min(centerPopupX, maxSafeCanvasX));
                candidates.Add((clampedPopupX, popupRect.Bottom + 8.0, BubblePointerPosition.Up));
            }

            // Evaluate candidates in strict priority
            foreach (var cand in candidates)
            {
                if (IsCandidatePositionSafe(cand.x, cand.y, bubbleW, bubbleH, popupRect, petRect, vp, finalLeft, finalTop, m))
                {
                    return cand;
                }
            }

            // Fallback: strictly ensure zero intersection with popupRect
            double fallbackX = clampedCenterPetX;
            double fallbackY = petCanvasY - bubbleH - 8.0;
            BubblePointerPosition fallbackPointer = BubblePointerPosition.Down;

            if (!popupRect.IsEmpty)
            {
                if (petCanvasX + petSize + 10.0 + bubbleW + finalLeft <= vp.Right - m)
                {
                    fallbackX = petCanvasX + petSize + 10.0;
                    fallbackY = petCanvasY;
                    fallbackPointer = BubblePointerPosition.None;
                }
                else if (petCanvasX - bubbleW - 10.0 + finalLeft >= vp.Left + m)
                {
                    fallbackX = petCanvasX - bubbleW - 10.0;
                    fallbackY = petCanvasY;
                    fallbackPointer = BubblePointerPosition.None;
                }
                else
                {
                    fallbackX = clampedCenterPetX;
                    fallbackY = Math.Max(petCanvasY + petSize + 8.0, popupRect.Bottom + 8.0);
                    fallbackPointer = BubblePointerPosition.Up;
                }
            }

            fallbackX = Math.Max(5.0, Math.Min(fallbackX, WindowWidth - bubbleW - 5.0));
            fallbackY = Math.Max(5.0, Math.Min(fallbackY, WindowHeight - bubbleH - 5.0));

            return (fallbackX, fallbackY, fallbackPointer);
        }

        internal static bool IsCandidatePositionSafe(
            double candX, double candY, double w, double h,
            Rect popupRect, Rect petRect,
            ViewportBounds vp, double winLeft, double winTop, double m)
        {
            // 1. Must be within PetWindow Canvas bounds
            if (candX < 5.0 || candX + w > WindowWidth - 5.0) return false;
            if (candY < 5.0 || candY + h > WindowHeight - 5.0) return false;

            // 2. Must be within Screen working area
            double screenLeft = winLeft + candX;
            double screenTop = winTop + candY;
            if (screenLeft < vp.Left + m || screenLeft + w > vp.Right - m) return false;
            if (screenTop < vp.Top + m || screenTop + h > vp.Bottom - m) return false;

            var candRect = new Rect(candX, candY, w, h);

            // 3. Must NOT intersect StatusPopup if open
            if (!popupRect.IsEmpty && candRect.IntersectsWith(popupRect)) return false;

            // 4. Must NOT intersect Pet sprite
            if (candRect.IntersectsWith(petRect)) return false;

            return true;
        }

        private void UpdateRenderer()
        {
            PetRendererControl.UpdateAppearance(_viewModel.Pet, _viewModel.Species, _viewModel.PetScale);
        }

        private void OnPetMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double click chuột trái vào pet -> Ẩn / hiện bảng chỉ số
                _viewModel.IsStatusPopupOpen = !_viewModel.IsStatusPopupOpen;
                e.Handled = true;
                return;
            }

            _isDragging = true;
            _hasDraggedSignificantly = false;
            _dragStartScreenPoint = PointToScreen(e.GetPosition(this));
            _petStartPoint = new System.Windows.Point(_viewModel.Pet.X, _viewModel.Pet.Y);
            PetRendererControl.CaptureMouse();
        }

        private void OnPetMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentScreen = PointToScreen(e.GetPosition(this));
                var diffX = currentScreen.X - _dragStartScreenPoint.X;
                var diffY = currentScreen.Y - _dragStartScreenPoint.Y;

                // Chuyển đổi device pixel delta sang WPF DIP delta chính xác theo DPI hiện tại của Window
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                var diffDipX = diffX / scaleX;
                var diffDipY = diffY / scaleY;

                if (Math.Abs(diffDipX) > 3 || Math.Abs(diffDipY) > 3)
                {
                    _hasDraggedSignificantly = true;
                    var scale = _viewModel.GameSave.Settings.PetScale;
                    var petSize = PetBaseSize * scale;
                    var targetX = _petStartPoint.X + diffDipX;
                    var targetY = _petStartPoint.Y + diffDipY;

                    // Clamping: Pet can never be dragged outside the visible viewport!
                    var (clampedX, clampedY) = ViewportService.ClampPosition(
                        targetX, targetY,
                        petSize, petSize,
                        _viewModel.GameSave.Settings.SelectedMonitorIndex);

                    _viewModel.Pet.X = clampedX;
                    _viewModel.Pet.Y = clampedY;

                    UpdatePosition();
                }
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            LoggerService.Info($"PetWindow DPI thay đổi: {oldDpi.PixelsPerInchX} -> {newDpi.PixelsPerInchX} (Scale: {newDpi.DpiScaleX})");
            UpdatePosition();
            UpdateRenderer();
        }

        private void OnPetMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                PetRendererControl.ReleaseMouseCapture();

                if (_hasDraggedSignificantly)
                {
                    // Thả chuột sau khi nhấc pet lên cao -> kích hoạt vật lý rơi tự do!
                    var currentPetHeight = PetBaseSize * _viewModel.GameSave.Settings.PetScale;
                    var groundY = ViewportService.GetGroundY(currentPetHeight, _viewModel.GameSave.Settings.SelectedMonitorIndex);
                    if (_viewModel.Pet.Y < groundY - 15)
                    {
                        _viewModel.StartFalling();
                    }
                }
                else
                {
                    // Click chuột trái thông thường -> Vuốt ve pet tăng thân mật
                    _viewModel.OnPetClicked();
                }
            }
        }

        // CONTEXT MENU HANDLERS
        private void OnMenuToggleStatusPopupClick(object sender, RoutedEventArgs e) => _viewModel.IsStatusPopupOpen = !_viewModel.IsStatusPopupOpen;
        private void OnMenuFeedClick(object sender, RoutedEventArgs e) => _viewModel.FeedPet();
        private void OnMenuDrinkClick(object sender, RoutedEventArgs e) => _viewModel.DrinkPet();
        private void OnMenuPlayClick(object sender, RoutedEventArgs e) => _viewModel.PlayWithPet();
        private void OnMenuBathClick(object sender, RoutedEventArgs e) => _viewModel.BathPet();
        private void OnMenuSleepClick(object sender, RoutedEventArgs e) => _viewModel.ToggleSleep();
        private void OnMenuInventoryClick(object sender, RoutedEventArgs e) => _viewModel.OpenInventory();
        private void OnMenuShopClick(object sender, RoutedEventArgs e) => _viewModel.OpenShop();
        private void OnMenuCollectionClick(object sender, RoutedEventArgs e) => _viewModel.OpenCollection();
        private void OnMenuDashboardClick(object sender, RoutedEventArgs e) => _viewModel.OpenDashboard();
        private void OnMenuSettingsClick(object sender, RoutedEventArgs e) => _viewModel.OpenSettings();
        private void OnMenuExitClick(object sender, RoutedEventArgs e)
        {
            SaveService.Instance.SaveGame(_viewModel.GameSave);
            System.Windows.Application.Current.Shutdown();
        }
    }
}
