using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppleMusicDesktopLyrics.App.LayoutEditing;
using AppleMusicDesktopLyrics.Core.Layout;

namespace AppleMusicDesktopLyrics.App.Modules
{
    public partial class IslandModuleHost : UserControl
    {
        private string layoutSignature = string.Empty;

        public IslandModuleHost()
        {
            InitializeComponent();
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
                module.Id + ":" + module.Type + ":" + module.LyricsWidth.ToString("0.##", CultureInfo.InvariantCulture)));
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
            for (var index = 0; index < ModulePanel.Children.Count; index++)
            {
                targets.Add(new LayoutInsertionTarget(index, x));
                var element = ModulePanel.Children[index] as FrameworkElement;
                x += element?.ActualWidth > 0 ? element.ActualWidth : element?.DesiredSize.Width ?? 0;
            }

            targets.Add(new LayoutInsertionTarget(ModulePanel.Children.Count, x));
            return targets;
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
            DragDrop.DoDragDrop(element, new DataObject(typeof(IslandLayoutDragPayload), payload), DragDropEffects.Move);
        }
    }
}
