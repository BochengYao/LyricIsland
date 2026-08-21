using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LyricHover.App
{
    public partial class InformationDialog : Window
    {
        private static readonly string[] ThemeResourceKeys =
        {
            "SettingsRootBackgroundBrush",
            "SettingsControlForegroundBrush",
            "SettingsControlMutedForegroundBrush",
            "SettingsControlBorderBrush"
        };

        public InformationDialog(Window owner, string title, string message)
        {
            InitializeComponent();
            // WPF throws if the owner window has never been shown (e.g. the dialog is
            // raised while the main window is still constructing); skip ownership then.
            if (owner != null && owner.IsLoaded) Owner = owner;
            InheritThemeResources(owner);

            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
        }

        private void InheritThemeResources(FrameworkElement owner)
        {
            foreach (var key in ThemeResourceKeys)
            {
                if (owner?.TryFindResource(key) is Brush brush)
                {
                    Resources[key] = brush.CloneCurrentValue();
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                e.Handled = true;
                DialogResult = true;
            }
        }
    }
}
