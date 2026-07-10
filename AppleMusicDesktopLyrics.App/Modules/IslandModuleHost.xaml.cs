using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        public void ApplyLayout(IslandLayoutProfile profile)
        {
            profile = profile ?? IslandLayoutDefaults.CreateCollapsed();
            profile.Normalize();

            var nextSignature = string.Join("|", profile.Modules.Select(module => module.Id + ":" + module.Type));
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
                        view = new LyricsModuleView();
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

                view.Tag = module.Id;
                ModulePanel.Children.Add(view);
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
    }
}
