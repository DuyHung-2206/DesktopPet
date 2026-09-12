using System.Windows;
using System.Windows.Controls;
using DesktopPet.ViewModels;

namespace DesktopPet.Views.Controls
{
    public partial class PetStatusPopup : System.Windows.Controls.UserControl
    {
        public PetStatusPopup()
        {
            InitializeComponent();
        }

        private void OnBagClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is PetViewModel vm)
            {
                vm.OpenInventory();
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is PetViewModel vm)
            {
                vm.IsStatusPopupOpen = false;
            }
        }
    }
}
