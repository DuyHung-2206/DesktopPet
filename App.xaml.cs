using System;
using System.IO;
using System.Threading;
using System.Windows;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.ViewModels;
using DesktopPet.Views;

namespace DesktopPet
{
    public partial class App : System.Windows.Application
    {
        private Mutex? _singleInstanceMutex;
        private TrayService? _trayService;

        private PetViewModel? _petVM;
        private PetWindow? _petWindow;
        private ShopWindow? _shopWindow;
        private InventoryWindow? _inventoryWindow;
        private PetSelectionWindow? _collectionWindow;
        private SettingsWindow? _settingsWindow;
        private MainDashboardWindow? _dashboardWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Single Instance Check (Chỉ cho phép một bản chạy trên máy)
            const string mutexName = "DesktopPetWorld_SingleInstance_Mutex_2026";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                System.Windows.MessageBox.Show("Desktop Pet World đã đang chạy ở dưới khay hệ thống (System Tray)!",
                                "Desktop Pet World", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 2. Global Exception Handling (Bắt mọi lỗi, không để crash âm thầm)
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LoggerService.Error("Unhandled AppDomain Exception", args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LoggerService.Error("Unhandled Dispatcher Exception", args.Exception);
                args.Handled = true; // Tiếp tục chạy nếu có thể
            };

            LoggerService.Info("=== KHỞI ĐỘNG DESKTOP PET WORLD ===");

            // 3. Tải Dữ liệu game
            DataManager.Instance.LoadAllData();
            var save = SaveService.Instance.LoadGame();
            AudioService.Instance.Volume = save.Settings.SoundVolume;
            AudioService.Instance.IsMuted = save.Settings.IsMuted;

            // 4. Tính toán thời gian Offline
            var offlineResult = OfflineTimeService.CalculateAndApply(save);

            // 5. Khởi tạo ViewModel & View chính
            _petVM = new PetViewModel(save);
            _petWindow = new PetWindow(_petVM);

            // 6. Khởi tạo các cửa sổ phụ
            var shopVM = new ShopViewModel(_petVM);
            _shopWindow = new ShopWindow(shopVM);

            var invVM = new InventoryViewModel(_petVM);
            _inventoryWindow = new InventoryWindow(invVM);

            var colVM = new PetCollectionViewModel(_petVM);
            _collectionWindow = new PetSelectionWindow(colVM);

            var setVM = new SettingsViewModel(_petVM);
            _settingsWindow = new SettingsWindow(setVM);

            var dashVM = new MainDashboardViewModel(_petVM);
            _dashboardWindow = new MainDashboardWindow(dashVM, _petVM);

            // 7. Đăng ký sự kiện mở cửa sổ từ PetViewModel
            _petVM.RequestOpenShop += () =>
            {
                shopVM.RefreshCoins();
                ShowWindow(_shopWindow);
            };

            _petVM.RequestOpenInventory += () =>
            {
                invVM.RefreshInventory();
                ShowWindow(_inventoryWindow);
            };

            _petVM.RequestOpenCollection += () =>
            {
                colVM.RefreshCollection();
                ShowWindow(_collectionWindow);
            };

            _petVM.RequestOpenDashboard += () =>
            {
                ShowWindow(_dashboardWindow);
            };

            _petVM.RequestOpenSettings += () =>
            {
                ShowWindow(_settingsWindow);
            };

            // 8. Khởi tạo System Tray
            _trayService = new TrayService(
                _petVM,
                () => ShowWindow(_settingsWindow),
                () => { shopVM.RefreshCoins(); ShowWindow(_shopWindow); },
                () => { invVM.RefreshInventory(); ShowWindow(_inventoryWindow); },
                () => Shutdown()
            );

            // 9. Hiển thị cửa sổ Desktop Pet
            _petWindow.Show();
            LoggerService.Info($"Đã hiển thị PetWindow thành công. Tọa độ: Left={_petWindow.Left}, Top={_petWindow.Top}");

            // Thông báo Offline time nếu có
            if (offlineResult.Elapsed.TotalMinutes >= 2)
            {
                _petVM.ShowEmote(offlineResult.SummaryMessage, 4.0);
            }
            else
            {
                _petVM.ShowEmote("Chào mừng bạn quay lại! 🐾✨", 3.0);
            }
        }

        private void ShowWindow(Window? win)
        {
            if (win == null) return;
            if (_petVM != null)
            {
                var petW = 70.0 * _petVM.GameSave.Settings.PetScale;
                var petH = 70.0 * _petVM.GameSave.Settings.PetScale;
                ViewportService.PositionWindowNearPet(
                    win,
                    _petVM.X,
                    _petVM.Y,
                    petW,
                    petH,
                    _petVM.GameSave.Settings.SelectedMonitorIndex);
            }

            if (win.IsVisible)
            {
                win.Activate();
            }
            else
            {
                win.Show();
                win.Activate();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggerService.Info("=== THOÁT DESKTOP PET WORLD ===");
            try
            {
                if (_petVM != null)
                {
                    SaveService.Instance.SaveGame(_petVM.GameSave);
                }
                _trayService?.Dispose();
                _singleInstanceMutex?.Dispose();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Lỗi khi dọn dẹp lúc thoát game", ex);
            }

            base.OnExit(e);
        }
    }
}
