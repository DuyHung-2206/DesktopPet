using System.Windows;
using DesktopPet.ViewModels;

namespace DesktopPet.Views
{
    public partial class MainDashboardWindow : Window
    {
        private readonly MainDashboardViewModel _viewModel;
        private readonly PetViewModel _petVM;

        public MainDashboardWindow(MainDashboardViewModel viewModel, PetViewModel petVM)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _petVM = petVM;
            DataContext = _viewModel;
        }

        public void ShowInformationPanel() => ShowPanel(InformationPanel, "Thông tin Pet");

        public void ShowQuestPanel() => ShowPanel(QuestPanel, "Nhiệm vụ");

        public void ShowDailyRewardsPanel() => ShowPanel(DailyRewardsPanel, "Phần thưởng hàng ngày");

        private void ShowPanel(System.Windows.UIElement panel, string title)
        {
            InformationPanel.Visibility = Visibility.Collapsed;
            QuestPanel.Visibility = Visibility.Collapsed;
            DailyRewardsPanel.Visibility = Visibility.Collapsed;
            panel.Visibility = Visibility.Visible;
            PanelTitleText.Text = title;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();
    }
}
