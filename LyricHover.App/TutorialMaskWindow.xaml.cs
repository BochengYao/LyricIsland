using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace LyricHover.App
{
    public partial class TutorialMaskWindow : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;

        public TutorialMaskWindow()
        {
            InitializeComponent();
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            SourceInitialized += (sender, args) => MakeMouseTransparentAndNonActivating();
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        private void MakeMouseTransparentAndNonActivating()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle, style | WsExTransparent | WsExNoActivate | WsExToolWindow);
        }

        public Task FadeInAsync(TimeSpan duration)
        {
            return AnimateOpacityAsync(1, duration);
        }

        public Task FadeOutAsync(TimeSpan duration)
        {
            return AnimateOpacityAsync(0, duration);
        }

        private Task AnimateOpacityAsync(double target, TimeSpan duration)
        {
            var completion = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimation(target, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            animation.Completed += (sender, args) =>
            {
                Opacity = target;
                BeginAnimation(OpacityProperty, null);
                completion.TrySetResult(true);
            };
            BeginAnimation(OpacityProperty, animation);
            return completion.Task;
        }
    }
}
