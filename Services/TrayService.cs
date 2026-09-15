using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DesktopPet.ViewModels;

namespace DesktopPet.Services
{
    public class TrayService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly PetViewModel _petVM;
        private readonly Action _openSettingsAction;
        private readonly Action _openShopAction;
        private readonly Action _openInventoryAction;
        private readonly Action _exitAction;

        public TrayService(PetViewModel petVM,
                           Action openSettingsAction,
                           Action openShopAction,
                           Action openInventoryAction,
                           Action exitAction)
        {
            _petVM = petVM;
            _openSettingsAction = openSettingsAction;
            _openShopAction = openShopAction;
            _openInventoryAction = openInventoryAction;
            _exitAction = exitAction;

            InitializeTray();

            NotificationService.OnShowNotification = ShowBalloonNotification;
        }

        private void InitializeTray()
        {
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Text = "Desktop Pet World",
                    Visible = true
                };

                // Tải icon từ Assets nếu có, fallback SystemIcons.Application
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons", "app.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("🐾 Hiện / Trọng Tâm Pet", null, (s, e) => _petVM.OnPetClicked());
                contextMenu.Items.Add("🛒 Cửa hàng", null, (s, e) => _openShopAction());
                contextMenu.Items.Add("🎒 Túi Đồ", null, (s, e) => _openInventoryAction());
                contextMenu.Items.Add("⚙️ Cài Đặt", null, (s, e) => _openSettingsAction());
                contextMenu.Items.Add(new ToolStripSeparator());

                var muteItem = new ToolStripMenuItem("🔇 Tắt Tiếng (Mute)") { Checked = AudioService.Instance.IsMuted };
                muteItem.Click += (s, e) =>
                {
                    AudioService.Instance.IsMuted = !AudioService.Instance.IsMuted;
                    muteItem.Checked = AudioService.Instance.IsMuted;
                    _petVM.GameSave.Settings.IsMuted = AudioService.Instance.IsMuted;
                    SaveService.Instance.SaveGame(_petVM.GameSave);
                };
                contextMenu.Opening += (s, e) =>
                {
                    muteItem.Checked = AudioService.Instance.IsMuted;
                };
                contextMenu.Items.Add(muteItem);

                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add("❌ Thoát Game Hoàn Toàn", null, (s, e) => _exitAction());

                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, e) => _petVM.OnPetClicked();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Lỗi khởi tạo System Tray Icon", ex);
            }
        }

        public void ShowBalloonNotification(string title, string message)
        {
            try
            {
                if (_notifyIcon != null && _notifyIcon.Visible && _petVM.GameSave.Settings.NotificationsEnabled)
                {
                    _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"Không thể hiện thông báo Tray: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
