using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LyricHover.App
{
    public partial class TaskbarLyricsConfirmationWindow : Window
    {
        private static readonly string[] ThemeResourceKeys =
        {
            "SettingsRootBackgroundBrush",
            "SettingsControlBackgroundBrush",
            "SettingsControlForegroundBrush",
            "SettingsControlMutedForegroundBrush",
            "SettingsControlBorderBrush",
            "SettingsControlHoverBackgroundBrush",
            "SettingsControlPressedBackgroundBrush"
        };

        public TaskbarLyricsConfirmationWindow(Window owner)
        {
            InitializeComponent();
            // WPF throws if the owner window has never been shown; skip ownership then.
            if (owner != null && owner.IsLoaded) Owner = owner;
            InheritThemeResources(owner);
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
            ConfirmButton.Focus();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            DialogResult = false;
        }
    }
}
