using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.ViewModels;

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
                if (finishedState == PetState.Eat)
                {
                    Dispatcher.Invoke(() => _viewModel.FinishEatAnimation());
                }
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
                     e.PropertyName == nameof(PetViewModel.IsEmoteVisible))
            {
                UpdatePosition();
            }
            else if (e.PropertyName == nameof(PetViewModel.State) ||
                     e.PropertyName == nameof(PetViewModel.IsFacingLeft) ||
                     e.PropertyName == nameof(PetViewModel.BaseColor) ||
                     e.PropertyName == nameof(PetViewModel.EquippedHeadIcon) ||
                     e.PropertyName == nameof(PetViewModel.EquippedEyesIcon) ||
                     e.PropertyName == nameof(PetViewModel.EquippedBackIcon) ||
                     e.PropertyName == nameof(PetViewModel.EquippedHatIcon) ||
                     e.PropertyName == nameof(PetViewModel.EquippedGlassesIcon) ||
                     e.PropertyName == nameof(PetViewModel.EquippedBowIcon) ||
                     e.PropertyName == nameof(PetViewModel.EquippedBackpackIcon) ||
                     e.PropertyName == nameof(PetViewModel.PetScale) ||
                     e.PropertyName == "EquippedItems")
            {
                UpdatePosition();
                UpdateRenderer();
            }

        }

        private const double PetBaseSize = 70.0;

        private void UpdatePosition()
        {
            var monitorIdx = _viewModel.GameSave.Settings.SelectedMonitorIndex;
            var vp = ViewportService.GetViewport(monitorIdx);
            var m = ViewportService.SafeMargin;

            var scale = _viewModel.GameSave.Settings.PetScale;
            var petSize = PetBaseSize * scale;
            var petX = _viewModel.X;
            var petY = _viewModel.Y;

            PetRendererControl.Width = petSize;
            PetRendererControl.Height = petSize;

            // 1. Check vertical space above pet to decide if overlays (StatusPopup & EmoteBubble)
            // sit above or below the pet
            double spaceAbove = petY - vp.Top;
            bool placeOverlaysBelow = spaceAbove < 140.0;

            double petCanvasY;
            double targetTop;
            if (placeOverlaysBelow)
            {
                // Pet placed near the top inside the 360px window
                petCanvasY = 20.0;
                targetTop = petY - petCanvasY;
                if (targetTop < vp.Top + m)
                {
                    targetTop = vp.Top + m;
                    petCanvasY = petY - targetTop;
                }
                Canvas.SetTop(PetRendererControl, petCanvasY);

                // EmoteBubble below pet
                Canvas.SetTop(EmoteBubbleControl, petCanvasY + petSize + 6.0);

                // StatusPopup below pet
                Canvas.SetTop(StatusPopupControl, petCanvasY + petSize + 6.0);
            }
            else
            {
                // Normal: overlays above pet, pet placed near bottom inside window
                petCanvasY = 160.0;
                targetTop = petY - petCanvasY;
                if (targetTop + 360.0 > vp.Bottom - m)
                {
                    targetTop = Math.Max(vp.Top + m, vp.Bottom - m - 360.0);
                    petCanvasY = petY - targetTop;
                }
                Canvas.SetTop(PetRendererControl, petCanvasY);

                // EmoteBubble above pet
                Canvas.SetTop(EmoteBubbleControl, Math.Max(5.0, petCanvasY - 65.0));

                // StatusPopup above pet
                Canvas.SetTop(StatusPopupControl, Math.Max(5.0, petCanvasY - 145.0));
            }

            // 2. Horizontal placement: Window is 360px wide. Position window so it remains completely inside viewport
            double desiredPetCanvasX = 145.0; // pet centered in 360px window
            double targetLeft = petX - desiredPetCanvasX;

            double finalLeft;
            if (targetLeft < vp.Left + m)
            {
                finalLeft = vp.Left + m;
            }
            else if (targetLeft + 360.0 > vp.Right - m)
            {
                finalLeft = Math.Max(vp.Left + m, vp.Right - m - 360.0);
            }
            else
            {
                finalLeft = targetLeft;
            }

            ViewportService.SetWindowPosition(this, finalLeft, targetTop, monitorIdx);

            // Sync pet renderer exact canvas position so it always lands on petX
            double petCanvasX = petX - finalLeft;
            Canvas.SetLeft(PetRendererControl, petCanvasX);

            // 3. Responsive horizontal positioning for EmoteBubble (width 260) and StatusPopup (width 175)
            // so they never clip against viewport edges even if pet is at far left or far right
            double petCenterCanvasX = petCanvasX + (petSize / 2.0);

            // EmoteBubble: Width 260
            double desiredBubbleX = petCenterCanvasX - 130.0;
            double bubbleMinX = 5.0;
            double bubbleMaxX = Math.Max(bubbleMinX, 360.0 - 260.0 - 5.0); // 95.0
            Canvas.SetLeft(EmoteBubbleControl, Math.Max(bubbleMinX, Math.Min(desiredBubbleX, bubbleMaxX)));

            // StatusPopup: Width 175
            double desiredPopupX = petCenterCanvasX - (175.0 / 2.0);
            double popupMinX = 5.0;
            double popupMaxX = Math.Max(popupMinX, 360.0 - 175.0 - 5.0); // 180.0
            Canvas.SetLeft(StatusPopupControl, Math.Max(popupMinX, Math.Min(desiredPopupX, popupMaxX)));
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
        private void OnMenuPlayClick(object sender, RoutedEventArgs e) => _viewModel.PlayWithPet();
        private void OnMenuBathClick(object sender, RoutedEventArgs e) => _viewModel.BathPet();
        private void OnMenuSleepClick(object sender, RoutedEventArgs e) => _viewModel.ToggleSleep();
        private void OnMenuInventoryClick(object sender, RoutedEventArgs e) => _viewModel.OpenInventory();
        private void OnMenuShopClick(object sender, RoutedEventArgs e) => _viewModel.OpenShop();
        private void OnMenuCollectionClick(object sender, RoutedEventArgs e) => _viewModel.OpenDashboard();
        private void OnMenuMiniGameClick(object sender, RoutedEventArgs e) => _viewModel.OpenMiniGame();
        private void OnMenuDashboardClick(object sender, RoutedEventArgs e) => _viewModel.OpenDashboard();
        private void OnMenuSettingsClick(object sender, RoutedEventArgs e) => _viewModel.OpenSettings();
        private void OnMenuExitClick(object sender, RoutedEventArgs e)
        {
            SaveService.Instance.SaveGame(_viewModel.GameSave);
            System.Windows.Application.Current.Shutdown();
        }
    }
}
