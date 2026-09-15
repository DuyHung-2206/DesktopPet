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

        private void OnShopClick(object sender, RoutedEventArgs e) => _petVM.OpenShop();
        private void OnInventoryClick(object sender, RoutedEventArgs e) => _petVM.OpenInventory();
        private void OnCollectionClick(object sender, RoutedEventArgs e) => _petVM.OpenDashboard();
        private void OnSettingsClick(object sender, RoutedEventArgs e) => _petVM.OpenSettings();
        private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();
    }
}
