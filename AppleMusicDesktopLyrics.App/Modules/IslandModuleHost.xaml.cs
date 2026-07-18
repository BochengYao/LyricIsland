using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppleMusicDesktopLyrics.App.LayoutEditing;
using AppleMusicDesktopLyrics.Core.Layout;

namespace AppleMusicDesktopLyrics.App.Modules
{
    public partial class IslandModuleHost : UserControl
    {
        private string layoutSignature = string.Empty;
        private bool playbackInteractionEnabled;
        private readonly Border insertionPlaceholder;
        private int insertionPreviewIndex = -1;
        private double insertionPreviewWidth = -1;

        public IslandModuleHost()
        {
            InitializeComponent();
            insertionPlaceholder = new Border
            {
                Width = 44,
                Height = 42,
                Margin = new Thickness(5, 3, 5, 3),
                Background = (Brush)new BrushConverter().ConvertFromString("#181677FF"),
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#661677FF"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(9),
                IsHitTestVisible = false
            };
        }

        public event EventHandler PreviousRequested;
        public event EventHandler PlayPauseRequested;
        public event EventHandler NextRequested;

        public bool LayoutEditingEnabled { get; set; }

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
            ModulePanel.Children.Clear();

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
                        view = new TrackInfoModuleView();
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
                view.PreviewMouseMove += ModuleView_PreviewMouseMove;
                ModulePanel.Children.Add(view);
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
            var x = 0.0;
            var modules = ModulePanel.Children
                .OfType<FrameworkElement>()
                .Where(element => !ReferenceEquals(element, insertionPlaceholder))
                .ToList();
            for (var index = 0; index < modules.Count; index++)
            {
                targets.Add(new LayoutInsertionTarget(index, x));
                var element = modules[index];
                x += element?.ActualWidth > 0 ? element.ActualWidth : element?.DesiredSize.Width ?? 0;
            }

            targets.Add(new LayoutInsertionTarget(modules.Count, x));
            return targets;
        }

        public void ShowInsertionPreview(int index, double suggestedWidth)
        {
            var previewWidth = Math.Max(28, Math.Min(120, suggestedWidth));
            if (insertionPreviewIndex == index &&
                Math.Abs(insertionPreviewWidth - previewWidth) < 0.5 &&
                ModulePanel.Children.Contains(insertionPlaceholder))
            {
                return;
            }

            ModulePanel.Children.Remove(insertionPlaceholder);
            insertionPlaceholder.Width = previewWidth;
            var moduleCount = ModulePanel.Children.Count;
            insertionPreviewIndex = Math.Max(0, Math.Min(index, moduleCount));
            insertionPreviewWidth = previewWidth;
            ModulePanel.Children.Insert(insertionPreviewIndex, insertionPlaceholder);
        }

        public void ClearInsertionPreview()
        {
            if (ModulePanel.Children.Contains(insertionPlaceholder))
            {
                ModulePanel.Children.Remove(insertionPlaceholder);
                insertionPreviewIndex = -1;
                insertionPreviewWidth = -1;
            }
        }

        public double GetDragPreviewWidth(IslandLayoutDragPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(payload?.ExistingInstanceId))
            {
                var existing = ModulePanel.Children
                    .OfType<FrameworkElement>()
                    .FirstOrDefault(element => string.Equals(element.Tag as string, payload.ExistingInstanceId, StringComparison.Ordinal));
                if (existing != null)
                {
                    return existing.ActualWidth > 0 ? existing.ActualWidth : existing.DesiredSize.Width;
                }
            }

            switch (payload?.NewType)
            {
                case IslandModuleType.AlbumArt: return 60;
                case IslandModuleType.PlaybackControls: return 108;
                case IslandModuleType.TrackInfo: return 168;
                case IslandModuleType.Progress: return 148;
                case IslandModuleType.Divider: return 18;
                case IslandModuleType.Lyrics: return 120;
                default: return 44;
            }
        }

        private void ModuleView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!LayoutEditingEnabled || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var id = element?.Tag as string;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var payload = new IslandLayoutDragPayload { ExistingInstanceId = id };
            element.Opacity = 0.38;
            try
            {
                DragDrop.DoDragDrop(element, new DataObject(typeof(IslandLayoutDragPayload), payload), DragDropEffects.Move);
            }
            finally
            {
                element.Opacity = 1.0;
                ClearInsertionPreview();
            }
        }
    }
}
