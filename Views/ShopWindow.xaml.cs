using System.Windows;
using DesktopPet.ViewModels;

namespace DesktopPet.Views
{
    public partial class ShopWindow : Window
    {
        public ShopWindow(ShopViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
