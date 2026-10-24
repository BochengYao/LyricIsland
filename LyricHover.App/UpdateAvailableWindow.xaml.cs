using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LyricHover.Core;

namespace LyricHover.App
{
    public partial class UpdateAvailableWindow : Window
    {
        internal const string StoreUri = "ms-windows-store://pdp/?ProductId=9NRXZP5HMXK2";
        internal const string StoreWebUrl = "https://apps.microsoft.com/detail/9nrxzp5hmxk2?hl=zh-CN&gl=CN";

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

        private readonly UpdateDialogCopy copy;

        public UpdateAvailableWindow(Window owner, AppUpdateRelease release, string locale)
        {
            InitializeComponent();
            if (owner != null && owner.IsLoaded)
            {
                Owner = owner;
            }

            InheritThemeResources(owner);
            copy = UpdateDialogCopy.ForLocale(locale);
            DataContext = release;
            Title = copy.Title;
            TitleTextBlock.Text = copy.Title;
            SubtitleTextBlock.Text = string.Format(copy.Subtitle, release.VersionLabel);
            LeadTextBlock.Text = copy.Lead;
            DetailsButton.Content = copy.DetailsButton;
            StoreButton.Content = copy.StoreButton;
        }

        internal static string DetailsUrlForLocale(string locale)
        {
            if (string.Equals(locale, "zh-Hant", StringComparison.OrdinalIgnoreCase)) return "https://lyric-island.top/zh-hant/updates/";
            if (string.Equals(locale, "ja", StringComparison.OrdinalIgnoreCase)) return "https://lyric-island.top/ja/updates/";
            if (string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase)) return "https://lyric-island.top/en/updates/";
            return "https://lyric-island.top/updates/";
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
            StoreButton.Focus();
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            TryOpenExternal(DetailsUrlForLocale(copy.Locale), copy.WebsiteError, null);
        }

        private void StoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryOpenExternal(StoreUri, null, StoreWebUrl))
            {
                Close();
            }
            else
            {
                ShowOpenError(copy.StoreError);
            }
        }

        private bool TryOpenExternal(string target, string error, string fallback)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                return true;
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(fallback) { UseShellExecute = true });
                        return true;
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    ShowOpenError(error);
                }
                return false;
            }
        }

        private void ShowOpenError(string message)
        {
            new InformationDialog(this, copy.ErrorTitle, message).ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private sealed class UpdateDialogCopy
        {
            public string Locale { get; private set; }
            public string Title { get; private set; }
            public string Subtitle { get; private set; }
            public string Lead { get; private set; }
            public string DetailsButton { get; private set; }
            public string StoreButton { get; private set; }
            public string ErrorTitle { get; private set; }
            public string WebsiteError { get; private set; }
            public string StoreError { get; private set; }

            public static UpdateDialogCopy ForLocale(string locale)
            {
                if (string.Equals(locale, "zh-Hant", StringComparison.OrdinalIgnoreCase))
                {
                    return Create("zh-Hant", "發現新版本", "Microsoft Store 已發佈 {0}", "本次更新內容", "查看詳情", "立即更新", "無法開啟連結", "暫時無法開啟官網，請稍後再試。", "暫時無法開啟 Microsoft Store，請稍後再試。");
                }
                if (string.Equals(locale, "ja", StringComparison.OrdinalIgnoreCase))
                {
                    return Create("ja", "新しいバージョンがあります", "Microsoft Store で {0} が公開されました", "アップデート内容", "詳細を見る", "今すぐ更新", "リンクを開けません", "公式サイトを開けませんでした。後でもう一度お試しください。", "Microsoft Store を開けませんでした。後でもう一度お試しください。");
                }
                if (string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase))
                {
                    return Create("en", "A new version is available", "{0} is now available in Microsoft Store", "What's new", "View details", "Update now", "Couldn't open the link", "The website couldn't be opened. Try again later.", "Microsoft Store couldn't be opened. Try again later.");
                }
                return Create("zh", "发现新版本", "Microsoft Store 已发布 {0}", "本次更新内容", "查看详情", "立即更新", "无法打开链接", "暂时无法打开官网，请稍后重试。", "暂时无法打开 Microsoft Store，请稍后重试。");
            }

            private static UpdateDialogCopy Create(string locale, string title, string subtitle, string lead, string detailsButton, string storeButton, string errorTitle, string websiteError, string storeError)
            {
                return new UpdateDialogCopy { Locale = locale, Title = title, Subtitle = subtitle, Lead = lead, DetailsButton = detailsButton, StoreButton = storeButton, ErrorTitle = errorTitle, WebsiteError = websiteError, StoreError = storeError };
            }
        }
    }
}
