using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace LyricHover.App
{
    public partial class WidgetsSettingsConfirmationWindow : Window
    {
        private static readonly string[] ThemeResourceKeys = { "SettingsRootBackgroundBrush", "SettingsControlBackgroundBrush", "SettingsControlForegroundBrush", "SettingsControlMutedForegroundBrush", "SettingsControlBorderBrush", "SettingsControlHoverBackgroundBrush", "SettingsControlPressedBackgroundBrush" };

        public WidgetsSettingsConfirmationWindow(Window owner, SettingsThemePreference themePreference)
        {
            InitializeComponent();
            if (owner != null && owner.IsLoaded) Owner = owner;
            ApplyTheme(IsDark(themePreference));
        }

        private void ApplyTheme(bool dark)
        {
            SetBrush("SettingsRootBackgroundBrush", dark ? "#1C1C1E" : "#F5F5F7");
            SetBrush("SettingsControlBackgroundBrush", dark ? "#2C2C2E" : "#FFFFFF");
            SetBrush("SettingsControlForegroundBrush", dark ? "#F5F5F7" : "#1D1D1F");
            SetBrush("SettingsControlMutedForegroundBrush", dark ? "#98989D" : "#6E6E73");
            SetBrush("SettingsControlBorderBrush", dark ? "#22FFFFFF" : "#1F000000");
            SetBrush("SettingsControlHoverBackgroundBrush", dark ? "#3A3A3C" : "#F5F5F7");
            SetBrush("SettingsControlPressedBackgroundBrush", dark ? "#48484A" : "#E8E8ED");
        }

        private void SetBrush(string key, string color) => Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        private static bool IsDark(SettingsThemePreference preference)
        {
            if (preference == SettingsThemePreference.Dark) return true;
            if (preference == SettingsThemePreference.Light) return false;
            try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is int value && value == 0; }
            catch { return false; }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => ConfirmButton.Focus();
        private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; DialogResult = false; } }
    }
}
