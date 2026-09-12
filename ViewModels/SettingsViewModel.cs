using System;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.Windows.Input;
using DesktopPet.Models;
using DesktopPet.Services;
using Microsoft.Win32;

namespace DesktopPet.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly PetViewModel _petVM;
        private readonly GameSave _save;

        public ObservableCollection<string> MonitorOptions { get; } = new();
        public ObservableCollection<double> ScaleOptions { get; } = new() { 0.75, 1.0, 1.25, 1.5 };

        public double SoundVolume
        {
            get => _save.Settings.SoundVolume;
            set
            {
                _save.Settings.SoundVolume = value;
                AudioService.Instance.Volume = value;
                OnPropertyChanged();
            }
        }

        public bool IsMuted
        {
            get => _save.Settings.IsMuted;
            set
            {
                _save.Settings.IsMuted = value;
                AudioService.Instance.IsMuted = value;
                OnPropertyChanged();
            }
        }

        public double SelectedScale
        {
            get => _save.Settings.PetScale;
            set
            {
                _save.Settings.PetScale = value;
                OnPropertyChanged();
                _petVM.RecalculatePosition();
                _petVM.NotifyAllProperties();
            }
        }

        public int SelectedMonitorIndex
        {
            get => _save.Settings.SelectedMonitorIndex;
            set
            {
                _save.Settings.SelectedMonitorIndex = value;
                OnPropertyChanged();
                _petVM.RecalculatePosition();
            }
        }

        public bool AlwaysOnTop
        {
            get => _save.Settings.AlwaysOnTop;
            set
            {
                _save.Settings.AlwaysOnTop = value;
                OnPropertyChanged();
            }
        }

        public bool NotificationsEnabled
        {
            get => _save.Settings.NotificationsEnabled;
            set
            {
                _save.Settings.NotificationsEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool PerformanceMode
        {
            get => _save.Settings.PerformanceMode;
            set
            {
                _save.Settings.PerformanceMode = value;
                OnPropertyChanged();
            }
        }

        public bool StartWithWindows
        {
            get => _save.Settings.StartWithWindows;
            set
            {
                _save.Settings.StartWithWindows = value;
                SetStartWithWindows(value);
                OnPropertyChanged();
            }
        }

        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            LoadMonitors();
            ViewportService.ViewportChanged += _ => LoadMonitors();

            SaveSettingsCommand = new RelayCommand(() =>
            {
                SaveService.Instance.SaveGame(_save);
                AudioService.Instance.PlayClick();
                _petVM.ShowEmote("Đã lưu cài đặt! ⚙️", 1.5);
            });
        }

        private void LoadMonitors()
        {
            MonitorOptions.Clear();
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                var vp = ViewportService.GetViewport(i);
                var pct = (int)Math.Round(vp.DpiScaleX * 100);
                var label = $"Màn hình {i + 1}: {s.Bounds.Width}x{s.Bounds.Height} ({pct}% DPI) {(s.Primary ? "(Chính)" : "")}";
                MonitorOptions.Add(label);
            }

            if (SelectedMonitorIndex >= screens.Length)
            {
                SelectedMonitorIndex = 0;
            }
        }

        private void SetStartWithWindows(bool enable)
        {
            try
            {
                const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
                if (key != null)
                {
                    var exePath = Environment.ProcessPath;
                    if (enable && !string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("DesktopPetWorld", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("DesktopPetWorld", false);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"Không thể cập nhật Registry khởi động cùng Windows: {ex.Message}");
            }
        }
    }
}
