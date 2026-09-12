using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DesktopPet.Views.Controls
{
    public partial class EmoteBubble : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(EmoteBubble),
                new PropertyMetadata("❤️", OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EmoteBubble bubble)
            {
                bubble.EmoteTextBlock.Text = e.NewValue?.ToString() ?? "";
            }
        }

        public EmoteBubble()
        {
            InitializeComponent();
            var floatAnim = TryFindResource("FloatAnimation") as Storyboard;
            floatAnim?.Begin();
        }
    }
}
