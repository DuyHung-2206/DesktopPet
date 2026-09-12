using System.Windows;
using DesktopPet.ViewModels;

namespace DesktopPet.Views
{
    public partial class PetSelectionWindow : Window
    {
        public PetSelectionWindow(PetCollectionViewModel viewModel)
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
