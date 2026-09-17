using System.Windows;

namespace DesktopPet.Views
{
    public partial class ResetConfirmationDialog : Window
    {
        public bool IsConfirmed { get; private set; } = false;

        public ResetConfirmationDialog()
        {
            InitializeComponent();
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
