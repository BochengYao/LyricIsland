using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using LyricHover.App.LayoutEditing;
using LyricHover.Core;
using Forms = System.Windows.Forms;

namespace LyricHover.App
{
    public partial class SupporterBadgePreviewWindow : Window
    {
        private readonly SupporterBadgeOptions options;
        private readonly SupporterBadgeRotationState rotationState;
        private readonly SupporterBadge3DScene badgeScene;
        private readonly DispatcherTimer glintTimer;
        private readonly Stopwatch glintStopwatch = new Stopwatch();
        private readonly Stopwatch renderStopwatch = new Stopwatch();
        private readonly Stopwatch interactionStopwatch = new Stopwatch();

        private bool isDragging;
        private Point lastPointerPosition;
        private TimeSpan previousRenderTime;
        private TouchDevice activeTouchDevice;

        public SupporterBadgePreviewWindow(
            string supporterNickname,
            DateTimeOffset acquiredAt)
            : this(new SupporterBadgeOptions
            {
                Identity = new SupporterBadgeIdentity
                {
                    DisplayName = supporterNickname,
                    AcquiredDate = acquiredAt
                }
            })
        {
        }

        public SupporterBadgePreviewWindow(SupporterBadgeOptions options)
        {
            this.options = options ?? new SupporterBadgeOptions();
            InitializeComponent();

            var reduceMotion = !SystemParameters.ClientAreaAnimation;
            var initialYaw = this.options.InitialSide == SupporterBadgeInitialSide.Back
                ? 160.0
                : -20.0;
            rotationState = new SupporterBadgeRotationState(
                initialYaw,
                -6,
                this.options.AutoRotate,
                reduceMotion);
            badgeScene = SupporterBadge3DFactory.Create(
                this.options.Identity,
                this);
            UpdateSceneRotation();
            BadgeViewport.Children.Add(new ModelVisual3D { Content = badgeScene.Model });

            var badgeSize = ResolveBadgeSize(this.options.Size);
            BadgeInteractionSurface.Width = badgeSize;
            BadgeInteractionSurface.Height = badgeSize;
            BadgeInteractionSurface.Cursor = LayoutDragCursors.OpenHand;

            glintTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(16),
                DispatcherPriority.Render,
                GlintTimer_Tick,
                Dispatcher)
            {
                IsEnabled = false
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyOwnerMonitorBounds();
            Activate();
            Focus();

            var targetYaw = options.InitialSide == SupporterBadgeInitialSide.Back
                ? 180.0
                : 0.0;
            rotationState.AnimateTo(targetYaw, 0, false);
            previousRenderTime = TimeSpan.Zero;
            renderStopwatch.Restart();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            if (!rotationState.ReduceMotion)
            {
                BeginGlint();
            }
        }

        private static double ResolveBadgeSize(SupporterBadgeSize size)
        {
            switch (size)
            {
                case SupporterBadgeSize.Compact:
                    return 520;
                case SupporterBadgeSize.Regular:
                    return 640;
                default:
                    return 720;
            }
        }

        private void ApplyOwnerMonitorBounds()
        {
            var ownerHandle = Owner == null
                ? IntPtr.Zero
                : new WindowInteropHelper(Owner).Handle;
            var screen = ownerHandle == IntPtr.Zero
                ? Forms.Screen.FromPoint(Forms.Cursor.Position)
                : Forms.Screen.FromHandle(ownerHandle);

            var transform = Matrix.Identity;
            var ownerSource = Owner == null
                ? null
                : PresentationSource.FromVisual(Owner);
            if (ownerSource?.CompositionTarget != null)
            {
                transform = ownerSource.CompositionTarget.TransformFromDevice;
            }

            var topLeft = transform.Transform(new Point(screen.Bounds.Left, screen.Bounds.Top));
            var bottomRight = transform.Transform(new Point(screen.Bounds.Right, screen.Bounds.Bottom));
            Left = topLeft.X;
            Top = topLeft.Y;
            Width = bottomRight.X - topLeft.X;
            Height = bottomRight.Y - topLeft.Y;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            var elapsed = renderStopwatch.Elapsed;
            var delta = previousRenderTime == TimeSpan.Zero
                ? 1.0 / 60.0
                : (elapsed - previousRenderTime).TotalSeconds;
            previousRenderTime = elapsed;
            if (rotationState.Advance(delta))
            {
                UpdateSceneRotation();
            }
        }

        private void UpdateSceneRotation()
        {
            badgeScene.YawRotation.Angle = rotationState.Yaw;
            badgeScene.PitchRotation.Angle = rotationState.Pitch;
        }

        private void BeginGlint()
        {
            glintStopwatch.Restart();
            glintTimer.Start();
        }

        private void GlintTimer_Tick(object sender, EventArgs e)
        {
            const double durationMilliseconds = 900.0;
            var progress = Math.Min(
                1.0,
                glintStopwatch.Elapsed.TotalMilliseconds / durationMilliseconds);
            var envelope = Math.Sin(progress * Math.PI);
            var strength = (byte)Math.Round(255 * 0.92 * envelope);
            badgeScene.GlintLight.Color = Color.FromRgb(
                strength,
                (byte)Math.Round(strength * 0.90),
                (byte)Math.Round(strength * 0.60));
            badgeScene.GlintLight.Position = new Point3D(
                -3.2 + progress * 6.4,
                1.35 - progress * 0.5,
                3.3);

            if (progress >= 1.0)
            {
                StopGlint();
            }
        }

        private void StopGlint()
        {
            glintTimer.Stop();
            glintStopwatch.Stop();
            badgeScene.GlintLight.Color = Colors.Black;
        }

        private void BeginPointerInteraction(Point point)
        {
            isDragging = true;
            lastPointerPosition = point;
            interactionStopwatch.Restart();
            rotationState.BeginInteraction();
            BadgeInteractionSurface.Cursor = LayoutDragCursors.ClosedHand;
        }

        private void ApplyPointerInteraction(Point point)
        {
            if (!isDragging)
            {
                return;
            }

            var elapsed = Math.Max(1.0 / 240.0, interactionStopwatch.Elapsed.TotalSeconds);
            rotationState.ApplyDrag(
                point.X - lastPointerPosition.X,
                point.Y - lastPointerPosition.Y,
                elapsed);
            lastPointerPosition = point;
            interactionStopwatch.Restart();
            UpdateSceneRotation();
        }

        private void EndPointerInteraction()
        {
            if (isDragging)
            {
                rotationState.EndInteraction();
            }
            isDragging = false;
            interactionStopwatch.Stop();
            BadgeInteractionSurface.Cursor = LayoutDragCursors.OpenHand;
        }

        private void BadgeInteractionSurface_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            BeginPointerInteraction(e.GetPosition(BadgeInteractionSurface));
            BadgeInteractionSurface.CaptureMouse();
            e.Handled = true;
        }

        private void BadgeInteractionSurface_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (!BadgeInteractionSurface.IsMouseCaptured)
            {
                return;
            }

            ApplyPointerInteraction(e.GetPosition(BadgeInteractionSurface));
            e.Handled = true;
        }

        private void BadgeInteractionSurface_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            ReleasePointerCapture();
            e.Handled = true;
        }

        private void BadgeInteractionSurface_LostMouseCapture(
            object sender,
            MouseEventArgs e)
        {
            EndPointerInteraction();
        }

        private void BadgeInteractionSurface_TouchDown(object sender, TouchEventArgs e)
        {
            if (activeTouchDevice != null)
            {
                return;
            }

            activeTouchDevice = e.TouchDevice;
            BeginPointerInteraction(e.GetTouchPoint(BadgeInteractionSurface).Position);
            BadgeInteractionSurface.CaptureTouch(activeTouchDevice);
            e.Handled = true;
        }

        private void BadgeInteractionSurface_TouchMove(object sender, TouchEventArgs e)
        {
            if (e.TouchDevice != activeTouchDevice)
            {
                return;
            }

            ApplyPointerInteraction(e.GetTouchPoint(BadgeInteractionSurface).Position);
            e.Handled = true;
        }

        private void BadgeInteractionSurface_TouchUp(object sender, TouchEventArgs e)
        {
            if (e.TouchDevice != activeTouchDevice)
            {
                return;
            }

            ReleasePointerCapture();
            e.Handled = true;
        }

        private void BadgeInteractionSurface_LostTouchCapture(
            object sender,
            TouchEventArgs e)
        {
            if (e.TouchDevice == activeTouchDevice)
            {
                activeTouchDevice = null;
                EndPointerInteraction();
            }
        }

        private void ReleasePointerCapture()
        {
            EndPointerInteraction();
            if (BadgeInteractionSurface.IsMouseCaptured)
            {
                BadgeInteractionSurface.ReleaseMouseCapture();
            }
            if (activeTouchDevice != null)
            {
                var touchDevice = activeTouchDevice;
                activeTouchDevice = null;
                BadgeInteractionSurface.ReleaseTouchCapture(touchDevice);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            ReleasePointerCapture();
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            renderStopwatch.Stop();
            ReleasePointerCapture();
            StopGlint();
            BadgeViewport.Children.Clear();
        }
    }
}
