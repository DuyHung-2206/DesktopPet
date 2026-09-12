using System.Windows;
using DesktopPet.ViewModels;

namespace DesktopPet.Views
{
    public partial class InventoryWindow : Window
    {
        public InventoryWindow(InventoryViewModel viewModel)
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
