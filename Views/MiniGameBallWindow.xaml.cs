using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.ViewModels;

namespace DesktopPet.Views
{
    public partial class MiniGameBallWindow : Window
    {
        private readonly PetViewModel _petVM;
        private readonly DispatcherTimer _gameTimer = new();
        private readonly DispatcherTimer _countdownTimer = new();
        private readonly Random _rand = new();

        private double _ballX = 300;
        private double _ballY = 300;
        private double _velX = 350;
        private double _velY = 280;
        private int _score = 0;
        private int _remainingSeconds = 30;

        public MiniGameBallWindow(PetViewModel petVM)
        {
            InitializeComponent();
            _petVM = petVM;

            _gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            _gameTimer.Tick += OnGamePhysicsTick;

            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += OnCountdownTick;

            Loaded += (s, e) => StartGame();
        }

        public void StartGame()
        {
            _score = 0;
            _remainingSeconds = 30;
            ScoreTextBlock.Text = "0";
            TimeTextBlock.Text = "Thời gian: 30s";
            WinPanel.Visibility = Visibility.Collapsed;

            var monitorIdx = _petVM.GameSave.Settings.SelectedMonitorIndex;
            var vp = ViewportService.GetViewport(monitorIdx);

            Width = vp.Width;
            Height = vp.Height;
            ViewportService.SetWindowPosition(this, vp.Left, vp.Top, monitorIdx);

            var screenW = vp.Width;
            var screenH = vp.Height;

            _ballX = Math.Max(ViewportService.SafeMargin, (screenW - 60) / 2);
            _ballY = Math.Max(70, (screenH - 60) / 2);

            _gameTimer.Start();
            _countdownTimer.Start();
        }

        private void OnGamePhysicsTick(object? sender, EventArgs e)
        {
            var dt = 0.02;
            _ballX += _velX * dt;
            _ballY += _velY * dt;

            var monitorIdx = _petVM.GameSave.Settings.SelectedMonitorIndex;
            var vp = ViewportService.GetViewport(monitorIdx);
            var screenW = vp.Width;
            var screenH = vp.Height;
            var m = ViewportService.SafeMargin;

            var minX = m;
            var maxX = Math.Max(minX, screenW - 60 - m);
            var minY = Math.Max(m, 70.0); // Keep below score panel at top
            var maxY = Math.Max(minY, screenH - 60 - m);

            if (_ballX <= minX)
            {
                _ballX = minX;
                _velX = Math.Abs(_velX);
            }
            else if (_ballX >= maxX)
            {
                _ballX = maxX;
                _velX = -Math.Abs(_velX);
            }

            if (_ballY <= minY)
            {
                _ballY = minY;
                _velY = Math.Abs(_velY);
            }
            else if (_ballY >= maxY)
            {
                _ballY = maxY;
                _velY = -Math.Abs(_velY);
            }

            Canvas.SetLeft(BallElement, _ballX);
            Canvas.SetTop(BallElement, _ballY);
        }

        private void OnCountdownTick(object? sender, EventArgs e)
        {
            _remainingSeconds--;
            TimeTextBlock.Text = $"Thời gian: {_remainingSeconds}s";

            if (_remainingSeconds <= 0)
            {
                EndGame();
            }
        }

        private void OnBallClick(object sender, MouseButtonEventArgs e)
        {
            _score++;
            ScoreTextBlock.Text = _score.ToString();
            AudioService.Instance.PlayHappy();

            // Đổi hướng ngẫu nhiên & tăng tốc độ nhẹ
            _velX = (_velX > 0 ? 1 : -1) * _rand.Next(300, 500);
            _velY = (_velY > 0 ? 1 : -1) * _rand.Next(250, 450);

            _petVM.ShowEmote("Bắt được rồi! ⚽✨", 1.0);
        }

        private void EndGame()
        {
            _gameTimer.Stop();
            _countdownTimer.Stop();

            int expReward = 0;
            if (_petVM.CompleteNeed(PetNeedTypes.Play, 20, out int awardedExp))
            {
                expReward = awardedExp;
            }

            int coinsReward = Math.Max(10, _score * 2);
            _petVM.AddCoins(coinsReward);
            _petVM.Pet.Happiness = Math.Min(100, _petVM.Pet.Happiness + 25);
            _petVM.Pet.Affection = Math.Min(100, _petVM.Pet.Affection + 5);
            _petVM.CheckAchievementProgress("minigame_master", _score);

            if (expReward > 0)
            {
                RewardSummaryText.Text = $"Bắt bóng {_score} lần!\n+{coinsReward} Xu | +{expReward} EXP | +💖 Thân thiết!";
            }
            else
            {
                RewardSummaryText.Text = $"Bắt bóng {_score} lần!\n+{coinsReward} Xu | Bé rất vui vẻ! 💖";
            }

            var vp = ViewportService.GetViewport(_petVM.GameSave.Settings.SelectedMonitorIndex);
            var screenW = ActualWidth > 100 ? ActualWidth : vp.Width;
            var screenH = ActualHeight > 100 ? ActualHeight : vp.Height;

            WinPanel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var panelW = WinPanel.DesiredSize.Width > 0 ? WinPanel.DesiredSize.Width : 300;
            var panelH = WinPanel.DesiredSize.Height > 0 ? WinPanel.DesiredSize.Height : 160;

            Canvas.SetLeft(WinPanel, Math.Max(ViewportService.SafeMargin, (screenW - panelW) / 2));
            Canvas.SetTop(WinPanel, Math.Max(ViewportService.SafeMargin, (screenH - panelH) / 2));

            WinPanel.Visibility = Visibility.Visible;
            AudioService.Instance.PlayLevelUp();
        }

        private void OnEndGameClick(object sender, RoutedEventArgs e)
        {
            _gameTimer.Stop();
            _countdownTimer.Stop();
            Close();
        }
    }
}
