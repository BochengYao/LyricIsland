using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LyricHover.App.LayoutEditing;
using LyricHover.Core.Layout;

namespace LyricHover.App.Modules
{
    public partial class IslandModuleHost : UserControl
    {
        private string layoutSignature = string.Empty;
        private bool playbackInteractionEnabled;
        private IslandRenderState lastRenderState = new IslandRenderState();
        private int insertionPreviewIndex = -1;
        private double insertionPreviewWidth = -1;
        private Point? moduleDragStartPoint;
        private string moduleDragSourceId;
        private bool moduleDragInProgress;
        private bool layoutEditingEnabled;
        private readonly Border dragPlaceholder;
        private readonly Dictionary<string, IslandModuleType> moduleTypesById =
            new Dictionary<string, IslandModuleType>(StringComparer.Ordinal);
        private string previewSourceId;
        private int previewFinalIndex = -1;
        private ModuleDragGhostWindow dragGhost;

        public IslandModuleHost()
        {
            InitializeComponent();
            dragPlaceholder = new Border
            {
                Height = ModulePreviewMetrics.Height,
                Margin = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x16, 0x77, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x16, 0x77, 0xFF)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(9),
                IsHitTestVisible = false
            };
        }

        public event EventHandler PreviousRequested;
        public event EventHandler PlayPauseRequested;
        public event EventHandler NextRequested;
        public event EventHandler ModuleDragStarted;
        public event EventHandler<ModuleDragCompletedEventArgs> ModuleDragCompleted;
        public event EventHandler ContentSizeChanged;

        public bool LayoutEditingEnabled
        {
            get => layoutEditingEnabled;
            set
            {
                if (layoutEditingEnabled == value)
                {
                    return;
                }

                layoutEditingEnabled = value;
                UpdateModuleDragCursors();
            }
        }

        public void ApplyLayout(IslandLayoutProfile profile)
        {
            profile = profile ?? IslandLayoutDefaults.CreateCollapsed();
            profile.Normalize();

            var nextSignature = string.Join("|", profile.Modules.Select(module =>
                module.Id + ":" + module.Type + ":" +
                module.LyricsWidth.ToString("0.##", CultureInfo.InvariantCulture) + ":" +
                module.DividerOpacity.ToString("0.###", CultureInfo.InvariantCulture) + ":" +
                module.MarginBefore.ToString("0.##", CultureInfo.InvariantCulture) + ":" +
                module.MarginAfter.ToString("0.##", CultureInfo.InvariantCulture)));
            if (nextSignature == layoutSignature)
            {
                return;
            }

            layoutSignature = nextSignature;
            ClearInsertionPreview(false);
            ModulePanel.Children.Clear();
            moduleTypesById.Clear();

            foreach (var module in profile.Modules)
            {
                FrameworkElement view;
                switch (module.Type)
                {
                    case IslandModuleType.Lyrics:
                        var lyrics = new LyricsModuleView();
                        lyrics.ApplyModuleSettings(module.LyricsWidth);
                        view = lyrics;
                        break;
                    case IslandModuleType.AlbumArt:
                        view = new AlbumArtModuleView();
                        break;
                    case IslandModuleType.PlaybackControls:
                        var controls = new PlaybackControlsModuleView();
                        controls.SetInteractionEnabled(playbackInteractionEnabled);
                        controls.PreviousRequested += (sender, args) => PreviousRequested?.Invoke(this, EventArgs.Empty);
                        controls.PlayPauseRequested += (sender, args) => PlayPauseRequested?.Invoke(this, EventArgs.Empty);
                        controls.NextRequested += (sender, args) => NextRequested?.Invoke(this, EventArgs.Empty);
                        view = controls;
                        break;
                    case IslandModuleType.TrackInfo:
                        var trackInfo = new TrackInfoModuleView();
                        trackInfo.PreferredWidthChanged += (sender, args) =>
                            ContentSizeChanged?.Invoke(this, EventArgs.Empty);
                        view = trackInfo;
                        break;
                    case IslandModuleType.Progress:
                        view = new ProgressModuleView();
                        break;
                    case IslandModuleType.Divider:
                        view = new DividerModuleView(module);
                        break;
                    default:
                        continue;
                }

                ApplyModuleSettings(view, module);
                view.Tag = module.Id;
                moduleTypesById[module.Id] = module.Type;
                view.PreviewMouseLeftButtonDown += ModuleView_PreviewMouseLeftButtonDown;
                view.PreviewMouseLeftButtonUp += ModuleView_PreviewMouseLeftButtonUp;
                view.PreviewMouseMove += ModuleView_PreviewMouseMove;
                view.GiveFeedback += ModuleView_GiveFeedback;
                view.Cursor = LayoutEditingEnabled ? LayoutDragCursors.OpenHand : Cursors.Arrow;
                ModulePanel.Children.Add(view);
            }

            // A layout change creates fresh module views. Immediately replay the latest render
            // state so the island never shows an empty black shell between tutorial steps.
            Update(lastRenderState);
        }

        public Size MeasureContentSize()
        {
            ModulePanel.InvalidateMeasure();
            ModulePanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return ModulePanel.DesiredSize;
        }

        private void UpdateModuleDragCursors()
        {
            var cursor = LayoutEditingEnabled ? LayoutDragCursors.OpenHand : Cursors.Arrow;
            foreach (var module in ModulePanel.Children.OfType<FrameworkElement>())
            {
                module.Cursor = cursor;
            }
        }

        private static void ApplyModuleSettings(FrameworkElement view, IslandModuleInstance module)
        {
            var lyrics = view as LyricsModuleView;
            if (lyrics != null)
            {
                lyrics.ApplyModuleSettings(module.LyricsWidth);
            }
        }

        public void Update(IslandRenderState state)
        {
            state = state ?? new IslandRenderState();
            lastRenderState = state;
            foreach (var child in ModulePanel.Children.OfType<IIslandModuleView>())
            {
                child.Update(state);
            }
        }

        public void SetPlaybackInteractionEnabled(bool value)
        {
            if (playbackInteractionEnabled == value)
            {
                return;
            }

            playbackInteractionEnabled = value;
            foreach (var controls in ModulePanel.Children.OfType<PlaybackControlsModuleView>())
            {
                controls.SetInteractionEnabled(value);
            }
        }

        public Task AnimateModulesInAsync(
            IReadOnlyList<string> instanceIds,
            int durationMilliseconds,
            int staggerMilliseconds,
            CancellationToken cancellationToken)
        {
            return AnimateModulesOpacityAsync(
                instanceIds,
                true,
                durationMilliseconds,
                staggerMilliseconds,
                cancellationToken);
        }

        public Task AnimateModulesOutAsync(
            IReadOnlyList<string> instanceIds,
            int durationMilliseconds,
            int staggerMilliseconds,
            CancellationToken cancellationToken)
        {
            return AnimateModulesOpacityAsync(
                instanceIds,
                false,
                durationMilliseconds,
                staggerMilliseconds,
                cancellationToken);
        }

        private async Task AnimateModulesOpacityAsync(
            IReadOnlyList<string> instanceIds,
            bool fadeIn,
            int durationMilliseconds,
            int staggerMilliseconds,
            CancellationToken cancellationToken)
        {
            var modules = (instanceIds ?? Array.Empty<string>())
                .Select(FindModuleElement)
                .Where(module => module != null)
                .ToList();
            if (modules.Count == 0)
            {
                return;
            }

            durationMilliseconds = Math.Max(80, durationMilliseconds);
            staggerMilliseconds = Math.Max(0, Math.Min(durationMilliseconds - 20, staggerMilliseconds));
            for (var index = 0; index < modules.Count; index++)
            {
                var module = modules[index];
                module.BeginAnimation(OpacityProperty, null);
                module.Opacity = fadeIn ? 0 : 1;
                var animation = new DoubleAnimation(fadeIn ? 1 : 0, TimeSpan.FromMilliseconds(durationMilliseconds))
                {
                    BeginTime = TimeSpan.FromMilliseconds(staggerMilliseconds * index),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = fadeIn ? EasingMode.EaseOut : EasingMode.EaseIn
                    },
                    FillBehavior = FillBehavior.HoldEnd
                };
                module.BeginAnimation(OpacityProperty, animation);
            }

            try
            {
                await Task.Delay(
                    durationMilliseconds + staggerMilliseconds * (modules.Count - 1),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                foreach (var module in modules)
                {
                    module.BeginAnimation(OpacityProperty, null);
                    module.Opacity = 1;
                }
                throw;
            }

            foreach (var module in modules)
            {
                module.BeginAnimation(OpacityProperty, null);
                module.Opacity = fadeIn ? 1 : 0;
            }
        }

        public void ShowTransientMessage(string message, TimeSpan duration)
        {
            Update(new IslandRenderState
            {
                PrimaryLyric = message ?? string.Empty,
                SecondaryLyric = string.Empty,
                LineDuration = duration
            });
        }

        public IReadOnlyList<LayoutInsertionTarget> BuildInsertionTargets()
        {
            var targets = new List<LayoutInsertionTarget>();
            var panelLeft = ModulePanel.TranslatePoint(new Point(0, 0), this).X;
            var modules = GetModuleElements()
                .Where(element => element.Visibility != Visibility.Collapsed)
                .ToList();

            targets.Add(new LayoutInsertionTarget(0, panelLeft));
            for (var index = 1; index < modules.Count; index++)
            {
                var previous = modules[index - 1];
                var current = modules[index];
                var previousWidth = previous.ActualWidth > 0 ? previous.ActualWidth : previous.DesiredSize.Width;
                var previousRight = previous.TranslatePoint(new Point(previousWidth, 0), this).X;
                var currentLeft = current.TranslatePoint(new Point(0, 0), this).X;
                targets.Add(new LayoutInsertionTarget(index, (previousRight + currentLeft) / 2));
            }

            if (modules.Count > 0)
            {
                var panelWidth = ModulePanel.ActualWidth > 0 ? ModulePanel.ActualWidth : ModulePanel.DesiredSize.Width;
                targets.Add(new LayoutInsertionTarget(modules.Count, panelLeft + panelWidth));
            }

            return targets;
        }

        public int FindInsertionIndex(double pointerX, IslandLayoutDragPayload payload)
        {
            var panelLeft = ModulePanel.TranslatePoint(new Point(0, 0), this).X;
            var modules = GetModuleElements()
                .Where(element => !string.Equals(element.Tag as string, payload?.ExistingInstanceId, StringComparison.Ordinal))
                .ToList();
            for (var index = 0; index < modules.Count; index++)
            {
                var module = modules[index];
                var width = module.ActualWidth > 0 ? module.ActualWidth : module.DesiredSize.Width;
                var left = panelLeft + VisualTreeHelper.GetOffset(module).X;
                if (pointerX < left + width / 2)
                {
                    return index;
                }
            }

            return modules.Count;
        }

        public void ShowInsertionPreview(int index, double suggestedWidth, IslandLayoutDragPayload payload)
        {
            var previewWidth = Math.Max(18, Math.Min(560, suggestedWidth));
            var modules = GetModuleElements()
                .Where(element => !string.Equals(element.Tag as string, payload?.ExistingInstanceId, StringComparison.Ordinal))
                .ToList();
            index = Math.Max(0, Math.Min(index, modules.Count));
            if (previewFinalIndex == index &&
                string.Equals(previewSourceId, payload?.ExistingInstanceId, StringComparison.Ordinal) &&
                Math.Abs(insertionPreviewWidth - previewWidth) < 0.5 &&
                ModulePanel.Children.Contains(dragPlaceholder))
            {
                return;
            }

            var positions = CaptureModulePositions();
            if (ModulePanel.Children.Contains(dragPlaceholder))
            {
                ModulePanel.Children.Remove(dragPlaceholder);
            }

            RestorePreviewSource();
            previewSourceId = payload?.ExistingInstanceId;
            var source = FindModuleElement(previewSourceId);
            if (source != null)
            {
                source.Visibility = Visibility.Collapsed;
            }

            modules = GetModuleElements()
                .Where(element => !string.Equals(element.Tag as string, previewSourceId, StringComparison.Ordinal))
                .ToList();
            index = Math.Max(0, Math.Min(index, modules.Count));
            var childIndex = index < modules.Count
                ? ModulePanel.Children.IndexOf(modules[index])
                : ModulePanel.Children.Count;
            dragPlaceholder.Width = previewWidth;
            ModulePanel.Children.Insert(childIndex, dragPlaceholder);
            insertionPreviewIndex = index;
            previewFinalIndex = index;
            insertionPreviewWidth = previewWidth;
            InsertionIndicator.Visibility = Visibility.Collapsed;
            AnimateModuleReflow(positions);
        }

        public void ClearInsertionPreview()
        {
            ClearInsertionPreview(true);
        }

        private void ClearInsertionPreview(bool animate)
        {
            var positions = animate ? CaptureModulePositions() : null;
            if (ModulePanel.Children.Contains(dragPlaceholder))
            {
                ModulePanel.Children.Remove(dragPlaceholder);
            }
            RestorePreviewSource();
            InsertionIndicator.Visibility = Visibility.Collapsed;
            insertionPreviewIndex = -1;
            insertionPreviewWidth = -1;
            previewFinalIndex = -1;
            previewSourceId = null;
            if (animate)
            {
                AnimateModuleReflow(positions);
            }
        }

        public void ShowRemovalPreview(string instanceId)
        {
            var positions = CaptureModulePositions();
            if (ModulePanel.Children.Contains(dragPlaceholder))
            {
                ModulePanel.Children.Remove(dragPlaceholder);
            }
            RestorePreviewSource();
            previewSourceId = instanceId;
            var source = FindModuleElement(instanceId);
            if (source != null)
            {
                source.Visibility = Visibility.Collapsed;
            }
            previewFinalIndex = -1;
            insertionPreviewIndex = -1;
            AnimateModuleReflow(positions);
        }

        public int GetCommittedInsertionIndex(IslandLayoutDragPayload payload, int destinationIndex = -1)
        {
            var destination = Math.Max(0, destinationIndex >= 0 ? destinationIndex : previewFinalIndex);
            if (string.IsNullOrWhiteSpace(payload?.ExistingInstanceId))
            {
                return destination;
            }

            var modules = GetModuleElements().ToList();
            var sourceIndex = modules.FindIndex(element =>
                string.Equals(element.Tag as string, payload.ExistingInstanceId, StringComparison.Ordinal));
            return ModuleLayoutProjection.ToMoveInsertionIndex(sourceIndex, destination, modules.Count);
        }

        public double GetDragPreviewWidth(IslandLayoutDragPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(payload?.ExistingInstanceId))
            {
                IslandModuleType existingType;
                if (moduleTypesById.TryGetValue(payload.ExistingInstanceId, out existingType))
                {
                    return ModulePreviewMetrics.GetWidth(existingType);
                }
            }

            return payload?.NewType.HasValue == true
                ? ModulePreviewMetrics.GetWidth(payload.NewType.Value)
                : ModulePreviewMetrics.GetWidth(IslandModuleType.AlbumArt);
        }

        private void ModuleView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!LayoutEditingEnabled || moduleDragInProgress)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var id = element?.Tag as string;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            moduleDragStartPoint = e.GetPosition(this);
            moduleDragSourceId = id;
            element.Cursor = LayoutDragCursors.ClosedHand;
            element.CaptureMouse();
            e.Handled = true;
        }

        private void ModuleView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            moduleDragStartPoint = null;
            moduleDragSourceId = null;
            var element = sender as FrameworkElement;
            if (element != null)
            {
                element.Cursor = LayoutEditingEnabled ? LayoutDragCursors.OpenHand : Cursors.Arrow;
            }
            if (element?.IsMouseCaptured == true)
            {
                element.ReleaseMouseCapture();
            }
        }

        private void ModuleView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!LayoutEditingEnabled || moduleDragInProgress ||
                e.LeftButton != MouseButtonState.Pressed || !moduleDragStartPoint.HasValue)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var id = element?.Tag as string;
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, moduleDragSourceId, StringComparison.Ordinal))
            {
                return;
            }

            var point = e.GetPosition(this);
            if (Math.Abs(point.X - moduleDragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(point.Y - moduleDragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var payload = new IslandLayoutDragPayload { ExistingInstanceId = id };
            moduleDragStartPoint = null;
            moduleDragSourceId = null;
            moduleDragInProgress = true;
            if (element.IsMouseCaptured)
            {
                element.ReleaseMouseCapture();
            }
            IslandModuleType draggedType;
            if (!moduleTypesById.TryGetValue(id, out draggedType))
            {
                draggedType = IslandModuleType.AlbumArt;
            }
            var payloadDescriptor = ModuleToolboxCatalog.Get(draggedType);
            dragGhost = new ModuleDragGhostWindow(payloadDescriptor);
            dragGhost.Show();
            dragGhost.UpdatePosition();
            ModuleDragStarted?.Invoke(this, EventArgs.Empty);
            var mouseReleaseObserved = false;
            var wasCancelled = false;
            QueryContinueDragEventHandler continueDrag = (dragSender, dragArgs) =>
            {
                if (dragArgs.EscapePressed)
                {
                    wasCancelled = true;
                    dragArgs.Action = DragAction.Cancel;
                    dragArgs.Handled = true;
                }
                else if ((dragArgs.KeyStates & DragDropKeyStates.LeftMouseButton) == 0)
                {
                    mouseReleaseObserved = true;
                    dragArgs.Action = DragAction.Drop;
                    dragArgs.Handled = true;
                }
            };
            element.QueryContinueDrag += continueDrag;
            var result = DragDropEffects.None;
            try
            {
                result = DragDrop.DoDragDrop(
                    element,
                    IslandLayoutDragPayload.CreateDataObject(payload),
                    DragDropEffects.Move);
            }
            finally
            {
                element.QueryContinueDrag -= continueDrag;
                moduleDragInProgress = false;
                if (element.IsMouseCaptured)
                {
                    element.ReleaseMouseCapture();
                }
                element.Cursor = LayoutEditingEnabled ? LayoutDragCursors.OpenHand : Cursors.Arrow;
                ClearInsertionPreview();
                dragGhost?.Close();
                dragGhost = null;
                var acceptedByIsland = result == DragDropEffects.Move;
                var droppedOutside = ModuleDragCompletionDecision.ShouldDeleteExistingModule(
                    true,
                    mouseReleaseObserved,
                    wasCancelled,
                    acceptedByIsland);
                ModuleDragCompleted?.Invoke(
                    this,
                    new ModuleDragCompletedEventArgs(
                        payload.ExistingInstanceId,
                        payload.OperationId,
                        droppedOutside,
                        wasCancelled));
            }
        }

        private void ModuleView_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            dragGhost?.UpdatePosition();
            Mouse.SetCursor(LayoutDragCursors.ClosedHand);
            e.UseDefaultCursors = false;
            e.Handled = true;
        }

        private List<FrameworkElement> GetModuleElements()
        {
            return ModulePanel.Children
                .OfType<FrameworkElement>()
                .Where(element => !ReferenceEquals(element, dragPlaceholder))
                .ToList();
        }

        private FrameworkElement FindModuleElement(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            return GetModuleElements().FirstOrDefault(element =>
                string.Equals(element.Tag as string, instanceId, StringComparison.Ordinal));
        }

        private void RestorePreviewSource()
        {
            var source = FindModuleElement(previewSourceId);
            if (source != null)
            {
                source.Visibility = Visibility.Visible;
            }
        }

        private Dictionary<FrameworkElement, double> CaptureModulePositions()
        {
            var positions = new Dictionary<FrameworkElement, double>();
            foreach (var element in GetModuleElements().Where(item => item.Visibility == Visibility.Visible))
            {
                positions[element] = element.TranslatePoint(new Point(0, 0), this).X;
            }
            return positions;
        }

        private void AnimateModuleReflow(Dictionary<FrameworkElement, double> previousPositions)
        {
            ModulePanel.UpdateLayout();
            if (previousPositions == null)
            {
                return;
            }

            foreach (var element in GetModuleElements().Where(item => item.Visibility == Visibility.Visible))
            {
                double previousX;
                if (!previousPositions.TryGetValue(element, out previousX))
                {
                    continue;
                }

                var transform = element.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    transform = new TranslateTransform();
                    element.RenderTransform = transform;
                }
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                transform.X = 0;
                var nextX = element.TranslatePoint(new Point(0, 0), this).X;
                var delta = previousX - nextX;
                if (Math.Abs(delta) < 0.5)
                {
                    continue;
                }

                transform.X = delta;
                var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                };
                animation.Completed += (sender, args) => transform.X = 0;
                transform.BeginAnimation(TranslateTransform.XProperty, animation);
            }
        }

    }
}
