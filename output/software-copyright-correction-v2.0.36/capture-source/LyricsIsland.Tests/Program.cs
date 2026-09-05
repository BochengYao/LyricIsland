using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LyricsIsland.Core;
using LyricsIsland.Core.Layout;
using LyricsIsland.Core.Media;

namespace LyricsIsland.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            var suite = new TestSuite();
            suite.Run("parses synced lrc lines and metadata", ParsesSyncedLrcLinesAndMetadata);
            suite.Run("selects the current lyric line by playback position", SelectsCurrentLyricLineByPlaybackPosition);
            suite.Run("selects the current lyric line with timing offset", SelectsCurrentLyricLineWithTimingOffset);
            suite.Run("selects current and next lyric lines", SelectsCurrentAndNextLyricLines);
            suite.Run("selects current lyric line with translation", SelectsCurrentLyricLineWithTranslation);
            suite.Run("selects one display line when multiline is disabled", SelectsOneDisplayLineWhenMultilineIsDisabled);
            suite.Run("selects translated display lines when translation is enabled", SelectsTranslatedDisplayLinesWhenTranslationIsEnabled);
            suite.Run("ignores translation setting for Chinese lyrics", IgnoresTranslationSettingForChineseLyrics);
            suite.Run("keeps translation setting for Japanese lyrics", KeepsTranslationSettingForJapaneseLyrics);
            suite.Run("uses one line for translation placeholder", UsesOneLineForTranslationPlaceholder);
            suite.Run("does not reuse stale translation for next lyric", DoesNotReuseStaleTranslationForNextLyric);
            suite.Run("parses lyrics package without translation", ParsesLyricsPackageWithoutTranslation);
            suite.Run("detects whether lyrics package has translation", DetectsWhetherLyricsPackageHasTranslation);
            suite.Run("gets current lyric line duration", GetsCurrentLyricLineDuration);
            suite.Run("tracks lyric text changes for animation", TracksLyricTextChangesForAnimation);
            suite.Run("positions lyric text before transition", PositionsLyricTextBeforeTransition);
            suite.Run("returns an empty line before the first lyric", ReturnsEmptyLineBeforeFirstLyric);
            suite.Run("keeps the previous lyric across empty timestamp markers", KeepsPreviousLyricAcrossEmptyTimestampMarkers);
            suite.Run("builds stable cache paths from song identity", BuildsStableCachePathsFromSongIdentity);
            suite.Run("migrates legacy product data to the renamed directory", MigratesLegacyProductDataDirectory);
            suite.Run("reuses cached lyrics when reported duration drifts", ReusesCachedLyricsWhenReportedDurationDrifts);
            suite.Run("evicts least recently used song cache files to stay under size limit", EvictsLeastRecentlyUsedSongCacheFilesToStayUnderSizeLimit);
            suite.Run("uses lrc lib search as the primary lyrics lookup", UsesLrcLibSearchAsPrimaryLyricsLookup);
            suite.Run("falls back from album scoped lrc lib search", FallsBackFromAlbumScopedLrcLibSearch);
            suite.Run("returns empty lyrics when lrc lib reports 404", ReturnsEmptyLyricsWhenLrcLibReports404);
            suite.Run("returns empty lyrics when lrc lib request times out", ReturnsEmptyLyricsWhenLrcLibRequestTimesOut);
            suite.Run("fetches synced lyrics from netease response", FetchesSyncedLyricsFromNetEaseResponse);
            suite.Run("fetches translated lyrics from netease response", FetchesTranslatedLyricsFromNetEaseResponse);
            suite.Run("fetches synced lyrics from qq music response", FetchesSyncedLyricsFromQqMusicResponse);
            suite.Run("fetches translated lyrics from qq music response", FetchesTranslatedLyricsFromQqMusicResponse);
            suite.Run("fetches synced lyrics from kugou response", FetchesSyncedLyricsFromKuGouResponse);
            suite.Run("scores lyric candidates by title artist and duration", ScoresLyricCandidatesByTitleArtistAndDuration);
            suite.Run("uses fallback lyrics source when primary source is empty", UsesFallbackLyricsSourceWhenPrimarySourceIsEmpty);
            suite.Run("prefers translated fallback lyrics source", PrefersTranslatedFallbackLyricsSource);
            suite.Run("uses fallback lyrics source when primary source throws", UsesFallbackLyricsSourceWhenPrimarySourceThrows);
            suite.Run("cleans combined now playing titles", CleansCombinedNowPlayingTitles);
            suite.Run("removes featured artist credit from now playing titles", RemovesFeaturedArtistCreditFromNowPlayingTitles);
            suite.Run("removes album suffix from now playing artist", RemovesAlbumSuffixFromNowPlayingArtist);
            suite.Run("matches lyric candidates when now playing title includes featured artist", MatchesLyricCandidatesWhenNowPlayingTitleIncludesFeaturedArtist);
            suite.Run("prefers locked media session", PrefersLockedMediaSession);
            suite.Run("prefers most recently active playing session", PrefersMostRecentlyActivePlayingSession);
            suite.Run("prefers Windows current media session", PrefersWindowsCurrentMediaSession);
            suite.Run("falls back when locked session disappears", FallsBackWhenLockedSessionDisappears);
            suite.Run("ignores non-music media sessions", IgnoresNonMusicMediaSessions);
            suite.Run("classifies target SMTC players", ClassifiesTargetSmtcPlayers);
            suite.Run("locks a known player across SMTC session ids", LocksKnownPlayerAcrossSessionIds);
            suite.Run("uses generic profile for unknown players", UsesGenericProfileForUnknownPlayers);
            suite.Run("playback intent follows the latest click", PlaybackIntentFollowsLatestClick);
            suite.Run("playback intent cancels when session changes", PlaybackIntentCancelsWhenSessionChanges);
            suite.Run("creates independent A and C layouts", CreatesIndependentAAndCLayouts);
            suite.Run("keeps repeated divider modules", KeepsRepeatedDividerModules);
            suite.Run("settings schema contains independent layouts", SettingsSchemaContainsIndependentLayouts);
            suite.Run("tracks normalized settings dirty state", TracksNormalizedSettingsDirtyState);
            suite.Run("layout draft snapshots are isolated", LayoutDraftSnapshotsAreIsolated);
            suite.Run("settings store backs up corrupt JSON", SettingsStoreBacksUpCorruptJson);
            suite.Run("builds island geometry for measured module size", BuildsIslandGeometryForMeasuredModuleSize);
            suite.Run("module host exposes all v2 module views", ModuleHostExposesAllV2ModuleViews);
            suite.Run("track info shows title and artist without album", TrackInfoShowsTitleAndArtistWithoutAlbum);
            suite.Run("calculates adaptive track info widths", CalculatesAdaptiveTrackInfoWidths);
            suite.Run("album art is centered with a rounded clip", AlbumArtIsCenteredWithRoundedClip);
            suite.Run("temporary interaction expands expandable mode", TemporaryInteractionExpandsExpandableMode);
            suite.Run("expandable layout requires the temporary interaction hotkey", ExpandableLayoutRequiresTemporaryInteractionHotkey);
            suite.Run("temporary interaction release uses configured expanded duration", TemporaryInteractionReleaseUsesConfiguredExpandedDuration);
            suite.Run("keeps island expanded while editing", KeepsIslandExpandedWhileEditing);
            suite.Run("settings layout mode labels are product facing", SettingsLayoutModeLabelsAreProductFacing);
            suite.Run("layout cards directly select the edited mode", LayoutCardsDirectlySelectEditedMode);
            suite.Run("about page exposes v2 Beta product information", AboutPageExposesV2BetaProductInformation);
            suite.Run("support developer page exposes Pro and free support actions", SupportDeveloperPageExposesProAndFreeSupportActions);
            suite.Run("formats the public Beta version", FormatsThePublicBetaVersion);
            suite.Run("release version has one source and auto increments", ReleaseVersionHasOneSourceAndAutoIncrements);
            suite.Run("tutorial waits for required user actions", TutorialWaitsForRequiredUserActions);
            suite.Run("tutorial rejects control click without temporary interaction", TutorialRejectsControlClickWithoutTemporaryInteraction);
            suite.Run("first launch tutorial is persisted and can be replayed", FirstLaunchTutorialIsPersistedAndCanBeReplayed);
            suite.Run("tutorial overlay is dimmer and cannot cover interactions", TutorialOverlayIsDimmerAndCannotCoverInteractions);
            suite.Run("layout rebuild replays the latest island content", LayoutRebuildReplaysLatestIslandContent);
            suite.Run("escape exits tutorial from island and settings", EscapeExitsTutorialFromIslandAndSettings);
            suite.Run("lyric transition keeps centered canvas position", LyricTransitionKeepsCenteredCanvasPosition);
            suite.Run("tutorial hover waits before enabling avoidance", TutorialHoverWaitsBeforeEnablingAvoidance);
            suite.Run("tutorial next remains visible and keeps settings open", TutorialNextRemainsVisibleAndKeepsSettingsOpen);
            suite.Run("tutorial next uses an unclipped rounded pulse", TutorialNextUsesUnclippedRoundedPulse);
            suite.Run("tutorial action buttons fade with the mask", TutorialActionButtonsFadeWithMask);
            suite.Run("tutorial highlights only the layout editing setting background", TutorialHighlightsLayoutEditingSettingBackground);
            suite.Run("tutorial module transitions overlap and keep click-expand layout unchanged", TutorialModuleTransitionsOverlapAndKeepClickExpandLayoutUnchanged);
            suite.Run("settings and tutorial explain the expandable hotkey", SettingsAndTutorialExplainExpandableHotkey);
            suite.Run("all measured island width changes animate", AllMeasuredIslandWidthChangesAnimate);
            suite.Run("expanded island measures unconstrained module content", ExpandedIslandMeasuresUnconstrainedModuleContent);
            suite.Run("island size animation avoids per-frame transparent window resizing", IslandSizeAnimationAvoidsPerFrameTransparentWindowResizing);
            suite.Run("module toolbox captures mouse down for drag", ModuleToolboxCapturesMouseDownForDrag);
            suite.Run("settings stays modeless while editing modules", SettingsStaysModelessWhileEditingModules);
            suite.Run("expandable island animates measured size", ExpandableIslandAnimatesMeasuredSize);
            suite.Run("auto retract delays are configurable", AutoRetractDelaysAreConfigurable);
            suite.Run("settings and temporary interaction restart no playback countdown", SettingsAndTemporaryInteractionRestartNoPlaybackCountdown);
            suite.Run("lyrics module exposes configurable width", LyricsModuleExposesConfigurableWidth);
            suite.Run("island background width reserves shaped edge padding", IslandBackgroundWidthReservesShapedEdgePadding);
            suite.Run("playback controls use media glyphs", PlaybackControlsUseMediaGlyphs);
            suite.Run("playback controls are not consumed by layout drag", PlaybackControlsAreNotConsumedByLayoutDrag);
            suite.Run("configured key temporarily suppresses hover transparency", ConfiguredKeyTemporarilySuppressesHoverTransparency);
            suite.Run("snaps module within eighteen pixels", SnapsModuleWithinEighteenPixels);
            suite.Run("moves module after crossing midpoint", MovesModuleAfterCrossingMidpoint);
            suite.Run("moves module one slot right without skipping", MovesModuleOneSlotRightWithoutSkipping);
            suite.Run("allows duplicate modules in one layout", AllowsDuplicateModulesInOneLayout);
            suite.Run("deduplicates one layout drop operation", DeduplicatesOneLayoutDropOperation);
            suite.Run("deletes modules only after an outside mouse release", DeletesModulesOnlyAfterOutsideMouseRelease);
            suite.Run("projects a fixed placeholder while reordering", ProjectsFixedPlaceholderWhileReordering);
            suite.Run("converts projected destination to move boundary", ConvertsProjectedDestinationToMoveBoundary);
            suite.Run("layout drag shows snapped insertion placeholder", LayoutDragShowsSnappedInsertionPlaceholder);
            suite.Run("module drop handler is registered once", ModuleDropHandlerIsRegisteredOnce);
            suite.Run("layout module add and remove animate island size", LayoutModuleAddAndRemoveAnimateIslandSize);
            suite.Run("cancels layout draft without mutating original", CancelsLayoutDraftWithoutMutatingOriginal);
            suite.Run("uses approved lyric offset hotkeys", UsesApprovedLyricOffsetHotkeys);
            suite.Run("translation mode explains why single line is unavailable", TranslationModeExplainsSingleLineRestriction);
            suite.Run("settings exposes automatic and locked player selection", SettingsExposesPlayerSelection);
            suite.Run("estimates missing playback timeline", EstimatesMissingPlaybackTimeline);
            suite.Run("advances when a player repeats one reliable timeline sample", AdvancesRepeatedReliableTimelineSample);
            suite.Run("starts a local timeline for players without timeline metadata", StartsLocalTimelineWithoutMetadata);
            suite.Run("uses maximum seek time when SMTC end time is missing", UsesMaximumSeekTimeWhenSmtcEndTimeIsMissing);
            suite.Run("prefers SMTC end time when both duration fields are available", PrefersSmtcEndTimeWhenBothDurationFieldsAreAvailable);
            suite.Run("compensates stale playing timeline samples", CompensatesStalePlayingTimelineSamples);
            suite.Run("does not compensate paused timeline samples", DoesNotCompensatePausedTimelineSamples);
            suite.Run("freezes estimated timeline while paused", FreezesEstimatedTimelineWhilePaused);
            suite.Run("accepts large real timeline correction", AcceptsLargeRealTimelineCorrection);
            suite.Run("ignores small backward timeline jitter while playing", IgnoresSmallBackwardTimelineJitterWhilePlaying);
            suite.Run("allows only one named application instance", AllowsOnlyOneNamedApplicationInstance);
            suite.Run("signals the existing application instance", SignalsExistingApplicationInstance);
            suite.Run("keeps island visible while playing even without lyrics", KeepsIslandVisibleWhilePlayingEvenWithoutLyrics);
            suite.Run("keeps island visible during startup hint", KeepsIslandVisibleDuringStartupHint);
            suite.Run("keeps paused island available during grace period", KeepsPausedIslandAvailableDuringGracePeriod);
            suite.Run("hides paused island after grace period", HidesPausedIslandAfterGracePeriod);
            suite.Run("calculates top-only overlay positions", CalculatesTopOnlyOverlayPositions);
            suite.Run("calculates hidden top-only overlay positions", CalculatesHiddenTopOnlyOverlayPositions);
            suite.Run("snaps dragged overlay to top edge", SnapsDraggedOverlayToTopEdge);
            suite.Run("keeps pointer drag locked to top edge", KeepsPointerDragLockedToTopEdge);
            suite.Run("selects overlay shape path for dock edge", SelectsOverlayShapePathForDockEdge);
            suite.Run("mouse avoidance settings panel has enough layout rows", MouseAvoidanceSettingsPanelHasEnoughLayoutRows);
            suite.Run("mouse avoidance settings fit without scrolling", MouseAvoidanceSettingsFitWithoutScrolling);
            suite.Run("mouse avoidance settings exposes hover aspect ratio preview", MouseAvoidanceSettingsExposesHoverAspectRatioPreview);
            suite.Run("mouse avoidance settings exposes click through option", MouseAvoidanceSettingsExposesClickThroughOption);
            suite.Run("mouse avoidance settings restores screenshot defaults", MouseAvoidanceSettingsRestoresScreenshotDefaults);
            suite.Run("click through keeps left drag available", ClickThroughKeepsLeftDragAvailable);
            suite.Run("settings window exposes theme mode switcher", SettingsWindowExposesThemeModeSwitcher);
            suite.Run("system theme follows Windows changes live", SystemThemeFollowsWindowsChangesLive);
            suite.Run("cache settings explains capacity and cleanup", CacheSettingsExplainsCapacityAndCleanup);
            suite.Run("settings layout exposes requested streamlined controls", SettingsLayoutExposesRequestedStreamlinedControls);
            suite.Run("divider settings update layout modules", DividerSettingsUpdateLayoutModules);
            suite.Run("hover mask restores full opacity outside aura", HoverMaskRestoresFullOpacityOutsideAura);
            suite.Run("settings first open text uses theme resources", SettingsFirstOpenTextUsesThemeResources);
            suite.Run("line mode segment uses theme-aware colors", LineModeSegmentUsesThemeAwareColors);
            suite.Run("segmented settings animate their selection thumbs", SegmentedSettingsAnimateTheirSelectionThumbs);
            suite.Run("island reveal and retract use nonlinear frame animation", IslandRevealAndRetractUseNonlinearFrameAnimation);
            suite.Run("does not use player specific ocr fallback when lyrics sources miss", DoesNotUsePlayerSpecificOcrFallbackWhenLyricsSourcesMiss);
            suite.Run("shows tray icon on startup", ShowsTrayIconOnStartup);
            suite.Run("main window keeps startup hint without media session", MainWindowKeepsStartupHintWithoutMediaSession);
            suite.Run("startup hint begins auto retract countdown immediately", StartupHintBeginsAutoRetractCountdownImmediately);
            suite.Run("native SMTC service keeps persistent session subscriptions", NativeSmtcServiceKeepsPersistentSessionSubscriptions);
            suite.Run("native playback rejects stale lyrics and removes PowerShell bridge", NativePlaybackRejectsStaleLyricsAndRemovesPowerShellBridge);
            suite.Run("coalesces repeated animation targets while preserving the latest target", CoalescesRepeatedAnimationTargets);
            suite.Run("main window stops all runtime activity when closed", MainWindowStopsAllRuntimeActivityWhenClosed);
            suite.Run("atomically replaces settings files without leaving temporary files", AtomicallyReplacesSettingsFilesWithoutLeavingTemporaryFiles);
            suite.Run("tracks reference changes without treating equal content as the same object", TracksReferenceChangesWithoutTreatingEqualContentAsTheSameObject);
            suite.Run("module views skip unchanged rendering work", ModuleViewsSkipUnchangedRenderingWork);
            suite.Run("coalesces identical hover samples without losing changed samples", CoalescesIdenticalHoverSamplesWithoutLosingChangedSamples);
            suite.Run("settings dirty fingerprint avoids a second JSON deep clone", SettingsDirtyFingerprintAvoidsASecondJsonDeepClone);
            suite.Run("user visible product branding uses lyric island", UserVisibleProductBrandingUsesLyricsIsland);
            return suite.ExitCode;
        }

        static void ParsesSyncedLrcLinesAndMetadata()
        {
            var lrc = "[ar:Jay Chou]\n[ti:Simple Love]\n[00:01.50]I want to hold your hand\n[00:04.20]And never let go";

            var lyrics = LrcParser.Parse(lrc);

            Assert.Equal("Jay Chou", lyrics.Artist);
            Assert.Equal("Simple Love", lyrics.Title);
            Assert.Equal(2, lyrics.Lines.Count);
            Assert.Equal(TimeSpan.FromMilliseconds(1500), lyrics.Lines[0].Timestamp);
            Assert.Equal("I want to hold your hand", lyrics.Lines[0].Text);
            Assert.Equal(TimeSpan.FromMilliseconds(4200), lyrics.Lines[1].Timestamp);
        }

        static void SelectsCurrentLyricLineByPlaybackPosition()
        {
            var lyrics = new TimedLyrics(new[]
            {
                new LyricLine(TimeSpan.FromSeconds(1), "first"),
                new LyricLine(TimeSpan.FromSeconds(4), "second"),
                new LyricLine(TimeSpan.FromSeconds(8), "third")
            });

            var line = lyrics.GetCurrentLine(TimeSpan.FromSeconds(5));

            Assert.Equal("second", line.Text);
        }

        static void SelectsCurrentLyricLineWithTimingOffset()
        {
            var lyrics = new TimedLyrics(new[]
            {
                new LyricLine(TimeSpan.FromSeconds(1), "first"),
                new LyricLine(TimeSpan.FromSeconds(6), "second")
            });

            var line = lyrics.GetCurrentLine(TimeSpan.FromMilliseconds(5400), TimeSpan.FromMilliseconds(800));

            Assert.Equal("second", line.Text);
        }

        static void SelectsCurrentAndNextLyricLines()
        {
            var lyrics = new TimedLyrics(new[]
            {
                new LyricLine(TimeSpan.FromSeconds(1), "first"),
                new LyricLine(TimeSpan.FromSeconds(4), "second"),
                new LyricLine(TimeSpan.FromSeconds(8), "third")
            });

            var lines = lyrics.GetCurrentLines(TimeSpan.FromSeconds(5), TimeSpan.Zero, 2);

            Assert.Equal(2, lines.Count);
            Assert.Equal("second", lines[0].Text);
            Assert.Equal("third", lines[1].Text);
        }

        static void SelectsCurrentLyricLineWithTranslation()
        {
            var lyrics = LyricsPackageParser.Parse(
                "[00:01.00]hello\n[00:04.00]world\n" +
                LyricsPackageParser.TranslationSeparator + "\n" +
                "[00:01.00]你好\n[00:04.00]世界");

            var lines = lyrics.GetCurrentDisplayLines(TimeSpan.FromSeconds(2), TimeSpan.Zero, 2);

            Assert.Equal(2, lines.Count);
            Assert.Equal("hello", lines[0].Text);
            Assert.Equal("你好", lines[1].Text);
        }

        static void SelectsOneDisplayLineWhenMultilineIsDisabled()
        {
            var lyrics = new TimedLyrics(new[]
            {
                new LyricLine(TimeSpan.FromSeconds(1), "first"),
                new LyricLine(TimeSpan.FromSeconds(4), "second")
            });

            var lines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(2), TimeSpan.Zero, false, false);

            Assert.Equal(1, lines.Count);
            Assert.Equal("first", lines[0].Text);
        }

        static void SelectsTranslatedDisplayLinesWhenTranslationIsEnabled()
        {
            var lyrics = LyricsPackageParser.Parse(
                "[00:01.00]hello\n[00:04.00]world\n" +
                LyricsPackageParser.TranslationSeparator + "\n" +
                "[00:01.00]你好\n[00:04.00]世界");

            var lines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(2), TimeSpan.Zero, false, true);

            Assert.Equal(2, lines.Count);
            Assert.Equal("hello", lines[0].Text);
            Assert.Equal("你好", lines[1].Text);
        }

        static void IgnoresTranslationSettingForChineseLyrics()
        {
            var lyrics = LyricsPackageParser.Parse(
                "[00:01.00]夜空中最亮的星\n[00:04.00]能否听清\n[00:08.00]那仰望的人\n" +
                LyricsPackageParser.TranslationSeparator + "\n" +
                "[00:01.00]The brightest star in the night sky\n[00:04.00]Can you hear me\n[00:08.00]The one looking up");

            var lines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(2), TimeSpan.Zero, false, true);

            Assert.Equal(2, lines.Count);
            Assert.Equal("夜空中最亮的星", lines[0].Text);
            Assert.Equal("能否听清", lines[1].Text);
        }

        static void KeepsTranslationSettingForJapaneseLyrics()
        {
            var lyrics = LyricsPackageParser.Parse(
                "[00:01.00]君の声が聞こえる\n[00:04.00]夜空を見上げて\n" +
                LyricsPackageParser.TranslationSeparator + "\n" +
                "[00:01.00]我能听见你的声音\n[00:04.00]仰望夜空");

            var lines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(2), TimeSpan.Zero, true, true);

            Assert.Equal(2, lines.Count);
            Assert.Equal("君の声が聞こえる", lines[0].Text);
            Assert.Equal("我能听见你的声音", lines[1].Text);
        }

        static void UsesOneLineForTranslationPlaceholder()
        {
            var lyrics = LyricsPackageParser.Parse(
                "[00:01.00]Ohhhhh\n" +
                LyricsPackageParser.TranslationSeparator + "\n" +
                "[00:01.00]//");

            var lines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(2), TimeSpan.Zero, true, true);

            Assert.Equal(1, lines.Count);
            Assert.Equal("Ohhhhh", lines[0].Text);
        }

        static void DoesNotReuseStaleTranslationForNextLyric()
        {
            var lyrics = LyricsPackageParser.Parse(
                "[00:01.00]first\n[00:04.00]second\n" +
                LyricsPackageParser.TranslationSeparator + "\n" +
                "[00:01.00]第一句");

            var lines = LyricsDisplaySelector.Select(lyrics, TimeSpan.FromSeconds(5), TimeSpan.Zero, true, true);

            Assert.Equal(1, lines.Count);
            Assert.Equal("second", lines[0].Text);
        }

        static void ParsesLyricsPackageWithoutTranslation()
        {
            var lyrics = LyricsPackageParser.Parse("[00:01.00]hello\n[00:04.00]world");

            var lines = lyrics.GetCurrentDisplayLines(TimeSpan.FromSeconds(2), TimeSpan.Zero, 2);

            Assert.Equal(2, lines.Count);
            Assert.Equal("hello", lines[0].Text);
            Assert.Equal("world", lines[1].Text);
        }

        static void DetectsWhetherLyricsPackageHasTranslation()
        {
            Assert.False(LyricsPackageParser.HasTranslation("[00:01.00]hello"));
            Assert.True(LyricsPackageParser.HasTranslation("[00:01.00]hello\n" + LyricsPackageParser.TranslationSeparator + "\n[00:01.00]你好"));
        }

        static void GetsCurrentLyricLineDuration()
        {
            var lyrics = new TimedLyrics(new[]
            {
                new LyricLine(TimeSpan.FromSeconds(1), "first"),
                new LyricLine(TimeSpan.FromSeconds(4), "second"),
                new LyricLine(TimeSpan.FromSeconds(9), "third")
            });

            var duration = lyrics.GetCurrentLineDuration(TimeSpan.FromSeconds(5), TimeSpan.Zero, TimeSpan.FromSeconds(4));

            Assert.Equal(TimeSpan.FromSeconds(5), duration);
        }

        static void TracksLyricTextChangesForAnimation()
        {
            var tracker = new LyricTextTransitionTracker();

            Assert.False(tracker.Update("first", "second"));
            Assert.False(tracker.Update("first", "second"));
            Assert.True(tracker.Update("second", "third"));
            Assert.True(tracker.Update("second", ""));
            Assert.False(tracker.Update("second", ""));
        }

        static void PositionsLyricTextBeforeTransition()
        {
            var centered = LyricTextPlacement.Calculate(100, 40);
            Assert.Equal(30.0, centered.Left);
            Assert.Equal(0.0, centered.Overflow);
            Assert.False(centered.RequiresMarquee);

            var overflowing = LyricTextPlacement.Calculate(100, 140);
            Assert.Equal(0.0, overflowing.Left);
            Assert.Equal(68.0, overflowing.Overflow);
            Assert.True(overflowing.RequiresMarquee);

            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "LyricsModuleView.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "LyricsModuleView.xaml.cs"));
            Assert.True(xaml.Contains("x:Name=\"IncomingPrimaryLyricClip\""));
            Assert.True(xaml.Contains("x:Name=\"IncomingSecondaryLyricClip\""));
            Assert.True(source.Contains("PrepareTextBlock"));
            Assert.True(source.Contains("transitionVersion != lyricsTransitionVersion"));
            Assert.False(source.Contains("DispatcherPriority.Background"));
        }

        static void ReturnsEmptyLineBeforeFirstLyric()
        {
            var lyrics = new TimedLyrics(new[]
            {
                new LyricLine(TimeSpan.FromSeconds(3), "first")
            });

            var line = lyrics.GetCurrentLine(TimeSpan.FromSeconds(1));

            Assert.Equal(string.Empty, line.Text);
            Assert.Equal(TimeSpan.Zero, line.Timestamp);
        }

        static void BuildsStableCachePathsFromSongIdentity()
        {
            var cache = new LyricsCache(Path.Combine("cache-root", "lyrics"));
            var path = cache.GetPath(new TrackIdentity("A/B:C*D?", "Singer Name", TimeSpan.FromSeconds(242)));

            Assert.True(path.EndsWith(Path.Combine("cache-root", "lyrics", "singer-name-a-b-c-d-242.lrc")));
        }

        static void MigratesLegacyProductDataDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), "LyricsIsland.ProductData." + Guid.NewGuid().ToString("N"));
            var legacyRoot = Path.Combine(root, string.Concat("AppleMusic", "DesktopLyrics"));
            try
            {
                Directory.CreateDirectory(Path.Combine(legacyRoot, "lyrics"));
                File.WriteAllText(Path.Combine(legacyRoot, "settings.json"), "{\"HasSeenTutorial\":true}");
                File.WriteAllText(Path.Combine(legacyRoot, "lyrics", "song.lrc"), "[00:01.00]line");

                var currentRoot = ProductDataDirectory.Prepare(root);

                Assert.Equal(Path.Combine(root, "LyricsIsland"), currentRoot);
                Assert.True(File.Exists(Path.Combine(currentRoot, "settings.json")));
                Assert.True(File.Exists(Path.Combine(currentRoot, "lyrics", "song.lrc")));
                Assert.False(Directory.Exists(legacyRoot));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        static void EvictsLeastRecentlyUsedSongCacheFilesToStayUnderSizeLimit()
        {
            var root = Path.Combine(Path.GetTempPath(), "LyricsIsland.Tests." + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LyricsCache(root, 280);
                var first = new TrackIdentity("First", "Singer", TimeSpan.FromSeconds(180));
                var second = new TrackIdentity("Second", "Singer", TimeSpan.FromSeconds(180));
                var third = new TrackIdentity("Third", "Singer", TimeSpan.FromSeconds(180));

                cache.Write(first, new string('a', 100));
                cache.Write(second, new string('b', 100));
                File.SetLastWriteTimeUtc(cache.GetPath(first), DateTime.UtcNow.AddMinutes(-3));
                File.SetLastWriteTimeUtc(cache.GetPath(second), DateTime.UtcNow.AddMinutes(-2));

                string cached;
                Assert.True(cache.TryRead(first, out cached));
                cache.Write(third, new string('c', 100));

                Assert.True(File.Exists(cache.GetPath(first)));
                Assert.False(File.Exists(cache.GetPath(second)));
                Assert.True(File.Exists(cache.GetPath(third)));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        static void KeepsPreviousLyricAcrossEmptyTimestampMarkers()
        {
            var lyrics = LrcParser.Parse(
                "[00:20.00]first\n" +
                "[00:24.00]\n" +
                "[00:27.00]second");

            Assert.Equal("first", lyrics.GetCurrentLine(TimeSpan.FromSeconds(25)).Text);
            Assert.Equal(TimeSpan.FromSeconds(7), lyrics.GetCurrentLineDuration(
                TimeSpan.FromSeconds(25),
                TimeSpan.Zero,
                TimeSpan.FromSeconds(4)));
        }

        static void ReusesCachedLyricsWhenReportedDurationDrifts()
        {
            var root = Path.Combine(Path.GetTempPath(), "LyricsIsland.Tests." + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new LyricsCache(root);
                cache.Write(
                    new TrackIdentity("Same Song", "Same Artist", TimeSpan.FromSeconds(242)),
                    "[00:01.00]cached");

                string cached;
                Assert.True(cache.TryRead(
                    new TrackIdentity("Same Song", "Same Artist", TimeSpan.FromSeconds(241)),
                    out cached));
                Assert.Equal("[00:01.00]cached", cached);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        static void UsesLrcLibSearchAsPrimaryLyricsLookup()
        {
            Uri requested = null;
            var client = new LrcLibClient(uri =>
            {
                requested = uri;
                return "[{\"trackName\":\"Song Name\",\"artistName\":\"Singer Name\",\"syncedLyrics\":\"[00:01.00]hello\\n[00:02.00]world\"}]";
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Song Name", "Singer Name", TimeSpan.FromSeconds(210)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal("[00:01.00]hello\n[00:02.00]world", lrc);
            Assert.Equal("https://lrclib.net/api/search?track_name=Song%20Name&artist_name=Singer%20Name", requested.AbsoluteUri);
        }

        static void FallsBackFromAlbumScopedLrcLibSearch()
        {
            var requests = new List<Uri>();
            var client = new LrcLibClient(uri =>
            {
                requests.Add(uri);
                return requests.Count == 1
                    ? "[]"
                    : "[{\"trackName\":\"Song Name\",\"artistName\":\"Singer Name\",\"syncedLyrics\":\"[00:01.00]fallback\"}]";
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Song Name", "Singer Name", TimeSpan.FromSeconds(210), "Album Name"))
                .GetAwaiter()
                .GetResult();

            Assert.Equal("[00:01.00]fallback", lrc);
            Assert.Equal("https://lrclib.net/api/search?track_name=Song%20Name&artist_name=Singer%20Name&album_name=Album%20Name", requests[0].AbsoluteUri);
            Assert.Equal("https://lrclib.net/api/search?track_name=Song%20Name&artist_name=Singer%20Name", requests[1].AbsoluteUri);
        }

        static void ReturnsEmptyLyricsWhenLrcLibReports404()
        {
            var client = new LrcLibClient((Func<Uri, string>)(uri =>
            {
                throw new HttpRequestException("Response status code does not indicate success: 404 (Not Found).");
            }));

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Missing Song", "Missing Artist", TimeSpan.FromSeconds(180)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal(string.Empty, lrc);
        }

        static void ReturnsEmptyLyricsWhenLrcLibRequestTimesOut()
        {
            var client = new LrcLibClient((Func<Uri, string>)(uri =>
            {
                throw new TaskCanceledException("The request timed out.");
            }));

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Song Name", "Singer Name", TimeSpan.FromSeconds(210)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal(string.Empty, lrc);
        }

        static void FetchesSyncedLyricsFromNetEaseResponse()
        {
            var requests = new List<Uri>();
            var client = new NetEaseLyricsClient(uri =>
            {
                requests.Add(uri);
                if (uri.AbsolutePath.EndsWith("/api/search/get/web", StringComparison.OrdinalIgnoreCase))
                {
                    return "{\"result\":{\"songs\":[{\"id\":123,\"name\":\"Bad Romance\",\"artists\":[{\"name\":\"Lady Gaga\"}]}]}}";
                }

                return "{\"lrc\":{\"lyric\":\"[00:01.00]Rah rah ah-ah-ah\\n[00:02.00]Roma roma-ma\"}}";
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Bad Romance", "Lady Gaga", TimeSpan.FromSeconds(295)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal("[00:01.00]Rah rah ah-ah-ah\n[00:02.00]Roma roma-ma", lrc);
            Assert.True(requests[0].AbsoluteUri.Contains("s=Bad%20Romance%20Lady%20Gaga"));
            Assert.Equal("https://music.163.com/api/song/lyric?id=123&lv=1&kv=1&tv=-1", requests[1].AbsoluteUri);
        }

        static void FetchesTranslatedLyricsFromNetEaseResponse()
        {
            var client = new NetEaseLyricsClient(uri =>
            {
                if (uri.AbsolutePath.EndsWith("/api/search/get/web", StringComparison.OrdinalIgnoreCase))
                {
                    return "{\"result\":{\"songs\":[{\"id\":123,\"name\":\"Bad Romance\",\"artists\":[{\"name\":\"Lady Gaga\"}],\"duration\":295000}]}}";
                }

                return "{\"lrc\":{\"lyric\":\"[00:01.00]Rah rah\\n[00:02.00]Roma roma-ma\"},\"tlyric\":{\"lyric\":\"[00:01.00]拉拉\\n[00:02.00]罗马罗马\"}}";
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Bad Romance", "Lady Gaga", TimeSpan.FromSeconds(295)))
                .GetAwaiter()
                .GetResult();

            Assert.True(lrc.Contains(LyricsPackageParser.TranslationSeparator));
            Assert.True(lrc.Contains("[00:01.00]拉拉"));
        }

        static void UsesFallbackLyricsSourceWhenPrimarySourceIsEmpty()
        {
            var client = new CompositeLyricsClient(new ILyricsClient[]
            {
                new FakeLyricsClient(string.Empty),
                new FakeLyricsClient("[00:01.00]fallback")
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Song", "Artist", TimeSpan.FromSeconds(180)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal("[00:01.00]fallback", lrc);
        }

        static void PrefersTranslatedFallbackLyricsSource()
        {
            var translated = "[00:01.00]hello" +
                Environment.NewLine +
                LyricsPackageParser.TranslationSeparator +
                Environment.NewLine +
                "[00:01.00]你好";
            var client = new CompositeLyricsClient(new ILyricsClient[]
            {
                new FakeLyricsClient("[00:01.00]hello"),
                new FakeLyricsClient(translated)
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Song", "Artist", TimeSpan.FromSeconds(180)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal(translated, lrc);
        }

        static void FetchesSyncedLyricsFromKuGouResponse()
        {
            var requests = new List<Uri>();
            var lyrics = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[00:01.00]hot song\n[00:02.00]new line"));
            var client = new KuGouLyricsClient(uri =>
            {
                requests.Add(uri);
                if (uri.AbsolutePath.EndsWith("/search", StringComparison.OrdinalIgnoreCase))
                {
                    return "{\"status\":200,\"candidates\":[{\"id\":\"abc\",\"accesskey\":\"key\",\"duration\":188000}]}";
                }

                return "{\"status\":200,\"content\":\"" + lyrics + "\"}";
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Hot Song", "Singer", TimeSpan.FromSeconds(188)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal("[00:01.00]hot song\n[00:02.00]new line", lrc);
            Assert.True(requests[0].AbsoluteUri.Contains("keyword=Hot%20Song%20Singer"));
            Assert.True(requests[0].AbsoluteUri.Contains("duration=188000"));
            Assert.True(requests[1].AbsoluteUri.Contains("id=abc"));
            Assert.True(requests[1].AbsoluteUri.Contains("accesskey=key"));
        }

        static void ScoresLyricCandidatesByTitleArtistAndDuration()
        {
            var track = new TrackIdentity("I Knew It, I Knew You", "Taylor Swift", TimeSpan.FromSeconds(178), "I Knew It, I Knew You");

            Assert.False(LyricsCandidateMatcher.IsReasonable(track, "I Knew It, I Knew You (Live)", "Taylor Swift", "", TimeSpan.FromSeconds(240)));
            Assert.True(LyricsCandidateMatcher.IsReasonable(track, "I Knew It, I Knew You", "Taylor Swift", "I Knew It, I Knew You", TimeSpan.FromSeconds(178)));
            Assert.True(LyricsCandidateMatcher.Score(track, "I Knew It, I Knew You", "Taylor Swift", "I Knew It, I Knew You", TimeSpan.FromSeconds(178)) >
                LyricsCandidateMatcher.Score(track, "I Knew It, I Knew You", "Taylor Swift", "Other Album", TimeSpan.FromSeconds(190)));
        }

        static void FetchesSyncedLyricsFromQqMusicResponse()
        {
            var requests = new List<Uri>();
            var lyrics = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[ti:Bad Romance]\n[ar:Lady Gaga]\n[00:00.00]Bad Romance\n[00:01.00]Rah rah"));
            var client = new QQMusicLyricsClient(uri =>
            {
                requests.Add(uri);
                if (uri.AbsolutePath.EndsWith("/client_search_cp", StringComparison.OrdinalIgnoreCase))
                {
                    return "{\"data\":{\"song\":{\"list\":[{\"id\":103168363,\"mid\":\"002L922J1xDquy\",\"title\":\"Bad Romance\",\"singer\":[{\"name\":\"Lady Gaga\"}],\"interval\":295}]}}}";
                }

                return "{\"code\":0,\"req_0\":{\"code\":0,\"data\":{\"lyric\":\"" + lyrics + "\",\"trans\":\"\"}}}";
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Bad Romance", "Lady Gaga", TimeSpan.FromSeconds(295)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal("[ti:Bad Romance]\n[ar:Lady Gaga]\n[00:00.00]Bad Romance\n[00:01.00]Rah rah", lrc);
            Assert.True(requests[0].AbsoluteUri.Contains("w=Bad%20Romance%20Lady%20Gaga"));
            Assert.True(requests[1].AbsoluteUri.Contains("musicu.fcg"));
            Assert.True(Uri.UnescapeDataString(requests[1].Query).Contains("\"songId\":103168363"));
            Assert.True(Uri.UnescapeDataString(requests[1].Query).Contains("\"trans\":1"));
        }

        static void FetchesTranslatedLyricsFromQqMusicResponse()
        {
            var lyrics = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[00:00.00]Bad Romance\n[00:01.00]Rah rah"));
            var translation = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[00:00.00]糟糕的浪漫\n[00:01.00]拉拉"));
            var client = new QQMusicLyricsClient(uri =>
            {
                if (uri.AbsolutePath.EndsWith("/client_search_cp", StringComparison.OrdinalIgnoreCase))
                {
                    return "{\"data\":{\"song\":{\"list\":[{\"id\":103168363,\"mid\":\"002L922J1xDquy\",\"title\":\"Bad Romance\",\"singer\":[{\"name\":\"Lady Gaga\"}],\"interval\":295}]}}}";
                }

                return "{\"code\":0,\"req_0\":{\"code\":0,\"data\":{\"lyric\":\"" + lyrics + "\",\"trans\":\"" + translation + "\"}}}";
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Bad Romance", "Lady Gaga", TimeSpan.FromSeconds(295)))
                .GetAwaiter()
                .GetResult();

            Assert.True(lrc.Contains(LyricsPackageParser.TranslationSeparator));
            Assert.True(lrc.Contains("[00:00.00]糟糕的浪漫"));
        }

        static void UsesFallbackLyricsSourceWhenPrimarySourceThrows()
        {
            var client = new CompositeLyricsClient(new ILyricsClient[]
            {
                new ThrowingLyricsClient(),
                new FakeLyricsClient("[00:01.00]fallback")
            });

            var lrc = client.GetSyncedLyricsAsync(new TrackIdentity("Song", "Artist", TimeSpan.FromSeconds(180)))
                .GetAwaiter()
                .GetResult();

            Assert.Equal("[00:01.00]fallback", lrc);
        }

        static void CleansCombinedNowPlayingTitles()
        {
            var track = TrackIdentityCleaner.Clean(new TrackIdentity(
                "I Knew It, I Knew You - Taylor Swift — I Knew It, I Knew You - Single",
                "",
                TimeSpan.FromSeconds(188)));

            Assert.Equal("I Knew It, I Knew You", track.Title);
            Assert.Equal("Taylor Swift", track.Artist);
        }

        static void RemovesFeaturedArtistCreditFromNowPlayingTitles()
        {
            var track = TrackIdentityCleaner.Clean(new TrackIdentity(
                "Dark Horse (feat. Juicy J)",
                "Katy Perry",
                TimeSpan.FromSeconds(215),
                "PRISM"));

            Assert.Equal("Dark Horse", track.Title);
            Assert.Equal("Katy Perry", track.Artist);
        }

        static void RemovesAlbumSuffixFromNowPlayingArtist()
        {
            var exactAlbum = TrackIdentityCleaner.Clean(new TrackIdentity(
                "This Is How We Do",
                "Katy Perry — PRISM (Deluxe)",
                TimeSpan.FromSeconds(204),
                "PRISM (Deluxe)"));

            Assert.Equal("This Is How We Do", exactAlbum.Title);
            Assert.Equal("Katy Perry", exactAlbum.Artist);

            var fallbackSeparator = TrackIdentityCleaner.Clean(new TrackIdentity(
                "This Is How We Do",
                "Katy Perry — PRISM (Deluxe...)",
                TimeSpan.FromSeconds(204)));

            Assert.Equal("Katy Perry", fallbackSeparator.Artist);
        }

        static void MatchesLyricCandidatesWhenNowPlayingTitleIncludesFeaturedArtist()
        {
            var track = new TrackIdentity(
                "Dark Horse (feat. Juicy J)",
                "Katy Perry",
                TimeSpan.FromSeconds(215),
                "PRISM");

            Assert.True(LyricsCandidateMatcher.IsReasonable(
                track,
                "Dark Horse",
                "Katy Perry",
                "PRISM",
                TimeSpan.FromSeconds(215)));
        }

        static void PrefersLockedMediaSession()
        {
            var now = DateTimeOffset.Parse("2026-07-03T10:00:00+08:00");
            var spotify = MediaSessionSnapshot.CreateForTest("spotify", MediaPlaybackStatus.Playing, now);
            var qq = MediaSessionSnapshot.CreateForTest("qqmusic", MediaPlaybackStatus.Paused, now.AddSeconds(-3));

            Assert.Equal("qqmusic", SessionSelectionPolicy.Select(new[] { spotify, qq }, "qqmusic", null).SessionId);
        }

        static void PrefersMostRecentlyActivePlayingSession()
        {
            var now = DateTimeOffset.Parse("2026-07-03T10:00:00+08:00");
            var older = MediaSessionSnapshot.CreateForTest("spotify", MediaPlaybackStatus.Playing, now.AddSeconds(-5));
            var newer = MediaSessionSnapshot.CreateForTest("kugou", MediaPlaybackStatus.Playing, now);

            Assert.Equal("kugou", SessionSelectionPolicy.Select(new[] { older, newer }, "", "").SessionId);
        }

        static void PrefersWindowsCurrentMediaSession()
        {
            var now = DateTimeOffset.Parse("2026-07-03T10:00:00+08:00");
            var windowsCurrent = MediaSessionSnapshot.CreateForTest("spotify", MediaPlaybackStatus.Playing, now.AddSeconds(-5));
            var newer = MediaSessionSnapshot.CreateForTest("kugou", MediaPlaybackStatus.Playing, now);

            Assert.Equal("spotify", SessionSelectionPolicy.Select(
                new[] { windowsCurrent, newer },
                "",
                windowsCurrent.SessionId).SessionId);
        }

        static void FallsBackWhenLockedSessionDisappears()
        {
            var now = DateTimeOffset.Parse("2026-07-03T10:00:00+08:00");
            var available = MediaSessionSnapshot.CreateForTest("spotify", MediaPlaybackStatus.Playing, now);

            Assert.Equal("spotify", SessionSelectionPolicy.Select(new[] { available }, "missing", null).SessionId);
        }

        static void IgnoresNonMusicMediaSessions()
        {
            var now = DateTimeOffset.Parse("2026-07-23T10:00:00+08:00");
            var browserVideo = MediaSessionSnapshot.CreateForTest("MSEdge", MediaPlaybackStatus.Playing, now);
            var music = MediaSessionSnapshot.CreateForTest("QQMusic.exe", MediaPlaybackStatus.Playing, now.AddSeconds(-5));

            Assert.Equal("QQMusic.exe", SessionSelectionPolicy.Select(
                new[] { browserVideo, music },
                "",
                browserVideo.SessionId).SessionId);
            Assert.Equal(null, SessionSelectionPolicy.Select(
                new[] { browserVideo },
                "",
                browserVideo.SessionId));
        }

        static void ClassifiesTargetSmtcPlayers()
        {
            Assert.Equal(PlayerKind.QQMusic, PlayerProfileCatalog.Resolve("Tencent.QQMusic.exe").Kind);
            Assert.Equal(PlayerKind.NetEaseCloudMusicUwp, PlayerProfileCatalog.Resolve("NetEase.CloudMusicUWP_abc!App").Kind);
            Assert.Equal(PlayerKind.KuGou, PlayerProfileCatalog.Resolve("KuGou.exe").Kind);
            Assert.Equal(PlayerKind.Spotify, PlayerProfileCatalog.Resolve("Spotify.exe").Kind);
            Assert.Equal(PlayerKind.Kuwo, PlayerProfileCatalog.Resolve("KwMusic.exe").Kind);
            Assert.Equal(PlayerKind.AppleMusic, PlayerProfileCatalog.Resolve("AppleMusic.exe").Kind);
            Assert.Equal(PlayerKind.NetEaseCloudMusicUwp, PlayerProfileCatalog.Resolve("cloudmusic.exe").Kind);
        }

        static void LocksKnownPlayerAcrossSessionIds()
        {
            var now = DateTimeOffset.Parse("2026-07-03T10:00:00+08:00");
            var apple = MediaSessionSnapshot.CreateForTest("AppleMusic.exe", MediaPlaybackStatus.Playing, now);
            var netEase = MediaSessionSnapshot.CreateForTest("cloudmusic.exe", MediaPlaybackStatus.Paused, now.AddSeconds(-5));
            var selectionKey = PlayerProfileCatalog.GetSelectionKey(PlayerKind.NetEaseCloudMusicUwp);

            var selected = SessionSelectionPolicy.Select(new[] { apple, netEase }, selectionKey, apple.SessionId);

            Assert.Equal("cloudmusic.exe", selected.SessionId);
        }

        static void UsesGenericProfileForUnknownPlayers()
        {
            Assert.Equal(PlayerKind.Generic, PlayerProfileCatalog.Resolve("Example.Player").Kind);
            Assert.False(PlayerProfileCatalog.IsSupportedMusicPlayer("MSEdge"));
            Assert.True(PlayerProfileCatalog.IsSupportedMusicPlayer("QQMusic.exe"));
        }

        static void CreatesIndependentAAndCLayouts()
        {
            var settings = IslandLayoutDefaults.Create();
            settings.Horizontal.Modules.RemoveAt(0);

            Assert.Equal(IslandModuleType.Lyrics, settings.CompactCollapsed.Modules[0].Type);
            Assert.True(settings.Horizontal.Modules.Count != settings.CompactExpanded.Modules.Count);
        }

        static void KeepsRepeatedDividerModules()
        {
            var profile = new IslandLayoutProfile();
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.Divider));
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.Divider));
            profile.Normalize();

            Assert.Equal(2, profile.Modules.Count);
        }

        static void SettingsSchemaContainsIndependentLayouts()
        {
            var source = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "OverlayPlacementSettings.cs"));

            Assert.True(source.Contains("SchemaVersion"));
            Assert.True(source.Contains("IslandLayoutSettings"));
            Assert.True(source.Contains("LockedSourceAppUserModelId"));
            Assert.True(source.Contains("LyricOffsetHotkeys"));
        }

        static void TracksNormalizedSettingsDirtyState()
        {
            var tracker = new SettingsDirtyStateTracker<string>(
                " Automatic ",
                value => (value ?? string.Empty).Trim().ToLowerInvariant());

            Assert.False(tracker.IsDirty("automatic"));
            Assert.True(tracker.IsDirty("spotify"));
            tracker.Accept(" SPOTIFY ");
            Assert.False(tracker.IsDirty("spotify"));
            Assert.True(tracker.IsDirty("qqmusic"));
        }

        static void LayoutDraftSnapshotsAreIsolated()
        {
            var profile = new IslandLayoutProfile
            {
                Modules = new List<IslandModuleInstance>
                {
                    new IslandModuleInstance(IslandModuleType.Lyrics),
                    new IslandModuleInstance(IslandModuleType.Progress)
                }
            };
            var session = new LayoutEditSession(profile);
            var snapshot = session.GetDraftSnapshot();

            snapshot.Modules.Clear();

            Assert.Equal(2, session.Draft.Modules.Count);
        }

        static void SettingsStoreBacksUpCorruptJson()
        {
            var source = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "OverlayPlacementSettings.cs"));

            Assert.True(source.Contains(".corrupt-"));
            Assert.True(source.Contains("File.Copy"));
        }

        static void BuildsIslandGeometryForMeasuredModuleSize()
        {
            var path = IslandGeometryBuilder.BuildTopPath(720, 84);

            Assert.True(path.Contains("L 720,0"));
            Assert.True(path.Contains("L 69,79"));
        }

        static void PlaybackIntentFollowsLatestClick()
        {
            var coordinator = new PlaybackIntentCoordinator();

            Assert.Equal(MediaPlaybackStatus.Playing,
                coordinator.Toggle("cloudmusic", MediaPlaybackStatus.Paused));
            Assert.Equal(MediaPlaybackStatus.Paused,
                coordinator.Toggle("cloudmusic", MediaPlaybackStatus.Paused));
            Assert.Equal((MediaPlaybackStatus?)MediaPlaybackStatus.Paused,
                coordinator.GetDesiredStatus("cloudmusic"));
            Assert.True(coordinator.Confirm("cloudmusic", MediaPlaybackStatus.Paused));
            Assert.Equal((MediaPlaybackStatus?)null, coordinator.GetDesiredStatus("cloudmusic"));
        }

        static void PlaybackIntentCancelsWhenSessionChanges()
        {
            var coordinator = new PlaybackIntentCoordinator();
            coordinator.Toggle("spotify", MediaPlaybackStatus.Playing);

            Assert.True(coordinator.CancelUnless("qqmusic"));
            Assert.Equal((MediaPlaybackStatus?)null, coordinator.GetDesiredStatus("spotify"));
            Assert.Equal((MediaPlaybackStatus?)null, coordinator.GetDesiredStatus("qqmusic"));
        }

        static void ModuleHostExposesAllV2ModuleViews()
        {
            var root = GetSolutionRoot();
            var source = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));

            Assert.True(source.Contains("LyricsModuleView"));
            Assert.True(source.Contains("AlbumArtModuleView"));
            Assert.True(source.Contains("PlaybackControlsModuleView"));
            Assert.True(source.Contains("TrackInfoModuleView"));
            Assert.True(source.Contains("ProgressModuleView"));
            Assert.True(source.Contains("DividerModuleView"));
        }

        static void TrackInfoShowsTitleAndArtistWithoutAlbum()
        {
            var root = GetSolutionRoot();
            var source = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "Modules", "TrackInfoModuleView.xaml.cs"));
            var xaml = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "Modules", "TrackInfoModuleView.xaml"));
            var smtcSource = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "Media", "SmTcMediaSessionService.cs"));
            var host = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));
            var main = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(source.Contains("TrackIdentityCleaner.Clean"));
            Assert.True(source.Contains("session?.Title"));
            Assert.True(source.Contains("session?.Artist"));
            Assert.True(source.Contains("session?.Album"));
            Assert.False(source.Contains("state.Session.Album"));
            Assert.False(source.Contains("ArtistText.Text = state"));
            Assert.True(xaml.Contains("x:Name=\"TitleText\""));
            Assert.True(xaml.Contains("x:Name=\"ArtistText\""));
            Assert.False(xaml.Contains("AlbumText"));
            Assert.False(xaml.Contains("专辑"));
            Assert.True(smtcSource.Contains("Album = properties.AlbumTitle"));
            Assert.True(source.Contains("TrackInfoWidthCalculator.Calculate"));
            Assert.True(source.Contains("PreferredWidthChanged"));
            Assert.True(source.Contains("RepeatBehavior.Forever"));
            Assert.True(xaml.Contains("x:Name=\"TitleViewport\""));
            Assert.True(xaml.Contains("ClipToBounds=\"True\""));
            Assert.True(host.Contains("trackInfo.PreferredWidthChanged +="));
            Assert.True(main.Contains("ModuleHost.ContentSizeChanged +="));
            Assert.True(main.Contains("ApplyMeasuredIslandSize(true)"));
        }

        static void CalculatesAdaptiveTrackInfoWidths()
        {
            Assert.Equal(112.0, TrackInfoWidthCalculator.Calculate(60, 40));
            Assert.Equal(164.0, TrackInfoWidthCalculator.Calculate(150, 90));
            Assert.Equal(232.0, TrackInfoWidthCalculator.Calculate(400, 100));
        }

        static void AlbumArtIsCenteredWithRoundedClip()
        {
            var root = GetSolutionRoot();
            var albumArt = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "AlbumArtModuleView.xaml"));
            var mainWindow = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml"));

            Assert.True(albumArt.Contains("Width=\"60\""));
            Assert.True(albumArt.Contains("RectangleGeometry Rect=\"0,0,42,42\" RadiusX=\"4\" RadiusY=\"4\""));
            Assert.True(albumArt.Contains("VerticalAlignment=\"Center\""));
            Assert.True(mainWindow.Contains("TranslateTransform Y=\"-2.5\""));
        }

        static void TemporaryInteractionExpandsExpandableMode()
        {
            var controller = new IslandInteractionController();
            controller.PointerEntered(TimeSpan.Zero);

            Assert.Equal(IslandInteractionState.Collapsed, controller.GetState(TimeSpan.FromSeconds(5)));

            controller.SetTemporaryExpanded(true, TimeSpan.FromSeconds(5));

            Assert.Equal(IslandInteractionState.Expanded, controller.GetState(TimeSpan.FromSeconds(5)));
        }

        static void ExpandableLayoutRequiresTemporaryInteractionHotkey()
        {
            var main = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(main.Contains("private bool IsTemporaryInteractionHeld()"));
            Assert.True(main.Contains("placementSettings?.IslandLayouts?.Mode == IslandLayoutMode.Expandable &&"));
            Assert.True(main.Contains("interactionController.SetTemporaryExpanded("));
            Assert.True(main.Contains("temporaryInteractionHeld,"));
            Assert.True(main.Contains("GetInteractionClock());"));
            Assert.False(main.Contains("interactionController.ToggleExpanded"));
            Assert.True(main.Contains("IsHoverTransparencySuppressed()"));
        }

        static void TemporaryInteractionReleaseUsesConfiguredExpandedDuration()
        {
            var controller = new IslandInteractionController
            {
                ExpandedDuration = TimeSpan.FromSeconds(3)
            };
            controller.SetTemporaryExpanded(true, TimeSpan.Zero);
            Assert.Equal(IslandInteractionState.Expanded, controller.GetState(TimeSpan.Zero));

            controller.SetTemporaryExpanded(false, TimeSpan.FromSeconds(1));
            Assert.Equal(IslandInteractionState.Expanded, controller.GetState(TimeSpan.FromMilliseconds(3999)));
            Assert.Equal(IslandInteractionState.Collapsed, controller.GetState(TimeSpan.FromSeconds(4)));
        }

        static void KeepsIslandExpandedWhileEditing()
        {
            var controller = new IslandInteractionController();
            controller.SetEditing(true);

            Assert.Equal(IslandInteractionState.Editing, controller.GetState(TimeSpan.FromHours(1)));
        }

        static void SettingsLayoutModeLabelsAreProductFacing()
        {
            var xaml = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "PlacementSettingsWindow.xaml"));

            Assert.True(xaml.Contains("水平积木"));
            Assert.True(xaml.Contains("自动折叠"));
            Assert.False(xaml.Contains("水平模块"));
            Assert.False(xaml.Contains("单击展开"));
            Assert.False(xaml.Contains("A 模式"));
            Assert.False(xaml.Contains("C 模式"));
            Assert.False(xaml.Contains("悬停展开"));
        }

        static void LayoutCardsDirectlySelectEditedMode()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            Assert.False(xaml.Contains("x:Name=\"EditedLayoutComboBox\""));
            Assert.True(xaml.Contains("Click=\"HorizontalLayoutPreviewButton_Click\""));
            Assert.True(xaml.Contains("Click=\"ExpandableLayoutPreviewButton_Click\""));
            Assert.True(xaml.Contains("x:Name=\"LayoutModePreviewPanel\""));
            Assert.True(xaml.Contains("IsHitTestVisible=\"True\""));
            Assert.True(source.Contains("SelectLayoutMode(IslandLayoutMode.HorizontalBlocks)"));
            Assert.True(source.Contains("SelectLayoutMode(IslandLayoutMode.Expandable)"));
            Assert.True(source.Contains("selectedLayoutMode = mode"));
            Assert.False(source.Contains("class LayoutModeOption"));
            Assert.True(xaml.Contains("Canvas.Left=\"46\""));
            Assert.True(source.Contains("0, 116"));
            Assert.True(source.Contains("1.08, 250"));
            Assert.True(xaml.Contains("<Viewbox Margin=\"10,0\""));
            Assert.True(xaml.Contains("StretchDirection=\"DownOnly\""));
            Assert.True(xaml.Contains("<Canvas Width=\"250\""));
            Assert.True(xaml.Contains("Width=\"14\" Height=\"18\""));
            Assert.True(xaml.Contains("Width=\"11\" Height=\"16\""));
            Assert.False(xaml.Contains("M0,20 L7,11"));
            Assert.False(xaml.Contains("M0,18 L6,10"));
        }

        static void AboutPageExposesV2BetaProductInformation()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"AboutSectionButton\""));
            Assert.True(xaml.Contains("x:Name=\"AboutSettingsPanel\""));
            Assert.True(xaml.Contains("x:Name=\"AboutPageBody\""));
            var aboutPanelStart = xaml.IndexOf("x:Name=\"AboutSettingsPanel\"", StringComparison.Ordinal);
            var aboutPanelEnd = xaml.IndexOf("x:Name=\"SaveButton\"", aboutPanelStart, StringComparison.Ordinal);
            var aboutPanel = xaml.Substring(aboutPanelStart, aboutPanelEnd - aboutPanelStart);
            var headerTitleStart = xaml.IndexOf("x:Name=\"HeaderTitleText\"", StringComparison.Ordinal);
            var headerTitleEnd = xaml.IndexOf("/>", headerTitleStart, StringComparison.Ordinal);
            var headerTitle = xaml.Substring(headerTitleStart, headerTitleEnd - headerTitleStart);
            Assert.False(aboutPanel.Contains("<ScrollViewer"));
            Assert.False(aboutPanel.Contains("VerticalScrollBarVisibility="));
            Assert.False(aboutPanel.Contains("HorizontalScrollBarVisibility="));
            Assert.True(aboutPanel.Contains("FontSize=\"28\""));
            Assert.False(aboutPanel.Contains("MaxWidth=\"760\""));
            Assert.True(aboutPanel.Contains("HorizontalAlignment=\"Stretch\""));
            Assert.True(aboutPanel.Contains("x:Name=\"AboutAppIdentity\""));
            Assert.True(aboutPanel.Contains("x:Name=\"AboutAppLogoContainer\""));
            Assert.True(aboutPanel.Contains("Width=\"56\""));
            Assert.True(aboutPanel.Contains("Height=\"56\""));
            Assert.True(aboutPanel.Contains("<DropShadowEffect BlurRadius=\"14\""));
            Assert.True(aboutPanel.Contains("Opacity=\"0.28\""));
            Assert.True(aboutPanel.Contains("ShadowDepth=\"3\""));
            Assert.True(aboutPanel.Contains("FontSize=\"21\""));
            Assert.True(headerTitle.Contains("FontSize=\"28\""));
            Assert.False(xaml.Contains("x:Name=\"HeaderSubtitleText\""));
            Assert.False(xaml.Contains("调整歌词岛位置、缓存和鼠标避让效果"));
            Assert.True(xaml.Contains("Text=\"大丞子\""));
            Assert.True(xaml.Contains("ProductVersion.DisplayVersionNumber"));
            Assert.True(xaml.Contains("ProductVersion.DisplayVersionChannel"));
            Assert.True(aboutPanel.Contains("Foreground=\"{DynamicResource {x:Static SystemColors.HighlightBrushKey}}\""));
            Assert.True(xaml.Contains("x:Name=\"AboutSettingsList\""));
            Assert.True(aboutPanel.Contains("<Grid x:Name=\"AboutPageBody\""));
            Assert.False(aboutPanel.Contains("MinHeight=\"20\""));
            Assert.Equal(2, CountOccurrences(aboutPanel, "<RowDefinition Height=\"*\" />"));
            Assert.True(aboutPanel.Contains("x:Name=\"AboutTopDivider\""));
            Assert.True(aboutPanel.Contains("Margin=\"0,14,0,0\""));
            Assert.True(aboutPanel.Contains("x:Name=\"AboutBottomDivider\""));
            Assert.True(aboutPanel.Contains("Margin=\"0,14,0,0\""));
            Assert.Equal(3, CountOccurrences(aboutPanel, "<RowDefinition Height=\"56\" />"));
            Assert.True(xaml.Contains("x:Name=\"AboutWebsiteRow\""));
            Assert.True(xaml.Contains("x:Name=\"AboutGitHubRow\""));
            Assert.False(aboutPanel.Contains("x:Name=\"AboutFeedbackRow\""));
            Assert.True(xaml.Contains("x:Name=\"AboutTutorialRow\""));
            Assert.False(aboutPanel.Contains("Style=\"{StaticResource AboutActionRowButtonStyle}\""));
            Assert.Equal(2, CountOccurrences(aboutPanel, "Style=\"{StaticResource TextLinkButtonStyle}\""));
            Assert.True(xaml.Contains("Property=\"IsKeyboardFocused\" Value=\"True\""));
            Assert.Equal(1, CountOccurrences(aboutPanel, "Click=\"OpenGitHubAboutRow_Click\""));
            Assert.Equal(1, CountOccurrences(aboutPanel, "Click=\"RestartTutorialAboutRow_Click\""));
            Assert.Equal(5, CountOccurrences(aboutPanel, "Style=\"{StaticResource AboutDividerStyle}\""));
            Assert.Equal(1, CountOccurrences(aboutPanel, "CornerRadius="));
            Assert.False(xaml.Contains("x:Name=\"AboutInfoGrid\""));
            Assert.False(xaml.Contains("x:Name=\"AboutActionFlow\""));
            Assert.False(xaml.Contains("x:Name=\"AboutUpdateCard\""));
            Assert.Equal(2, CountOccurrences(xaml, "Text=\"即将上线\""));
            Assert.False(xaml.Contains("Text=\"功能规划中\""));
            Assert.True(xaml.Contains("Text=\"软件著作权\" Visibility=\"Collapsed\""));
            Assert.False(xaml.Contains("Text=\"运行环境\""));
            Assert.False(xaml.Contains("Text=\"数据说明\""));
            Assert.True(xaml.Contains("Text=\"打开 GitHub\""));
            Assert.True(xaml.Contains("x:Name=\"OpenGitHubLinkArrow\""));
            Assert.True(xaml.Contains("Data=\"M1,11 L11,1 M4,1 H11 V8\""));
            Assert.False(xaml.Contains("↗"));
            Assert.True(xaml.Contains("Text=\"重新开始教学\""));
            Assert.False(xaml.Contains("x:Key=\"SettingsTextLinkStyle\""));
            Assert.False(source.Contains("TextLinkPressedBrush"));
            Assert.False(source.Contains("ResetTextLinkVisual"));
            Assert.True(source.Contains("private void OpenGitHubAboutRow_Click"));
            Assert.True(source.Contains("private void RestartTutorialAboutRow_Click"));
            Assert.True(source.Contains("Dispatcher.BeginInvoke"));
            var hotkeySourceStart = source.IndexOf("private bool IsHotkeyTextBoxSource", StringComparison.Ordinal);
            var hotkeySourceEnd = source.IndexOf("private bool CanStartWindowDrag", hotkeySourceStart, StringComparison.Ordinal);
            var hotkeySource = source.Substring(hotkeySourceStart, hotkeySourceEnd - hotkeySourceStart);
            Assert.True(hotkeySource.Contains("GetVisualOrLogicalParent(source)"));
            Assert.False(hotkeySource.Contains("VisualTreeHelper.GetParent(source)"));
            Assert.True(xaml.Contains("v2.0 Beta 更新内容"));
            Assert.True(source.Contains("https://github.com/BochengYao/LyricIsland"));
            Assert.True(source.Contains("AboutSettingsPanel.Visibility = section == \"About\""));
        }

        static void SupportDeveloperPageExposesProAndFreeSupportActions()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var proBadgeSvg = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Assets", "pro-support-badge.svg"));
            var proBadgeDarkSvg = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Assets", "pro-support-badge-dark.svg"));
            var proBadgeSourcePng = File.ReadAllBytes(Path.Combine(root, "LyricsIsland.App", "Assets", "pro-support-badge-source.png"));
            var proBadgePng = File.ReadAllBytes(Path.Combine(root, "LyricsIsland.App", "Assets", "pro-support-badge.png"));
            var proBadgeDarkPng = File.ReadAllBytes(Path.Combine(root, "LyricsIsland.App", "Assets", "pro-support-badge-dark.png"));
            var supportStart = xaml.IndexOf("x:Name=\"SupportSettingsPanel\"", StringComparison.Ordinal);
            var supportEnd = xaml.IndexOf("x:Name=\"AboutSettingsPanel\"", supportStart, StringComparison.Ordinal);
            var supportPanel = xaml.Substring(supportStart, supportEnd - supportStart);
            var freeActionsStart = supportPanel.IndexOf("x:Name=\"SupportFreeActionsGrid\"", StringComparison.Ordinal);
            var freeActionsEnd = supportPanel.IndexOf("x:Name=\"SupportStatusText\"", freeActionsStart, StringComparison.Ordinal);
            var freeActions = supportPanel.Substring(freeActionsStart, freeActionsEnd - freeActionsStart);
            var freeColumnsEnd = freeActions.IndexOf("</Grid.ColumnDefinitions>", StringComparison.Ordinal);
            var freeColumns = freeActions.Substring(0, freeColumnsEnd);
            var freeActionsOpeningEnd = freeActions.IndexOf('>');
            var freeActionsOpeningTag = freeActions.Substring(0, freeActionsOpeningEnd);
            var proBenefitsStart = supportPanel.IndexOf("x:Name=\"SupportProBenefitsGrid\"", StringComparison.Ordinal);
            var proBenefitsEnd = supportPanel.IndexOf("<Button x:Name=\"SupportProPurchaseButton\"", proBenefitsStart, StringComparison.Ordinal);
            var proBenefits = supportPanel.Substring(proBenefitsStart, proBenefitsEnd - proBenefitsStart);

            Assert.True(xaml.Contains("x:Name=\"SupportSectionButton\""));
            Assert.True(xaml.Contains("x:Name=\"SupportSettingsPanel\""));
            Assert.True(xaml.IndexOf("x:Name=\"SupportSectionButton\"", StringComparison.Ordinal) >
                xaml.IndexOf("x:Name=\"ThemeToggleRoot\"", StringComparison.Ordinal));
            Assert.True(supportPanel.StartsWith("x:Name=\"SupportSettingsPanel\"", StringComparison.Ordinal));
            Assert.False(supportPanel.Contains("ScrollViewer"));
            Assert.False(supportPanel.Contains("VerticalScrollBarVisibility"));
            Assert.True(supportPanel.Contains("ClipToBounds=\"True\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportPageContent\""));
            Assert.True(supportPanel.Contains("<RowDefinition Height=\"*\" MinHeight=\"96\" />"));
            Assert.True(supportPanel.Contains("<RowDefinition Height=\"1.45*\" MinHeight=\"198\" />"));
            Assert.Equal(1, CountOccurrences(supportPanel, "<Setter Property=\"CornerRadius\" Value=\"9\" />"));
            Assert.Equal(1, CountOccurrences(supportPanel, "Style=\"{StaticResource SupportProContainerStyle}\""));
            Assert.False(supportPanel.Contains("Style=\"{StaticResource CardStyle}\""));
            Assert.True(supportPanel.Contains("<Run Text=\"歌词岛主体功能始终免费。您可以通过免费方式支持项目，您也可以升级Pro来支持开发者。\" />"));
            Assert.True(supportPanel.Contains("<LineBreak />"));
            Assert.True(supportPanel.Contains("<Run Text=\"这对我们真的很重要，谢谢！❤️\" />"));
            Assert.False(supportPanel.Contains("歌词岛始终免费。你可以先通过免费方式支持项目"));
            Assert.True(supportPanel.Contains("Text=\"免费支持\""));
            Assert.False(supportPanel.Contains("不花一分钱，也能帮到歌词岛"));
            Assert.True(supportPanel.Contains("x:Name=\"SupportFreeHeading\""));
            Assert.True(supportPanel.Contains("Margin=\"0,21,0,0\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportFreeActionsGrid\""));
            Assert.True(freeActionsOpeningTag.Contains("Margin=\"0,12,0,0\""));
            Assert.False(freeActionsOpeningTag.Contains("VerticalAlignment=\"Top\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportFeedbackItem\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportReviewTextGrid\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportShareTextGrid\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportGitHubTextGrid\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportFeedbackTextGrid\""));
            Assert.True(supportPanel.Contains("Text=\"意见反馈\""));
            Assert.True(supportPanel.Contains("Text=\"提交问题与功能建议\""));
            Assert.True(freeActions.Contains("<Grid.RowDefinitions>"));
            Assert.Equal(4, CountOccurrences(freeColumns, "<ColumnDefinition Width=\"*\" />"));
            Assert.Equal(3, CountOccurrences(freeColumns, "<ColumnDefinition Width=\"1\" />"));
            Assert.Equal(4, CountOccurrences(freeActions, "<ColumnDefinition Width=\"28\" />"));
            Assert.Equal(4, CountOccurrences(freeActions, "<ColumnDefinition Width=\"12\" />"));
            Assert.Equal(4, CountOccurrences(freeActions, "HorizontalAlignment=\"Center\""));
            Assert.Equal(4, CountOccurrences(freeActions, "Grid.Column=\"2\">"));
            Assert.False(freeActions.Contains("Margin=\"10,0,0,0\""));
            Assert.True(supportPanel.Contains("评价与撰写评价"));
            Assert.True(supportPanel.Contains("分享给身边的朋友"));
            Assert.True(supportPanel.Contains("在 GitHub 上点 Star"));
            Assert.Equal(4, CountOccurrences(freeActions, "<RowDefinition Height=\"34\" />"));
            Assert.Equal(4, CountOccurrences(freeActions, "Grid.Row=\"2\""));
            Assert.True(supportPanel.Contains("M9,18 H15 M10,22 H14"));
            Assert.False(supportPanel.Contains("M3,3 H21 V17 H10 L6,21"));
            Assert.True(supportPanel.Contains("x:Name=\"SupportFeedbackTextGrid\""));
            Assert.False(supportPanel.Contains("Margin=\"6,0,0,0\""));
            Assert.False(supportPanel.Contains("x:Key=\"SupportActionButtonStyle\""));
            Assert.Equal(3, CountOccurrences(supportPanel, "Style=\"{StaticResource TextLinkButtonStyle}\""));
            Assert.True(xaml.Contains("<Setter Property=\"Foreground\" Value=\"#FF2F91FF\" />"));
            Assert.False(xaml.Contains("<Setter Property=\"Opacity\" Value=\"0.78\" />"));
            Assert.Equal(3, CountOccurrences(supportPanel, "Foreground=\"{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}\""));
            Assert.Equal(1, CountOccurrences(supportPanel, "Click=\"SupportStoreReviewButton_Click\""));
            Assert.Equal(1, CountOccurrences(supportPanel, "Click=\"SupportShareButton_Click\""));
            Assert.Equal(1, CountOccurrences(supportPanel, "Click=\"OpenGitHubAboutRow_Click\""));
            Assert.False(source.Contains("SupportFeedbackButton_Click"));
            Assert.True(supportPanel.Contains("Pro 支持计划"));
            Assert.True(supportPanel.Contains("Margin=\"0,8,0,0\""));
            Assert.True(supportPanel.Contains("LineHeight=\"22\""));
            Assert.True(supportPanel.Contains("通过 Microsoft Store 升级 Pro，支持歌词岛持续开发，并解锁更多专属权益。"));
            Assert.True(supportPanel.Contains("抢先使用新功能"));
            Assert.True(supportPanel.Contains("新能力发布后优先体验。"));
            Assert.True(supportPanel.Contains("支持者徽章"));
            Assert.True(supportPanel.Contains("Pro 后使用当前账号识别支持者身份。"));
            Assert.True(supportPanel.Contains("永久有效"));
            Assert.True(supportPanel.Contains("买断制会员，已购权益长期有效。"));
            Assert.True(supportPanel.Contains("x:Name=\"SupportProPurchaseButton\""));
            Assert.True(supportPanel.Contains("Click=\"SupportProPurchaseButton_Click\""));
            Assert.True(supportPanel.Contains("Text=\"升级 Pro · ¥7\""));
            Assert.False(supportPanel.Contains("Pro 即将上线 Microsoft Store"));
            Assert.True(source.Contains("MicrosoftStoreProProductId = \"lyric_island_pro\""));
            Assert.True(source.Contains("GetAssociatedStoreProductsAsync(new[] { \"Durable\" })"));
            Assert.True(source.Contains("RequestPurchaseAsync()"));
            Assert.True(supportPanel.Contains("{x:Static SystemColors.HighlightBrushKey}"));
            Assert.True(supportPanel.Contains("x:Key=\"SupportLineIconStyle\""));
            Assert.True(supportPanel.Contains("x:Key=\"SupportBrandIconStyle\""));
            Assert.True(supportPanel.Contains("Lucide (ISC)"));
            Assert.True(supportPanel.Contains("Primer Octicons (MIT)"));
            Assert.True(supportPanel.Contains("<Setter Property=\"BorderBrush\" Value=\"{DynamicResource SettingsControlBorderBrush}\" />"));
            Assert.True(supportPanel.Contains("<Setter Property=\"BorderThickness\" Value=\"1\" />"));
            Assert.False(supportPanel.Contains("<Setter Property=\"BorderThickness\" Value=\"3.78\" />"));
            Assert.True(supportPanel.Contains("x:Name=\"SupportProContentGrid\""));
            Assert.True(supportPanel.Contains("x:Name=\"SupportProEmblem\""));
            Assert.True(supportPanel.Contains("AutomationProperties.Name=\"歌词岛 + Pro + 歌词岛 + Pro\""));
            Assert.True(supportPanel.Contains("Width=\"64\""));
            Assert.True(supportPanel.Contains("Height=\"64\""));
            Assert.True(supportPanel.Contains("Margin=\"0,0,8,0\""));
            Assert.True(supportPanel.Contains("Source=\"Assets/pro-support-badge.png\""));
            Assert.True(supportPanel.Contains("IsHitTestVisible=\"False\""));
            Assert.True(supportPanel.Contains("Focusable=\"False\""));
            Assert.True(supportPanel.Contains("<Setter Property=\"ClipToBounds\" Value=\"True\" />"));
            Assert.False(supportPanel.Contains("SupportProBadgeGlyphStyle"));
            Assert.False(supportPanel.Contains("SupportProBadgeTextCanvas"));
            Assert.False(supportPanel.Contains("RotateTransform"));
            Assert.True(proBadgeSvg.Contains("Generator: visioncortex VTracer"));
            Assert.True(proBadgeDarkSvg.Contains("Generator: visioncortex VTracer"));
            Assert.True(proBadgeSvg.Contains("width=\"170\" height=\"172\" viewBox=\"0 0 170 172\""));
            Assert.True(proBadgeDarkSvg.Contains("width=\"170\" height=\"172\" viewBox=\"0 0 170 172\""));
            Assert.True(CountOccurrences(proBadgeSvg, "<path") > 100);
            Assert.True(CountOccurrences(proBadgeDarkSvg, "<path") > 100);
            Assert.True(proBadgeSvg.Contains("fill=\"#000000\""));
            Assert.True(proBadgeDarkSvg.Contains("fill=\"#FFFFFF\""));
            Assert.False(proBadgeSvg.Contains("<text"));
            Assert.False(proBadgeDarkSvg.Contains("<text"));
            Assert.False(proBadgeSvg.Contains("<circle"));
            Assert.Equal(Convert.ToBase64String(proBadgeSourcePng), Convert.ToBase64String(proBadgePng));
            Assert.False(Convert.ToBase64String(proBadgePng) == Convert.ToBase64String(proBadgeDarkPng));
            Assert.True(source.Contains("UpdateSupportProBadge(dark);"));
            Assert.True(source.Contains("Assets/pro-support-badge-dark.png"));
            Assert.True(source.Contains("Assets/pro-support-badge.png"));
            Assert.False(supportPanel.Contains("Grid.RowSpan=\"3\""));
            Assert.True(xaml.Contains("x:Key=\"SidebarKeyboardFocusVisualStyle\""));
            Assert.True(xaml.Contains("<Setter Property=\"FocusVisualStyle\" Value=\"{StaticResource SidebarKeyboardFocusVisualStyle}\" />"));
            Assert.True(supportPanel.Contains("<Setter Property=\"Foreground\" Value=\"{DynamicResource SettingsControlForegroundBrush}\" />"));
            Assert.False(supportPanel.Contains("M10,18 C8,16 2,12 2,7"));
            Assert.Equal(3, CountOccurrences(proBenefits, "<ColumnDefinition Width=\"*\" />"));
            Assert.False(proBenefits.Contains("1.08*"));
            Assert.False(proBenefits.Contains("1.18*"));
            Assert.Equal(3, CountOccurrences(proBenefits, "Style=\"{StaticResource SupportProFeatureTitleStyle}\""));
            Assert.Equal(3, CountOccurrences(proBenefits, "Margin=\"33,1,0,0\""));
            Assert.Equal(3, CountOccurrences(proBenefits, "LineHeight=\"16\""));
            Assert.True(supportPanel.Contains("<Setter Property=\"TextWrapping\" Value=\"NoWrap\" />"));
            Assert.False(supportPanel.Contains("升级 Pro 会员"));
            Assert.False(supportPanel.Contains("抢先使用所有新功能"));
            Assert.False(supportPanel.Contains("支持与否不会影响"));
            Assert.False(supportPanel.Contains("不经过 Microsoft Store"));
            Assert.False(supportPanel.Contains("提交建议"));
            Assert.False(supportPanel.Contains("反馈 Bug"));
            Assert.False(supportPanel.Contains("永久拥有"));
            Assert.False(supportPanel.Contains("微信支付"));
            Assert.False(supportPanel.Contains("支付宝"));
            Assert.False(supportPanel.Contains("接收邮箱"));
            Assert.False(supportPanel.Contains("自定义金额"));
            Assert.True(xaml.Contains("<Setter Property=\"Background\" Value=\"{DynamicResource SettingsControlBackgroundBrush}\" />"));
            Assert.True(xaml.Contains("<Setter Property=\"BorderBrush\" Value=\"{DynamicResource SettingsControlBorderBrush}\" />"));
            Assert.False(source.Contains("ApplyTextTheme(this, primary, muted)"));
            Assert.True(source.Contains("SupportSettingsPanel.Visibility = section == \"Support\""));
            Assert.True(source.Contains("private void SupportStoreReviewButton_Click"));
            Assert.True(source.Contains("private void SupportShareButton_Click"));
            Assert.True(source.Contains("SupportStatusText.BeginAnimation(OpacityProperty, null)"));
            Assert.True(source.Contains("BeginTime = TimeSpan.FromSeconds(3)"));
            Assert.True(source.Contains("Duration = TimeSpan.FromMilliseconds(280)"));
            Assert.True(source.Contains("SupportStatusText.BeginAnimation(OpacityProperty, fadeAnimation)"));
            Assert.False(source.Contains("SupportContributionPolicy.Evaluate"));
            Assert.False(source.Contains("SupportEmailTextBox"));
            Assert.False(source.Contains("通道尚未配置"));
        }

        static void FormatsThePublicBetaVersion()
        {
            Assert.Equal("v2.0.24 Beta", ProductVersion.FormatDisplayVersion("2.0.24-Beta"));
            Assert.Equal("v2.0.24", ProductVersion.FormatDisplayVersion("2.0.24"));
            Assert.Equal("v2.0.24 Beta", ProductVersion.FormatDisplayVersion("2.0.24-beta+build.17"));
            Assert.True(ProductVersion.DisplayVersionNumber.StartsWith("v", StringComparison.Ordinal));
            Assert.Equal("Beta", ProductVersion.DisplayVersionChannel);
        }

        static void ReleaseVersionHasOneSourceAndAutoIncrements()
        {
            var root = GetSolutionRoot();
            var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
            var appProject = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "LyricsIsland.App.csproj"));
            var publishScript = File.ReadAllText(Path.Combine(root, "tools", "publish-next-version.ps1"));

            var versionMatch = System.Text.RegularExpressions.Regex.Match(
                props,
                "<VersionPrefix>\\d+\\.\\d+\\.\\d+</VersionPrefix>");
            Assert.True(versionMatch.Success);
            Assert.True(props.Contains("<VersionSuffix>Beta</VersionSuffix>"));
            Assert.False(appProject.Contains("<Version>"));
            Assert.False(appProject.Contains("<FileVersion>"));
            Assert.True(publishScript.Contains("$nextPatch = $currentPatch + 1"));
            Assert.True(publishScript.Contains("dotnet run"));
            Assert.True(publishScript.Contains("dotnet publish"));
            Assert.True(publishScript.Contains("WaitForExit"));
            Assert.True(publishScript.Contains("Move-DirectoryWithRetry"));
        }

        static void ModuleToolboxCapturesMouseDownForDrag()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(xaml.Contains("PreviewMouseLeftButtonDown=\"ModuleToolbox_PreviewMouseLeftButtonDown\""));
            Assert.True(source.Contains("ModuleToolbox_PreviewMouseLeftButtonDown"));
            Assert.True(source.Contains("moduleToolboxDragStartPoint"));
            Assert.True(source.Contains("moduleToolboxDragInProgress"));
            Assert.True(source.Contains("moduleToolboxDragStartPoint = null"));
            Assert.True(source.Contains("ReferenceEquals(source, ModuleToolbox)"));
            Assert.True(source.Contains("e.Handled = true"));
            Assert.False(source.Contains("addModule?.Invoke"));
            Assert.False(mainWindowSource.Contains("AddModuleFromToolbox"));
            Assert.True(source.Contains("setModuleDragActive?.Invoke(true)"));
            Assert.True(mainWindowSource.Contains("moduleDragActive ||"));
            Assert.True(mainWindowSource.Contains("if (settingsWindow != null)"));
            Assert.True(mainWindowSource.Contains("layoutEditing ||"));
            Assert.True(mainWindowSource.Contains("? DragDropEffects.Copy"));
            Assert.True(mainWindowSource.Contains("ModuleHost.FindInsertionIndex(pointerX, payload)"));
            var mainWindowXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml"));
            Assert.True(mainWindowXaml.Contains("AllowDrop=\"True\""));
            Assert.True(mainWindowXaml.Contains("PreviewDragOver=\"ModuleHost_DragOver\""));
            Assert.True(mainWindowXaml.Contains("HorizontalAlignment=\"Stretch\""));
            Assert.True(xaml.Contains("GiveFeedback=\"ModuleDrag_GiveFeedback\""));
            Assert.True(xaml.Contains("Cursor=\"Hand\""));
            Assert.True(source.Contains("ShowModuleDragGhost(option)"));
            Assert.True(source.Contains("ModuleDragGhostWindow"));
            Assert.True(source.Contains("IslandLayoutDragPayload.CreateDataObject"));
            Assert.False(xaml.Contains("PreviewDrop=\"ModuleToolbox_Drop\""));
            Assert.False(source.Contains("SetModuleRemovalPreview"));
            Assert.True(xaml.Contains("Text=\"自定义模块\""));
            Assert.True(xaml.Contains("拖出岛外删除"));
            Assert.False(xaml.Contains("拖出歌词岛松手删除"));
            Assert.True(xaml.Contains("layoutEditing:ModuleToolboxCard"));
            Assert.True(mainWindowSource.Contains("PreviewModuleDragSize"));
            Assert.True(mainWindowSource.Contains("moduleDragPreviewWidth"));
        }

        static void SettingsStaysModelessWhileEditingModules()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(mainWindowSource.Contains("settingsWindow.Show();"));
            Assert.False(mainWindowSource.Contains("window.ShowDialog();"));
            Assert.False(mainWindowSource.Contains("Owner = this"));
            Assert.True(settingsSource.Contains("Close();"));
            Assert.False(settingsSource.Contains("DialogResult ="));
        }

        static void ModuleDropHandlerIsRegisteredOnce()
        {
            var root = GetSolutionRoot();
            var mainWindowXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml"));

            Assert.Equal(1, CountOccurrences(mainWindowXaml, "Drop=\"ModuleHost_Drop\""));
            Assert.Equal(1, CountOccurrences(mainWindowXaml, "DragOver=\"ModuleHost_DragOver\""));
            Assert.Equal(1, CountOccurrences(mainWindowXaml, "DragLeave=\"ModuleHost_DragLeave\""));
        }

        static void LayoutModuleAddAndRemoveAnimateIslandSize()
        {
            var source = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(source.Contains("bool forceSizeAnimation = false"));
            Assert.True(source.Contains("var animateSize = forceSizeAnimation ||"));
            Assert.True(source.Contains("ApplyInteractionState(IslandInteractionState.Editing, payload.NewType.HasValue)"));
            Assert.True(source.Contains("ApplyInteractionState(IslandInteractionState.Editing, true)"));
            Assert.True(source.Contains("CompositionTarget.Rendering += islandSizeAnimationFrameHandler"));
            Assert.True(source.Contains("int durationMilliseconds = 360"));
            Assert.True(source.Contains("AnimateIslandSize(targetWidth, targetHeight, 180)"));
            Assert.True(source.Contains("if (moduleDragActive == value)"));
            Assert.True(source.Contains("committedModuleDrops.TryCommit"));
            Assert.True(source.Contains("committedModuleDrops.Reset"));
            Assert.True(source.Contains("PreviewModuleDragSize"));
            Assert.True(source.Contains("ClearModuleDragSizePreview"));
        }

        static void ExpandableIslandAnimatesMeasuredSize()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var mainWindowXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml"));

            Assert.True(mainWindowSource.Contains("AnimateIslandSize"));
            Assert.True(mainWindowSource.Contains("Math.Pow(1 - progress, 4)"));
            Assert.True(mainWindowSource.Contains("CompositionTarget.Rendering"));
            Assert.True(mainWindowXaml.Contains("SizeChanged=\"Window_SizeChanged\""));
        }

        static void AutoRetractDelaysAreConfigurable()
        {
            var root = GetSolutionRoot();
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "OverlayPlacementSettings.cs"));
            var settingsXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(settingsSource.Contains("NoPlaybackAutoRetractSeconds"));
            Assert.True(settingsSource.Contains("ExpandedAutoCollapseSeconds"));
            Assert.True(settingsXaml.Contains("x:Name=\"NoPlaybackAutoRetractSlider\""));
            Assert.True(settingsXaml.Contains("x:Name=\"ExpandedAutoCollapseSlider\""));
            Assert.True(settingsXaml.Contains("Minimum=\"0\""));
            Assert.True(mainWindowSource.Contains("placementSettings.NoPlaybackAutoRetractSeconds"));
            Assert.True(mainWindowSource.Contains("placementSettings.ExpandedAutoCollapseSeconds"));
            Assert.True(mainWindowSource.Contains("TimeSpan.MaxValue"));
        }

        static void SettingsAndTemporaryInteractionRestartNoPlaybackCountdown()
        {
            var root = GetSolutionRoot();
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var settings = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));

            Assert.True(main.Contains("RestartNoPlaybackAutoRetractCountdown()"));
            Assert.True(main.Contains("if (temporaryExpansionChanged)"));
            Assert.True(main.Contains("settingsWindow = null;"));
            Assert.True(settings.Contains("Text=\"位置与状态\""));
            Assert.False(settings.Contains("Text=\"位置\""));
        }

        static void LyricsModuleExposesConfigurableWidth()
        {
            var root = GetSolutionRoot();
            var instanceSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.Core", "Layout", "IslandModuleInstance.cs"));
            var hostSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));
            var lyricsXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "LyricsModuleView.xaml"));

            Assert.True(instanceSource.Contains("LyricsWidth"));
            Assert.True(instanceSource.Contains("DefaultLyricsWidth"));
            Assert.True(hostSource.Contains("ApplyModuleSettings"));
            Assert.True(hostSource.Contains("module.LyricsWidth"));
            Assert.False(lyricsXaml.Contains("Width=\"436\""));
        }

        static void IslandBackgroundWidthReservesShapedEdgePadding()
        {
            var source = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(source.Contains("IslandHorizontalShapePadding"));
            Assert.True(source.Contains("contentSize.Width + IslandHorizontalShapePadding"));
            Assert.True(source.Contains("IslandHorizontalShapePadding = 144"));
        }

        static void PlaybackControlsUseMediaGlyphs()
        {
            var root = GetSolutionRoot();
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "PlaybackControlsModuleView.xaml"));

            Assert.True(source.Contains("x:Name=\"PlayPauseGlyph\""));
            Assert.True(source.Contains("x:Name=\"PauseGlyph\""));
            Assert.True(source.Contains("Data=\"M 11,1 L 11,19 L 0,10 Z M 23,1"));
            Assert.True(source.Contains("Data=\"M 1,0 L 16,9 L 1,18 Z\""));
            Assert.True(source.Contains("Stretch=\"Uniform\""));
            Assert.False(source.Contains("M 0,0 L 18,11 L 0,22 Z"));
            Assert.False(source.Contains("<TranslateTransform Y=\"-2\""));
            Assert.True(source.Contains("Width=\"108\""));
            Assert.False(source.Contains("Text=\"⏮\""));
            Assert.False(source.Contains("Text=\"⏭\""));
        }

        static void PlaybackControlsAreNotConsumedByLayoutDrag()
        {
            var root = GetSolutionRoot();
            var hostSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));
            var windowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var controlsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "PlaybackControlsModuleView.xaml.cs"));

            Assert.True(hostSource.Contains("!LayoutEditingEnabled"));
            Assert.True(windowSource.Contains("IsInteractiveMouseSource"));
            Assert.True(controlsSource.Contains("PlayPauseButton.IsEnabled = session != null"));
            Assert.True(controlsSource.Contains("PlayPauseButton.IsHitTestVisible = value"));
            Assert.True(windowSource.Contains("SetPlaybackInteractionEnabled(suppressHoverTransparency)"));
        }

        static void ConfiguredKeyTemporarilySuppressesHoverTransparency()
        {
            var root = GetSolutionRoot();
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var hotkeys = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "HotkeySettings.cs"));

            Assert.True(source.Contains("TemporaryInteraction"));
            Assert.True(source.Contains("HotkeyGestureParser.IsPressed"));
            Assert.True(hotkeys.Contains("TemporaryInteraction = \"Ctrl\""));
            Assert.True(source.Contains("Window_KeyUp"));
        }

        static void SnapsModuleWithinEighteenPixels()
        {
            var bounds = new[] { new LayoutInsertionTarget(0, 100), new LayoutInsertionTarget(1, 220) };

            Assert.Equal(1, LayoutEditSession.FindInsertionIndex(204, bounds, 18));
            Assert.Equal(-1, LayoutEditSession.FindInsertionIndex(180, bounds, 18));
        }

        static void MovesModuleAfterCrossingMidpoint()
        {
            var profile = new IslandLayoutProfile();
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.AlbumArt));
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.Lyrics));
            var session = new LayoutEditSession(profile);

            session.Move(session.Draft.Modules[0].Id, 2);

            Assert.Equal(IslandModuleType.Lyrics, session.Draft.Modules[0].Type);
            Assert.Equal(IslandModuleType.AlbumArt, session.Draft.Modules[1].Type);
        }

        static void MovesModuleOneSlotRightWithoutSkipping()
        {
            var profile = new IslandLayoutProfile();
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.AlbumArt));
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.Lyrics));
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.Divider));
            var session = new LayoutEditSession(profile);

            session.Move(session.Draft.Modules[0].Id, 2);

            Assert.Equal(IslandModuleType.Lyrics, session.Draft.Modules[0].Type);
            Assert.Equal(IslandModuleType.AlbumArt, session.Draft.Modules[1].Type);
            Assert.Equal(IslandModuleType.Divider, session.Draft.Modules[2].Type);
        }

        static void AllowsDuplicateModulesInOneLayout()
        {
            var profile = new IslandLayoutProfile();
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.AlbumArt));
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.Lyrics));
            var session = new LayoutEditSession(profile);

            session.Add(IslandModuleType.AlbumArt, 2);
            session.Add(IslandModuleType.Divider, 2);
            session.Add(IslandModuleType.Divider, 3);

            Assert.Equal(2, session.Draft.Modules.FindAll(module => module.Type == IslandModuleType.AlbumArt).Count);
            Assert.Equal(2, session.Draft.Modules.FindAll(module => module.Type == IslandModuleType.Divider).Count);
        }

        static void DeduplicatesOneLayoutDropOperation()
        {
            var session = new LayoutEditSession(new IslandLayoutProfile());
            var guard = new LayoutDropCommitGuard();

            if (guard.TryCommit("drag-one")) session.Add(IslandModuleType.AlbumArt, 0);
            if (guard.TryCommit("drag-one")) session.Add(IslandModuleType.AlbumArt, 1);
            if (guard.TryCommit("drag-two")) session.Add(IslandModuleType.AlbumArt, 1);

            Assert.Equal(2, session.Draft.Modules.Count);
            guard.Reset();
            Assert.True(guard.TryCommit("drag-one"));
            Assert.False(guard.TryCommit(string.Empty));
        }

        static void DeletesModulesOnlyAfterOutsideMouseRelease()
        {
            Assert.True(ModuleDragCompletionDecision.ShouldDeleteExistingModule(
                true, true, false, false));
            Assert.False(ModuleDragCompletionDecision.ShouldDeleteExistingModule(
                true, true, true, false));
            Assert.False(ModuleDragCompletionDecision.ShouldDeleteExistingModule(
                true, false, false, false));
            Assert.False(ModuleDragCompletionDecision.ShouldDeleteExistingModule(
                true, true, false, true));
            Assert.False(ModuleDragCompletionDecision.ShouldDeleteExistingModule(
                false, true, false, false));
        }

        static void ProjectsFixedPlaceholderWhileReordering()
        {
            var projected = ModuleLayoutProjection.Project(
                new[] { "cover", "lyrics", "controls" },
                "cover",
                2);

            Assert.Equal("lyrics", projected[0]);
            Assert.Equal("controls", projected[1]);
            Assert.Equal(ModuleLayoutProjection.PlaceholderId, projected[2]);
        }

        static void ConvertsProjectedDestinationToMoveBoundary()
        {
            Assert.Equal(3, ModuleLayoutProjection.ToMoveInsertionIndex(0, 2, 3));
            Assert.Equal(0, ModuleLayoutProjection.ToMoveInsertionIndex(2, 0, 3));
            Assert.Equal(2, ModuleLayoutProjection.ToMoveInsertionIndex(0, 1, 3));
        }

        static void LayoutDragShowsSnappedInsertionPlaceholder()
        {
            var root = GetSolutionRoot();
            var host = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));
            var hostXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml"));
            var settingsXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(host.Contains("ShowInsertionPreview"));
            Assert.True(host.Contains("ModulePanel.TranslatePoint(new Point(0, 0), this).X"));
            Assert.True(host.Contains("moduleDragStartPoint"));
            Assert.True(host.Contains("MinimumHorizontalDragDistance"));
            Assert.True(host.Contains("element.CaptureMouse()"));
            Assert.True(host.Contains("element.ReleaseMouseCapture()"));
            Assert.True(host.Contains("ModulePanel.Children.Insert"));
            Assert.True(hostXaml.Contains("x:Name=\"InsertionIndicator\""));
            Assert.True(hostXaml.Contains("<Grid Background=\"Transparent\">"));
            Assert.True(hostXaml.Contains("#661677FF"));
            Assert.True(hostXaml.Contains("CornerRadius=\"9\""));
            Assert.True(host.Contains("dragPlaceholder.Width = previewWidth"));
            Assert.True(host.Contains("AnimateModuleReflow"));
            Assert.True(host.Contains("TimeSpan.FromMilliseconds(120)"));
            Assert.True(host.Contains("source.Visibility = Visibility.Collapsed"));
            Assert.False(settingsXaml.Contains("ModuleToolbox_Drop"));
            Assert.True(settingsXaml.Contains("ModuleToolboxCard"));
            Assert.True(settingsSource.Contains("ModuleToolboxCatalog.All"));
            Assert.True(host.Contains("ModulePreviewMetrics.GetWidth"));
            var metrics = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "LayoutEditing", "ModulePreviewMetrics.cs"));
            Assert.True(metrics.Contains("case IslandModuleType.Lyrics: return 92"));
            Assert.True(metrics.Contains("case IslandModuleType.Divider: return 38"));
            var mainWindow = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            Assert.True(mainWindow.Contains("DispatcherPriority.Render"));
            Assert.True(mainWindow.Contains("QueueModuleDragPreview"));
            var dividerXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "DividerModuleView.xaml"));
            Assert.True(dividerXaml.Contains("Background=\"Transparent\""));
            Assert.False(host.Contains("Cursors.SizeWE"));
            Assert.True(host.Contains("LayoutDragCursors.OpenHand"));
            Assert.True(host.Contains("LayoutDragCursors.ClosedHand"));
            Assert.True(host.Contains("ModuleDragCompletionDecision.ShouldDeleteExistingModule"));
            var ghost = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "LayoutEditing", "ModuleDragGhostWindow.cs"));
            Assert.True(ghost.Contains("new ModuleToolboxCard"));
            Assert.False(ghost.Contains("MinWidth"));
            Assert.True(File.Exists(Path.Combine(root, "LyricsIsland.App", "Assets", "grab-open.cur")));
            Assert.True(File.Exists(Path.Combine(root, "LyricsIsland.App", "Assets", "grab-closed.cur")));
        }

        static void CancelsLayoutDraftWithoutMutatingOriginal()
        {
            var profile = new IslandLayoutProfile();
            profile.Modules.Add(new IslandModuleInstance(IslandModuleType.Lyrics));
            var session = new LayoutEditSession(profile);

            session.Add(IslandModuleType.Divider, 1);
            session.Cancel();

            Assert.Equal(1, profile.Modules.Count);
        }

        static void UsesApprovedLyricOffsetHotkeys()
        {
            var root = GetSolutionRoot();
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "HotkeySettings.cs"));
            var settingsXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settingsWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(source.Contains("Ctrl+Alt+Left"));
            Assert.True(source.Contains("Ctrl+Alt+Right"));
            Assert.True(source.Contains("Ctrl+Alt+Down"));
            Assert.True(source.Contains("TemporaryInteraction"));
            Assert.True(settingsXaml.Contains("EarlierHotkeyTextBox"));
            Assert.True(settingsXaml.Contains("LaterHotkeyTextBox"));
            Assert.True(settingsXaml.Contains("ResetHotkeyTextBox"));
            Assert.True(settingsXaml.Contains("TemporaryInteractionHotkeyTextBox"));
            Assert.True(settingsXaml.Contains("PreviewKeyDown=\"HotkeyTextBox_PreviewKeyDown\""));
            Assert.True(settingsXaml.Contains("IsReadOnly=\"True\""));
            Assert.True(settingsXaml.Contains("LinearGradientBrush StartPoint=\"0,0\" EndPoint=\"1,1\""));
            Assert.True(settingsWindowSource.Contains("FormatHotkeyKey"));
            Assert.True(settingsWindowSource.Contains("Keyboard.ClearFocus()"));
            Assert.False(settingsXaml.Contains("组合键使用 + 分隔"));
        }

        static void TranslationModeExplainsSingleLineRestriction()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(xaml.Contains("PreviewMouseLeftButtonDown=\"SingleLineRadioButton_PreviewMouseLeftButtonDown\""));
            Assert.True(xaml.Contains("x:Name=\"TranslationModeToast\""));
            Assert.False(xaml.Contains("<Popup x:Name=\"TranslationModeToast\""));
            Assert.True(xaml.Contains("SettingsToastBackgroundBrush"));
            Assert.True(xaml.Contains("SettingsToastForegroundBrush"));
            Assert.True(xaml.Contains("翻译模式下仅支持多行模式"));
            Assert.True(source.Contains("ShowTranslationModeToast"));
            Assert.True(source.Contains("FadeOutTranslationModeToast"));
            Assert.True(source.Contains("TimeSpan.FromMilliseconds(320)"));
            Assert.False(source.Contains("TranslationModeToast.IsOpen"));
            Assert.False(source.Contains("MessageBox.Show("));
            Assert.False(source.Contains("SingleLineRadioButton.IsEnabled = false"));
        }

        static void SettingsExposesPlayerSelection()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(
                root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settings = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var detector = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Media", "InstalledPlayerCatalog.cs"));

            Assert.True(xaml.Contains("x:Name=\"PlayerSelectionComboBox\""));
            Assert.True(xaml.Contains("自动选择"));
            Assert.True(xaml.Contains("PlayerSelectionComboBox_SelectionChanged"));
            Assert.True(xaml.Contains("x:Name=\"PlayerSelectionHintText\""));
            Assert.True(xaml.Contains("注：网易云音乐由于接口限制无法实时同步歌曲进度（播放器内拖动进度条无法同步）"));
            Assert.True(xaml.Contains("PlacementTarget=\"{Binding ElementName=TemplateRoot}\""));
            Assert.True(xaml.Contains("StaysOpen=\"False\""));
            Assert.True(xaml.Contains("ComboBoxItem_PreviewMouseLeftButtonUp"));
            Assert.True(xaml.Contains("IsSynchronizedWithCurrentItem=\"False\""));
            Assert.True(xaml.Contains("SelectedValuePath=\"Value\""));
            Assert.True(settings.Contains("installedPlayers"));
            Assert.True(settings.Contains("NormalizePlayerSelection"));
            Assert.True(settings.Contains("ThenByDescending(option => option.IsDetected)"));
            Assert.True(settings.Contains("PlayerSelectionComboBox.SelectedValue"));
            Assert.True(settings.Contains("workingSettings.LockedSourceAppUserModelId"));
            Assert.True(settings.Contains("source is ComboBoxItem"));
            Assert.True(settings.Contains("DeepClone()"));
            Assert.True(settings.Contains("优先选择 "));
            Assert.False(settings.Contains("已锁定到 "));
            Assert.True(settings.Contains("注：网易云音乐由于接口限制无法实时同步歌曲进度（播放器内拖动进度条无法同步）"));
            Assert.True(settings.Contains("未检测到，启动播放器后生效"));
            Assert.True(detector.Contains("CurrentVersion\\Uninstall"));
            Assert.True(detector.Contains("AppModel\\Repository\\Packages"));
            Assert.True(detector.Contains("SpecialFolder.CommonStartMenu"));
            Assert.True(detector.Contains("cloudmusic.exe"));
            Assert.True(detector.Contains("KuGou.exe"));
            Assert.True(detector.Contains("KuwoMusic.exe"));
            Assert.True(detector.Contains("Spotify.exe"));
            Assert.True(detector.Contains("QQMusic.exe"));
        }

        static void EstimatesMissingPlaybackTimeline()
        {
            var clock = new FakeMonotonicClock();
            var coordinator = new TimelineCoordinator(clock);
            coordinator.Update(TimeSpan.FromSeconds(20), true, MediaPlaybackStatus.Playing);
            clock.Advance(TimeSpan.FromSeconds(3));

            var result = coordinator.Update(TimeSpan.Zero, false, MediaPlaybackStatus.Playing);

            Assert.Equal(TimeSpan.FromSeconds(23), result.Position);
            Assert.Equal(TimelineReliability.Estimated, result.Reliability);
        }

        static void AdvancesRepeatedReliableTimelineSample()
        {
            var clock = new FakeMonotonicClock();
            var coordinator = new TimelineCoordinator(clock);
            coordinator.Update(TimeSpan.Zero, true, MediaPlaybackStatus.Playing);
            clock.Advance(TimeSpan.FromSeconds(3));

            var result = coordinator.Update(TimeSpan.Zero, true, MediaPlaybackStatus.Playing);

            Assert.Equal(TimeSpan.FromSeconds(3), result.Position);
            Assert.Equal(TimelineReliability.Estimated, result.Reliability);
        }

        static void StartsLocalTimelineWithoutMetadata()
        {
            var clock = new FakeMonotonicClock();
            var coordinator = new TimelineCoordinator(clock);
            coordinator.Update(TimeSpan.Zero, false, MediaPlaybackStatus.Playing);
            clock.Advance(TimeSpan.FromSeconds(2));

            var result = coordinator.Update(TimeSpan.Zero, false, MediaPlaybackStatus.Playing);

            Assert.Equal(TimeSpan.FromSeconds(2), result.Position);
            Assert.Equal(TimelineReliability.Estimated, result.Reliability);
        }

        static void UsesMaximumSeekTimeWhenSmtcEndTimeIsMissing()
        {
            var duration = TimelineMetadataResolver.ResolveDuration(
                TimeSpan.Zero,
                TimeSpan.FromMinutes(3));

            Assert.Equal(TimeSpan.FromMinutes(3), duration);
            Assert.True(TimelineMetadataResolver.HasReliableTimeline(
                duration,
                TimeSpan.FromSeconds(20)));
        }

        static void PrefersSmtcEndTimeWhenBothDurationFieldsAreAvailable()
        {
            var duration = TimelineMetadataResolver.ResolveDuration(
                TimeSpan.FromSeconds(181),
                TimeSpan.FromSeconds(180));

            Assert.Equal(TimeSpan.FromSeconds(181), duration);
        }

        static void CompensatesStalePlayingTimelineSamples()
        {
            var sampledAt = DateTimeOffset.Parse("2026-07-13T18:00:05+08:00");

            var position = TimelineSampleCompensator.Compensate(
                TimeSpan.FromSeconds(20),
                sampledAt.AddSeconds(-3),
                sampledAt,
                MediaPlaybackStatus.Playing,
                TimeSpan.FromMinutes(3));

            Assert.Equal(TimeSpan.FromSeconds(23), position);
        }

        static void DoesNotCompensatePausedTimelineSamples()
        {
            var sampledAt = DateTimeOffset.Parse("2026-07-13T18:00:05+08:00");

            var position = TimelineSampleCompensator.Compensate(
                TimeSpan.FromSeconds(20),
                sampledAt.AddSeconds(-3),
                sampledAt,
                MediaPlaybackStatus.Paused,
                TimeSpan.FromMinutes(3));

            Assert.Equal(TimeSpan.FromSeconds(20), position);
        }

        static void FreezesEstimatedTimelineWhilePaused()
        {
            var clock = new FakeMonotonicClock();
            var coordinator = new TimelineCoordinator(clock);
            coordinator.Update(TimeSpan.FromSeconds(20), true, MediaPlaybackStatus.Playing);
            clock.Advance(TimeSpan.FromSeconds(2));
            coordinator.Update(TimeSpan.Zero, false, MediaPlaybackStatus.Paused);
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(TimeSpan.FromSeconds(22), coordinator.Update(TimeSpan.Zero, false, MediaPlaybackStatus.Paused).Position);
        }

        static void AcceptsLargeRealTimelineCorrection()
        {
            var clock = new FakeMonotonicClock();
            var coordinator = new TimelineCoordinator(clock);
            coordinator.Update(TimeSpan.FromSeconds(20), true, MediaPlaybackStatus.Playing);
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(TimeSpan.FromSeconds(40), coordinator.Update(TimeSpan.FromSeconds(40), true, MediaPlaybackStatus.Playing).Position);
        }

        static void AllowsOnlyOneNamedApplicationInstance()
        {
            var name = "LyricsIsland.Tests." + Guid.NewGuid().ToString("N");
            using (var first = SingleInstanceGuard.TryAcquire(name))
            using (var second = SingleInstanceGuard.TryAcquire(name))
            {
                Assert.True(first.HasHandle);
                Assert.False(second.HasHandle);
            }
        }

        static void SignalsExistingApplicationInstance()
        {
            var name = "LyricsIsland.Tests." + Guid.NewGuid().ToString("N");
            using (var first = SingleInstanceGuard.TryAcquire(name))
            using (var second = SingleInstanceGuard.TryAcquire(name))
            {
                second.SignalExistingInstance();

                Assert.True(first.ConsumeActivationSignal(TimeSpan.FromMilliseconds(100)));
            }
        }

        static void KeepsIslandVisibleWhilePlayingEvenWithoutLyrics()
        {
            Assert.False(PlaybackVisibilityPolicy.ShouldHide(true, "Bad Romance", true));
            Assert.True(PlaybackVisibilityPolicy.ShouldHide(true, "Bad Romance", false));
            Assert.True(PlaybackVisibilityPolicy.ShouldHide(false, "Bad Romance", true));
            Assert.True(PlaybackVisibilityPolicy.ShouldHide(true, "", true));
        }

        static void KeepsIslandVisibleDuringStartupHint()
        {
            Assert.False(PlaybackVisibilityPolicy.ShouldHide(false, "", false, true));
            Assert.True(PlaybackVisibilityPolicy.ShouldHide(false, "", false, false));
        }

        static void KeepsPausedIslandAvailableDuringGracePeriod()
        {
            Assert.False(PlaybackVisibilityPolicy.ShouldHide(
                true, "Song", MediaPlaybackStatus.Paused, TimeSpan.FromSeconds(3), false, false));
        }

        static void HidesPausedIslandAfterGracePeriod()
        {
            Assert.True(PlaybackVisibilityPolicy.ShouldHide(
                true, "Song", MediaPlaybackStatus.Paused, TimeSpan.FromSeconds(6), false, false));
        }

        static void CalculatesTopOnlyOverlayPositions()
        {
            var screen = new OverlayScreenArea("main", 0, 0, 1920, 1080, 0, 0, 1920, 1040);
            var size = new OverlaySize(560, 60);

            var top = OverlayPositioner.GetVisiblePosition(new OverlayPlacement("main", OverlayDockEdge.Top, 0.5), screen, size);
            var oldBottomSetting = OverlayPositioner.GetVisiblePosition(new OverlayPlacement("main", OverlayDockEdge.Bottom, 0.5), screen, size);
            var oldLeftSetting = OverlayPositioner.GetVisiblePosition(new OverlayPlacement("main", OverlayDockEdge.Left, 0.25), screen, size);

            Assert.Equal(680.0, top.Left);
            Assert.Equal(0.0, top.Top);
            Assert.Equal(680.0, oldBottomSetting.Left);
            Assert.Equal(0.0, oldBottomSetting.Top);
            Assert.Equal(340.0, oldLeftSetting.Left);
            Assert.Equal(0.0, oldLeftSetting.Top);
        }

        static void CalculatesHiddenTopOnlyOverlayPositions()
        {
            var screen = new OverlayScreenArea("main", 0, 0, 1920, 1080, 0, 0, 1920, 1040);
            var size = new OverlaySize(560, 60);

            Assert.Equal(-68.0, OverlayPositioner.GetHiddenPosition(new OverlayPlacement("main", OverlayDockEdge.Top, 0.5), screen, size).Top);
            Assert.Equal(-68.0, OverlayPositioner.GetHiddenPosition(new OverlayPlacement("main", OverlayDockEdge.Bottom, 0.5), screen, size).Top);
            Assert.Equal(-68.0, OverlayPositioner.GetHiddenPosition(new OverlayPlacement("main", OverlayDockEdge.Right, 0.5), screen, size).Top);
        }

        static void SnapsDraggedOverlayToTopEdge()
        {
            var screens = new[]
            {
                new OverlayScreenArea("left", -1280, 0, 1280, 720, -1280, 0, 1280, 680),
                new OverlayScreenArea("main", 0, 0, 1920, 1080, 0, 0, 1920, 1040)
            };
            var size = new OverlaySize(560, 60);

            var upper = OverlayPositioner.SnapToNearestEdge(-1260, 120, size, screens);
            var lower = OverlayPositioner.SnapToNearestEdge(-1260, 520, size, screens);

            Assert.Equal("left", upper.ScreenName);
            Assert.Equal(OverlayDockEdge.Top, upper.Edge);
            Assert.Equal("left", lower.ScreenName);
            Assert.Equal(OverlayDockEdge.Top, lower.Edge);
        }

        static void KeepsPointerDragLockedToTopEdge()
        {
            var screens = new[]
            {
                new OverlayScreenArea("left", -1280, 0, 1280, 720, -1280, 0, 1280, 680),
                new OverlayScreenArea("main", 0, 0, 1920, 1080, 0, 0, 1920, 1040)
            };
            var size = new OverlaySize(560, 60);

            var mainDrag = OverlayPositioner.GetHorizontalDragPlacement(960, 900, size, screens);
            var mainPosition = OverlayPositioner.GetVisiblePosition(mainDrag, screens[1], size);
            var leftDrag = OverlayPositioner.GetHorizontalDragPlacement(-640, 520, size, screens);
            var leftPosition = OverlayPositioner.GetVisiblePosition(leftDrag, screens[0], size);

            Assert.Equal("main", mainDrag.ScreenName);
            Assert.Equal(OverlayDockEdge.Top, mainDrag.Edge);
            Assert.Equal(680.0, mainPosition.Left);
            Assert.Equal(0.0, mainPosition.Top);
            Assert.Equal("left", leftDrag.ScreenName);
            Assert.Equal(OverlayDockEdge.Top, leftDrag.Edge);
            Assert.Equal(-920.0, leftPosition.Left);
            Assert.Equal(0.0, leftPosition.Top);
        }

        static void SelectsOverlayShapePathForDockEdge()
        {
            Assert.True(OverlayShapePath.GetPath(OverlayDockEdge.Top).StartsWith("M 0,0 L 560,0"));
            Assert.Equal(OverlayShapePath.GetPath(OverlayDockEdge.Top), OverlayShapePath.GetPath(OverlayDockEdge.Bottom));
            Assert.Equal(OverlayShapePath.GetPath(OverlayDockEdge.Top), OverlayShapePath.GetPath(OverlayDockEdge.Left));
            Assert.Equal(OverlayShapePath.GetPath(OverlayDockEdge.Top), OverlayShapePath.GetPath(OverlayDockEdge.Right));
        }

        static void MouseAvoidanceSettingsPanelHasEnoughLayoutRows()
        {
            var xaml = File.ReadAllText(Path.Combine(GetSolutionRoot(), "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var panelStart = xaml.IndexOf("x:Name=\"HoverSettingsPanel\"", StringComparison.Ordinal);
            var columnsStart = xaml.IndexOf("<Grid.ColumnDefinitions>", panelStart, StringComparison.Ordinal);
            var rowDefinitions = xaml.Substring(panelStart, columnsStart - panelStart);
            var rowCount = CountOccurrences(rowDefinitions, "<RowDefinition");

            Assert.True(rowCount >= 11);
            Assert.True(xaml.Contains("x:Name=\"HoverDetectionRangeSlider\""));
            Assert.True(xaml.Contains("Minimum=\"60\""));
        }

        static void MouseAvoidanceSettingsExposesHoverAspectRatioPreview()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "OverlayPlacementSettings.cs"));
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"HoverAuraAspectRatioSlider\""));
            Assert.True(xaml.Contains("x:Name=\"HoverShapePreviewEllipse\""));
            Assert.True(xaml.Contains("x:Name=\"HoverPreviewIsland\""));
            Assert.True(xaml.Contains("x:Name=\"HoverPreviewAura\""));
            Assert.True(xaml.Contains("歌词光影预览"));
            Assert.False(xaml.Contains("Microsoft 合作伙伴中心"));
            Assert.False(xaml.Contains("Package validation"));
            Assert.True(settingsSource.Contains("HoverAuraAspectRatio"));
            Assert.False(mainWindowSource.Contains("HoverAuraSize * 1.5"));
            Assert.False(mainWindowSource.Contains("HoverAuraSize * 0.73"));
        }

        static void MouseAvoidanceSettingsExposesClickThroughOption()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "OverlayPlacementSettings.cs"));
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"PassThroughOnHoverCheckBox\""));
            Assert.True(settingsSource.Contains("PassThroughOnHover"));
            Assert.True(mainWindowSource.Contains("WM_NCHITTEST"));
            Assert.True(mainWindowSource.Contains("HTTRANSPARENT"));
        }

        static void IgnoresSmallBackwardTimelineJitterWhilePlaying()
        {
            var clock = new FakeMonotonicClock();
            var coordinator = new TimelineCoordinator(clock);
            coordinator.Update(TimeSpan.FromSeconds(20), true, MediaPlaybackStatus.Playing);
            clock.Advance(TimeSpan.FromSeconds(1));

            var jittered = coordinator.Update(
                TimeSpan.FromMilliseconds(20700),
                true,
                MediaPlaybackStatus.Playing);

            Assert.Equal(TimeSpan.FromSeconds(21), jittered.Position);

            var seeked = coordinator.Update(
                TimeSpan.FromSeconds(10),
                true,
                MediaPlaybackStatus.Playing);
            Assert.Equal(TimeSpan.FromSeconds(10), seeked.Position);
        }

        static void MouseAvoidanceSettingsFitWithoutScrolling()
        {
            var xaml = File.ReadAllText(Path.Combine(GetSolutionRoot(), "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var panelStart = xaml.IndexOf("x:Name=\"HoverSettingsPanel\"", StringComparison.Ordinal);
            var panelEnd = xaml.IndexOf("x:Name=\"HotkeySettingsPanel\"", panelStart, StringComparison.Ordinal);
            var panel = xaml.Substring(panelStart, panelEnd - panelStart);

            Assert.True(xaml.Contains("<Grid x:Name=\"HoverSettingsPanel\""));
            Assert.False(panel.Contains("VerticalScrollBarVisibility"));
            Assert.True(panel.Contains("ClipToBounds=\"True\""));
            Assert.True(xaml.Contains("x:Name=\"HoverSettingsContent\""));
            Assert.Equal(8, CountOccurrences(panel, "<RowDefinition Height=\"*\" MinHeight=\"34\" />"));
            Assert.True(panel.Contains("<RowDefinition Height=\"*\" MinHeight=\"102\" />"));
            Assert.True(panel.Contains("<RowDefinition Height=\"*\" MinHeight=\"50\" />"));
            Assert.True(xaml.Contains("Height=\"96\""));
            Assert.True(xaml.Contains("Style=\"{StaticResource SettingLabel}\"\n                                       Text=\"实时预览\""));
        }

        static void MouseAvoidanceSettingsRestoresScreenshotDefaults()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var window = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var settings = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "OverlayPlacementSettings.cs"));

            Assert.True(xaml.Contains("x:Name=\"ResetHoverDefaultsButton\""));
            Assert.True(xaml.Contains("Click=\"ResetHoverDefaultsButton_Click\""));
            Assert.True(settings.Contains("DefaultHoverAuraSize = 86"));
            Assert.True(settings.Contains("DefaultHoverDetectionRange = 60"));
            Assert.True(settings.Contains("DefaultHoverAuraAspectRatio = 1.27"));
            Assert.True(settings.Contains("DefaultHoverSpectrumMidPosition = 56"));
            Assert.True(settings.Contains("DefaultHoverSpectrumCenterTransparency = 98"));
            Assert.True(settings.Contains("DefaultHoverSpectrumMidTransparency = 97"));
            Assert.True(settings.Contains("DefaultHoverSpectrumEdgeTransparency = 0"));
            Assert.True(window.Contains("HoverAuraSizeSlider.Value = OverlayPlacementSettings.DefaultHoverAuraSize"));
            Assert.True(window.Contains("SetHoverSpectrumControls(OverlayPlacementSettings.CreateDefaultHoverSpectrumStops())"));
            Assert.True(settings.Contains("DefaultPassThroughOnHover = true"));
            Assert.True(window.Contains("PassThroughOnHoverCheckBox.IsChecked = OverlayPlacementSettings.DefaultPassThroughOnHover"));
            Assert.True(window.Contains("QueueDirtyStateUpdate()"));
        }

        static void ClickThroughKeepsLeftDragAvailable()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.False(mainWindowSource.Contains("msg == WM_NCHITTEST && ShouldPassThroughMouseHit()"));
            Assert.True(mainWindowSource.Contains("BeginPotentialHorizontalDrag"));
            Assert.True(mainWindowSource.Contains("ForwardClickThroughToUnderlyingWindow"));
            Assert.True(mainWindowSource.Contains("DragStartThreshold"));
        }

        static void SettingsWindowExposesThemeModeSwitcher()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "OverlayPlacementSettings.cs"));
            var windowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"ThemeToggleRoot\""));
            Assert.True(xaml.Contains("ToolTip=\"浅色模式\""));
            Assert.True(xaml.Contains("ToolTip=\"深色模式\""));
            Assert.True(xaml.Contains("ToolTip=\"跟随系统\""));
            Assert.True(xaml.Contains("SettingsControlBackgroundBrush"));
            Assert.True(xaml.Contains("SettingsSelectedForegroundBrush"));
            Assert.True(settingsSource.Contains("SettingsThemePreference"));
            Assert.True(windowSource.Contains("ResolveDarkSettingsTheme"));
            Assert.True(windowSource.Contains("UpdateThemeResources"));
            Assert.True(windowSource.Contains("ColorAnimation"));
            Assert.True(windowSource.Contains("TimeSpan.FromMilliseconds(180)"));
            Assert.False(windowSource.Contains("foreach (var control in FindVisualChildren<Control>(root))"));
        }

        static void SettingsFirstOpenTextUsesThemeResources()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var windowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(xaml.Contains("<Setter Property=\"Foreground\" Value=\"{DynamicResource SettingsControlForegroundBrush}\" />"));
            Assert.True(xaml.Contains("<Setter Property=\"Foreground\" Value=\"{DynamicResource SettingsControlMutedForegroundBrush}\" />"));
            Assert.True(xaml.Contains("Foreground=\"{TemplateBinding Foreground}\""));
            Assert.False(xaml.Contains("<Setter Property=\"Foreground\" Value=\"#202124\" />"));
            Assert.False(xaml.Contains("<Setter Property=\"Foreground\" Value=\"#667085\" />"));
            Assert.False(xaml.Contains("<Setter Property=\"Foreground\" Value=\"#1F2937\" />"));
            Assert.True(windowSource.Contains("Loaded += (sender, args) =>"));
            Assert.True(windowSource.Contains("ApplySettingsTheme();"));
        }

        static void LineModeSegmentUsesThemeAwareColors()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var lineModeStart = xaml.IndexOf("x:Name=\"LineModeSegmentRoot\"", StringComparison.Ordinal);
            var lineModeEnd = xaml.IndexOf("x:Name=\"ShowTranslationCheckBox\"", lineModeStart, StringComparison.Ordinal);
            var lineModeBlock = xaml.Substring(lineModeStart, lineModeEnd - lineModeStart);

            Assert.True(xaml.Contains("x:Name=\"LineModeSegmentRoot\""));
            Assert.True(lineModeBlock.Contains("Background=\"{DynamicResource SettingsControlPressedBackgroundBrush}\""));
            Assert.False(lineModeBlock.Contains("Background=\"#EEF1F6\""));
            Assert.True(xaml.Contains("<Setter Property=\"Opacity\" Value=\"1\" />"));
        }

        static void DoesNotUsePlayerSpecificOcrFallbackWhenLyricsSourcesMiss()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.False(mainWindowSource.Contains("appleMusicOcrLyricsReader"));
            Assert.False(mainWindowSource.Contains("TryReadAppleMusicOcrFallbackAsync"));
        }

        static void ShowsTrayIconOnStartup()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var projectSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "LyricsIsland.App.csproj"));

            Assert.True(mainWindowSource.Contains("Forms.NotifyIcon"));
            Assert.True(mainWindowSource.Contains("InitializeTrayIcon();"));
            Assert.True(mainWindowSource.Contains("trayIcon.Visible = true"));
            Assert.True(mainWindowSource.Contains("ContextMenuStrip"));
            Assert.True(mainWindowSource.Contains("偏好设置"));
            Assert.True(mainWindowSource.Contains("退出"));
            Assert.True(mainWindowSource.Contains("OpenPlacementSettingsWindow"));
            Assert.True(mainWindowSource.Contains("DisposeTrayIcon"));
            Assert.True(mainWindowSource.Contains("Assets") && mainWindowSource.Contains("app.ico"));
            Assert.True(projectSource.Contains("Assets\\app.ico"));
            Assert.True(projectSource.Contains("CopyToOutputDirectory=\"Always\""));
        }

        static void SegmentedSettingsAnimateTheirSelectionThumbs()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"LineModeSelectionThumb\""));
            Assert.True(xaml.Contains("x:Name=\"LineModeSelectionTransform\""));
            Assert.True(xaml.Contains("x:Name=\"ThemeSelectionThumb\""));
            Assert.True(xaml.Contains("x:Name=\"ThemeSelectionTransform\""));
            Assert.True(source.Contains("AnimateSegmentSelection"));
            Assert.True(source.Contains("TimeSpan.FromMilliseconds(220)"));
            Assert.True(source.Contains("new CubicEase"));
        }

        static void IslandRevealAndRetractUseNonlinearFrameAnimation()
        {
            var source = File.ReadAllText(Path.Combine(GetSolutionRoot(), "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(source.Contains("positionAnimationFrameHandler"));
            Assert.True(source.Contains("StopPositionAnimationFrames"));
            Assert.True(source.Contains("Math.Pow(progress, 3)"));
            Assert.True(source.Contains("1 - Math.Pow(1 - progress, 4)"));
            Assert.True(source.Contains("positionAnimationFrameHandler == null"));
        }

        static void UserVisibleProductBrandingUsesLyricsIsland()
        {
            var root = GetSolutionRoot();
            var mainWindowXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml"));
            var settingsWindowXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var projectSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "LyricsIsland.App.csproj"));
            var coreProjectSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.Core", "LyricsIsland.Core.csproj"));
            var publishScript = File.ReadAllText(Path.Combine(root, "tools", "publish-next-version.ps1"));

            Assert.True(mainWindowXaml.Contains("Title=\"Lyric Island 歌词岛\""));
            Assert.True(settingsWindowXaml.Contains("Title=\"Lyric Island 歌词岛 - 偏好设置\""));
            Assert.True(mainWindowSource.Contains("Text = \"Lyric Island 歌词岛\""));
            Assert.True(projectSource.Contains("<AssemblyTitle>Lyric Island</AssemblyTitle>"));
            Assert.True(projectSource.Contains("<Product>Lyric Island 歌词岛</Product>"));
            Assert.True(projectSource.Contains("<AssemblyName>LyricsIsland.App</AssemblyName>"));
            Assert.True(projectSource.Contains("<PackageId>LyricsIsland.App</PackageId>"));
            Assert.True(coreProjectSource.Contains("<AssemblyName>LyricsIsland.Core</AssemblyName>"));
            Assert.True(coreProjectSource.Contains("<PackageId>LyricsIsland.Core</PackageId>"));
            Assert.True(publishScript.Contains("LyricsIsland.App.exe"));
            Assert.False(publishScript.Contains(string.Concat("AppleMusic", "DesktopLyrics", ".App.exe")));
        }

        static void SystemThemeFollowsWindowsChangesLive()
        {
            var source = File.ReadAllText(Path.Combine(GetSolutionRoot(), "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(source.Contains("SystemEvents.UserPreferenceChanged +="));
            Assert.True(source.Contains("SystemEvents.UserPreferenceChanged -="));
            Assert.True(source.Contains("selectedThemePreference == SettingsThemePreference.System"));
        }

        static void CacheSettingsExplainsCapacityAndCleanup()
        {
            var xaml = File.ReadAllText(Path.Combine(GetSolutionRoot(), "LyricsIsland.App", "PlacementSettingsWindow.xaml"));

            Assert.True(xaml.Contains("缓存用于保存已经下载过的同步歌词，下次播放同一首歌时可以直接读取，减少等待和重复请求"));
            Assert.True(xaml.Contains("按每首约10KB计算:实际数量会随歌词长度变化"));
            Assert.True(xaml.Contains("1 MB≈100首"));
            Assert.True(xaml.Contains("500 MB≈50,000首"));
            Assert.True(xaml.Contains("1000 MB≈100,000首"));
            Assert.True(xaml.Contains("写入歌词或修改容量后会检查总大小"));
            Assert.True(xaml.Contains("超过上限时优先删除最久未使用的歌词，直到低于容量上限"));
            Assert.False(xaml.Contains("用途："));
            Assert.False(xaml.Contains("容量估算："));
            Assert.False(xaml.Contains("自动清理："));
        }

        static void SettingsLayoutExposesRequestedStreamlinedControls()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var playback = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "PlaybackControlsModuleView.xaml"));
            var layoutStart = xaml.IndexOf("x:Name=\"LayoutSettingsPanel\"", StringComparison.Ordinal);
            var layoutEnd = xaml.IndexOf("x:Name=\"SupportSettingsPanel\"", layoutStart, StringComparison.Ordinal);
            var layoutPanel = xaml.Substring(layoutStart, layoutEnd - layoutStart);

            Assert.True(xaml.Contains("Grid.RowSpan=\"2\""));
            Assert.True(xaml.Contains("Click=\"CenterIslandButton_Click\""));
            Assert.True(source.Contains("OffsetSlider.Value = 50"));
            Assert.False(xaml.Contains("Text=\"上方\""));
            Assert.True(xaml.Contains("<RowDefinition Height=\"52\" />"));
            Assert.True(xaml.Contains("TextAlignment=\"Center\""));
            Assert.True(xaml.Contains("<Setter Property=\"FontSize\" Value=\"14\" />"));
            Assert.False(xaml.Contains("Content=\"保存布局\""));
            Assert.False(xaml.Contains("Content=\"删除分割线\""));
            Assert.False(xaml.Contains("Content=\"取消编辑\""));
            Assert.False(playback.Contains("TranslateTransform Y=\"-2\""));
            Assert.True(playback.Contains("Data=\"M 1,0 L 16,9 L 1,18 Z\""));
            Assert.True(xaml.Contains("x:Key=\"CompoundSettingLabel\""));
            Assert.True(layoutPanel.Contains("ClipToBounds=\"True\""));
            Assert.True(layoutPanel.Contains("<RowDefinition Height=\"2.2*\" MinHeight=\"118\" />"));
            Assert.True(layoutPanel.Contains("<RowDefinition Height=\"1.05*\" MinHeight=\"54\" />"));
            Assert.True(layoutPanel.Contains("<RowDefinition Height=\"1.35*\" MinHeight=\"112\" />"));
            Assert.True(layoutPanel.Contains("<RowDefinition Height=\"0.75*\" MinHeight=\"34\" />"));
            foreach (var elementName in new[]
            {
                "LayoutModePreviewPanel",
                "LayoutPlayerRowContent",
                "ModuleToolboxDropZone",
                "LayoutLyricsWidthRow",
                "LayoutDividerStyleRow"
            })
            {
                var elementStart = layoutPanel.IndexOf("x:Name=\"" + elementName + "\"", StringComparison.Ordinal);
                var openingTagEnd = layoutPanel.IndexOf('>', elementStart);
                var openingTag = layoutPanel.Substring(elementStart, openingTagEnd - elementStart);
                Assert.True(openingTag.Contains("VerticalAlignment=\"Top\""));
            }
            Assert.True(layoutPanel.Contains("Text=\"编辑布局\" />") && layoutPanel.Contains("Margin=\"0,8,16,0\""));
            Assert.True(layoutPanel.Contains("Text=\"播放器\" />") && layoutPanel.Contains("Margin=\"0,7,16,0\""));
            Assert.True(layoutPanel.Contains("Text=\"自定义模块\" />") && layoutPanel.Contains("Margin=\"0,17,16,0\""));
            Assert.True(layoutPanel.Contains("Text=\"歌词宽度\" />") && layoutPanel.Contains("Margin=\"0,4,16,0\""));
            Assert.True(layoutPanel.Contains("Content=\"恢复默认\"") && layoutPanel.Contains("Margin=\"0,14,0,0\""));
            Assert.True(xaml.Contains("x:Name=\"DividerOpacityValueText\""));
            Assert.True(xaml.Contains("x:Name=\"DividerSpacingValueText\""));
            Assert.True(source.Contains("DividerOpacityValueText.Text ="));
            Assert.True(source.Contains("DividerSpacingValueText.Text ="));
            Assert.True(xaml.Contains("x:Name=\"ApplyButton\""));
            Assert.True(xaml.Contains("x:Name=\"SaveButton\""));
            Assert.True(source.Contains("SettingsDirtyStateTracker<OverlayPlacementSettings>"));
            Assert.True(source.Contains("TimeSpan.FromMilliseconds(220)"));
            Assert.True(source.Contains("setHoverTransparencySuppressed?.Invoke(true)"));
            Assert.False(source.Contains("setHoverTransparencySuppressed?.Invoke(section == \"Layout\")"));
            Assert.True(xaml.Contains("x:Name=\"HorizontalLayoutPreviewCard\""));
            Assert.True(xaml.Contains("x:Name=\"ExpandableLayoutPreviewCard\""));
            Assert.True(xaml.Contains("<Grid x:Name=\"LayoutSettingsPanel\""));
            Assert.True(xaml.Contains("Content=\"恢复默认\""));
            Assert.True(xaml.Contains("Topmost=\"True\""));
            Assert.True(xaml.Contains("<Border CornerRadius=\"15\""));
            Assert.True(xaml.Contains("所有模块像积木一样横向排列，始终完整显示。"));
            Assert.True(xaml.Contains("x:Name=\"ExpandablePreviewShortcutRun\""));
            Assert.True(source.Contains("TimeSpan.FromSeconds(3.2)"));
            Assert.True(source.Contains("UpdateLayoutModePreviewAnimation"));
            var catalog = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "LayoutEditing", "ModuleToolboxCatalog.cs"));
            Assert.True(catalog.Contains("IslandModuleType.Lyrics"));
            Assert.True(catalog.Contains("IslandModuleType.TrackInfo"));
            Assert.True(catalog.Contains("M2,14 L15,14"));
            Assert.True(catalog.Contains("M11,9 L17,9"));
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            Assert.True(main.Contains("settingsWindowHoverSuppressed || moduleDragActive"));
            Assert.True(main.Contains("SetSettingsWindowHoverSuppressed"));
            Assert.True(main.Contains("IsStartupHintActive() || settingsWindow != null"));
        }

        static void DividerSettingsUpdateLayoutModules()
        {
            var root = GetSolutionRoot();
            var settings = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var host = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));

            Assert.True(settings.Contains("ApplyDividerSettings"));
            Assert.True(settings.Contains("updateDividerSettings?.Invoke"));
            Assert.True(main.Contains("UpdateDividerSettings"));
            Assert.True(main.Contains("RemoveDividers"));
            Assert.True(host.Contains("module.DividerOpacity.ToString"));
            Assert.True(host.Contains("module.MarginBefore.ToString"));
        }

        static void HoverMaskRestoresFullOpacityOutsideAura()
        {
            var root = GetSolutionRoot();
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var settingsXaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(source.Contains("HoverMaskContentRadiusScale"));
            Assert.True(source.Contains("new GradientStop(Colors.White, 1.0)"));
            Assert.False(source.Contains("placementSettings.HoverSpectrumStops,\n                    16"));
            Assert.True(settingsXaml.Contains("Background=\"{DynamicResource SettingsTrackBackgroundBrush}\""));
            Assert.True(settingsXaml.Contains("Color=\"#FF1677FF\""));
            Assert.True(settingsSource.Contains("SpectrumEdgePreviewStop.Color = Color.FromArgb(GetPreviewAlpha(SpectrumEdgeTransparencySlider.Value), 22, 119, 255)"));
        }

        static void MainWindowKeepsStartupHintWithoutMediaSession()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var branchStart = mainWindowSource.IndexOf("if (selected == null)", StringComparison.Ordinal);
            var selectedNullBranch = mainWindowSource.Substring(branchStart, 520);

            Assert.True(selectedNullBranch.Contains("IsStartupHintActive()"));
            Assert.True(selectedNullBranch.Contains("ShowIsland();"));
        }

        static void StartupHintBeginsAutoRetractCountdownImmediately()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var hintStart = mainWindowSource.IndexOf("public void ShowWaitingForPlaybackHint()", StringComparison.Ordinal);
            var hintEnd = mainWindowSource.IndexOf("private async Task RefreshAsync()", hintStart, StringComparison.Ordinal);
            var hintMethod = mainWindowSource.Substring(hintStart, hintEnd - hintStart);

            Assert.False(mainWindowSource.Contains("startupHintAwaitingConfirmation"));
            Assert.False(mainWindowSource.Contains("ConfirmStartupHint"));
            Assert.False(hintMethod.Contains("点击歌词岛或按任意键确认"));
            Assert.True(hintMethod.Contains("\"暂无播放内容\""));
            Assert.False(hintMethod.Contains("歌词岛已启动，等待播放内容"));
            Assert.True(hintMethod.Contains("startupHintTimer.Start();"));
            Assert.True(hintMethod.Contains("if (autoRetractSeconds > 0)"));
            Assert.True(hintMethod.Contains("\"歌词岛将在 \" + autoRetractSeconds + \" 秒后自动收起\""));
        }

        static void NativeSmtcServiceKeepsPersistentSessionSubscriptions()
        {
            var root = GetSolutionRoot();
            var projectPath = Path.Combine(root, "LyricsIsland.App", "LyricsIsland.App.csproj");
            var servicePath = Path.Combine(root, "LyricsIsland.App", "Media", "SmTcMediaSessionService.cs");

            Assert.True(File.Exists(servicePath));
            var projectSource = File.ReadAllText(projectPath);
            var serviceSource = File.ReadAllText(servicePath);

            Assert.True(projectSource.Contains("Microsoft.Windows.SDK.Contracts"));
            Assert.True(projectSource.Contains("10.0.19041.1"));
            Assert.True(serviceSource.Contains("GlobalSystemMediaTransportControlsSessionManager"));
            Assert.True(serviceSource.Contains("AttachSession(added)"));
            Assert.True(serviceSource.Contains("DetachSessions()"));
            Assert.True(serviceSource.Contains("DetachSession"));
            Assert.True(serviceSource.Contains("Session_Changed"));
            Assert.True(serviceSource.Contains("Session_TimelineChanged"));
            Assert.True(serviceSource.Contains("ScheduleRefreshAsync"));
            Assert.True(serviceSource.Contains("timeline.MaxSeekTime"));
            Assert.True(serviceSource.Contains("TimelineMetadataResolver.ResolveDuration"));
            Assert.True(serviceSource.Contains("artworkBySessionId"));
            Assert.True(serviceSource.Contains("Dispatcher.CurrentDispatcher"));
            Assert.True(serviceSource.Contains("RunOnOwnerThreadAsync"));
            Assert.True(serviceSource.Contains("ownerDispatcher.InvokeAsync(action).Task.Unwrap()"));
            Assert.False(serviceSource.Contains("ConfigureAwait(false)"));
            var refreshStart = serviceSource.IndexOf("private async Task RefreshCoreAsync()", StringComparison.Ordinal);
            var playbackStatusStart = serviceSource.IndexOf("public MediaPlaybackStatus?", StringComparison.Ordinal);
            var refreshBody = serviceSource.Substring(refreshStart, playbackStatusStart - refreshStart);
            Assert.False(refreshBody.Contains("DetachSessions();"));

            var mainWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            Assert.True(mainWindowSource.Contains("ExecuteTrackChangeCommandAsync"));
            var progressSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "ProgressModuleView.xaml.cs"));
            Assert.False(progressSource.Contains("≈"));
        }

        static void NativePlaybackRejectsStaleLyricsAndRemovesPowerShellBridge()
        {
            var root = GetSolutionRoot();
            var mainWindowPath = Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs");
            var projectPath = Path.Combine(root, "LyricsIsland.App", "LyricsIsland.App.csproj");
            var mainWindowSource = File.ReadAllText(mainWindowPath);
            var projectSource = File.ReadAllText(projectPath);

            Assert.True(mainWindowSource.Contains("SessionSelectionPolicy.Select"));
            Assert.True(mainWindowSource.Contains("lyricLoadGeneration"));
            Assert.True(mainWindowSource.Contains("generation == lyricLoadGeneration"));
            Assert.False(mainWindowSource.Contains("PowerShellNowPlayingProvider"));
            Assert.False(projectSource.Contains("now-playing.ps1"));
            Assert.False(File.Exists(Path.Combine(root, "scripts", "now-playing.ps1")));
        }

        static void TutorialWaitsForRequiredUserActions()
        {
            var tutorial = new TutorialFlowController();
            tutorial.Start();

            Assert.Equal(TutorialStep.AwaitingIslandClick, tutorial.Step);
            Assert.Equal(TutorialSettingsOpenPurpose.None, tutorial.SettingsOpened());
            Assert.True(tutorial.ContinueFromIslandClick());
            Assert.Equal(TutorialSettingsOpenPurpose.FirstSettings, tutorial.SettingsOpened());
            Assert.True(tutorial.BeginControlClickPractice());
            Assert.True(tutorial.ControlClicked(true));
            Assert.True(tutorial.RequestCustomSettings());
            Assert.Equal(TutorialSettingsOpenPurpose.CustomModules, tutorial.SettingsOpened());
            Assert.True(tutorial.LayoutPageSelected());
            Assert.True(tutorial.CompleteCustomization());
            Assert.Equal(TutorialStep.ShowingLayouts, tutorial.Step);
        }

        static void TutorialRejectsControlClickWithoutTemporaryInteraction()
        {
            var tutorial = new TutorialFlowController();
            tutorial.Start();
            tutorial.ContinueFromIslandClick();
            tutorial.SettingsOpened();
            tutorial.BeginControlClickPractice();

            Assert.False(tutorial.ControlClicked(false));
            Assert.Equal(TutorialStep.AwaitingControlClick, tutorial.Step);
            Assert.True(tutorial.ControlClicked(true));
        }

        static void FirstLaunchTutorialIsPersistedAndCanBeReplayed()
        {
            var root = GetSolutionRoot();
            var settingsSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "OverlayPlacementSettings.cs"));
            var mainSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var settingsWindowSource = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));

            Assert.True(settingsSource.Contains("HasSeenTutorial"));
            Assert.True(mainSource.Contains("var settingsFileExisted = System.IO.File.Exists(settingsPath)"));
            Assert.True(mainSource.Contains("shouldStartFirstRunTutorial = !settingsFileExisted"));
            Assert.True(mainSource.Contains("tutorialMaskWindow.FadeInAsync(TimeSpan.FromMilliseconds(500))"));
            Assert.True(mainSource.Contains("即将开始教学模式"));
            Assert.False(mainSource.Contains("即将开始引导模式"));
            Assert.True(settingsWindowSource.Contains("重新开始教学"));
            Assert.True(settingsWindowSource.Contains("RestartTutorialAboutRow_Click"));
        }

        static void TutorialOverlayIsDimmerAndCannotCoverInteractions()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "TutorialMaskWindow.xaml"));
            var code = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "TutorialMaskWindow.xaml.cs"));

            Assert.True(xaml.Contains("#70F4F5F7"));
            Assert.True(xaml.Contains("IsHitTestVisible=\"False\""));
            Assert.True(code.Contains("WsExTransparent"));
            Assert.True(code.Contains("WsExNoActivate"));
        }

        static void LayoutRebuildReplaysLatestIslandContent()
        {
            var root = GetSolutionRoot();
            var source = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));

            Assert.True(source.Contains("private IslandRenderState lastRenderState"));
            Assert.True(source.Contains("Update(lastRenderState);"));
            Assert.True(source.Contains("lastRenderState = state;"));
        }

        static void EscapeExitsTutorialFromIslandAndSettings()
        {
            var root = GetSolutionRoot();
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var settings = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(main.Contains("e.Key == Key.Escape && TryExitTutorial()"));
            Assert.True(settings.Contains("e.Key == Key.Escape && tryExitTutorial?.Invoke() == true"));
            Assert.True(main.Contains("private Task tutorialStopTask = Task.CompletedTask"));
            Assert.True(main.Contains("RegisterTutorialEscapeHotkey"));
            Assert.True(main.Contains("hotkeyService?.Unregister(TutorialEscapeHotkeyId)"));
        }

        static void LyricTransitionKeepsCenteredCanvasPosition()
        {
            var root = GetSolutionRoot();
            var code = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "LyricsModuleView.xaml.cs"));
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "LyricsModuleView.xaml"));
            var stopStart = code.IndexOf("private void StopMarquee()", StringComparison.Ordinal);
            var stopBody = code.Substring(stopStart);

            Assert.False(stopBody.Contains("Canvas.SetLeft(PrimaryLyricLinePanel, 0)"));
            Assert.False(stopBody.Contains("Canvas.SetLeft(SecondaryLyricText, 0)"));
            Assert.True(xaml.Contains("FontFamily=\"FZShuTi, Microsoft YaHei UI\""));
        }

        static void TutorialHoverWaitsBeforeEnablingAvoidance()
        {
            var root = GetSolutionRoot();
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var prompt = main.IndexOf("SetTutorialText(\"接下来演示鼠标避让\"", StringComparison.Ordinal);
            var delay = main.IndexOf("DelayTutorialAsync(1000", prompt, StringComparison.Ordinal);
            var enable = main.IndexOf("tutorialHoverSuppressed = false", prompt, StringComparison.Ordinal);

            Assert.True(prompt >= 0 && delay > prompt && enable > delay);
        }

        static void TutorialNextRemainsVisibleAndKeepsSettingsOpen()
        {
            var root = GetSolutionRoot();
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var action = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "TutorialActionWindow.cs"));
            var methodStart = main.IndexOf("private async Task CompleteTutorialCustomizationAsync()", StringComparison.Ordinal);
            var methodEnd = main.IndexOf("private async Task RunTutorialLayoutDemoAsync", methodStart, StringComparison.Ordinal);
            var method = main.Substring(methodStart, methodEnd - methodStart);

            Assert.False(method.Contains("FadeOutSettingsWindowAsync"));
            Assert.True(method.Contains("ApplyPendingChangesForTutorial"));
            Assert.True(action.Contains("Width = emphasized ? 176 : 142"));
            Assert.True(action.Contains("Opacity = emphasized ? 0.72 : 0.22"));
        }

        static void TutorialNextUsesUnclippedRoundedPulse()
        {
            var action = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "TutorialActionWindow.cs"));
            var main = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(action.Contains("const double ActionPadding"));
            Assert.True(action.Contains("button.Margin = new Thickness(ActionPadding)"));
            Assert.True(action.Contains("BorderThickness = new Thickness(0)"));
            Assert.True(action.Contains("FontSize = emphasized ? 18 : 15"));
            Assert.True(action.Contains("public Task PulseInAsync"));
            Assert.True(action.Contains("new LinearDoubleKeyFrame(0.32"));
            Assert.True(main.Contains("tutorialNextWindow.PulseInAsync(TimeSpan.FromMilliseconds(820))"));
        }

        static void TutorialActionButtonsFadeWithMask()
        {
            var root = GetSolutionRoot();
            var action = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "TutorialActionWindow.cs"));
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(action.Contains("public Task FadeOutAsync(TimeSpan duration)"));
            Assert.True(action.Contains("new DoubleAnimation(Opacity, 0, duration)"));
            Assert.True(main.Contains("await Task.WhenAll("));
            Assert.True(main.Contains("tutorialMaskWindow?.FadeOutAsync(fadeDuration)"));
            Assert.True(main.Contains("tutorialExitWindow?.FadeOutAsync(fadeDuration)"));
            Assert.True(main.Contains("tutorialNextWindow?.FadeOutAsync(fadeDuration)"));
        }

        static void TutorialHighlightsLayoutEditingSettingBackground()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settings = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"LayoutEditTutorialHighlight\""));
            Assert.True(xaml.Contains("CornerRadius=\"16\""));
            Assert.True(xaml.Contains("IsHitTestVisible=\"False\""));
            Assert.True(settings.Contains("public void PulseLayoutEditSettingsHighlight()"));
            Assert.True(main.Contains("settingsWindow?.PulseLayoutEditSettingsHighlight()"));
        }

        static void TutorialModuleTransitionsOverlapAndKeepClickExpandLayoutUnchanged()
        {
            var root = GetSolutionRoot();
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var host = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));
            var demoStart = main.IndexOf("private async Task RunTutorialLayoutDemoAsync", StringComparison.Ordinal);
            var demoEnd = main.IndexOf("private Task StopTutorialAsync", demoStart, StringComparison.Ordinal);
            var demo = main.Substring(demoStart, demoEnd - demoStart);

            Assert.True(host.Contains("AnimateModulesInAsync"));
            Assert.True(host.Contains("AnimateModulesOutAsync"));
            Assert.True(host.Contains("BeginTime = TimeSpan.FromMilliseconds(staggerMilliseconds * index)"));
            Assert.True(main.Contains("new[] { tutorialDividerModuleId, tutorialControlsModuleId }"));
            Assert.True(main.Contains("new[] { tutorialControlsModuleId, tutorialDividerModuleId }"));
            Assert.False(demo.Contains("layouts.CompactCollapsed"));
            Assert.False(demo.Contains("layouts.CompactExpanded"));
            Assert.False(demo.Contains("IslandInteractionState.Collapsed"));
        }

        static void SettingsAndTutorialExplainExpandableHotkey()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml"));
            var settings = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.False(xaml.Contains("x:Name=\"ExpandableInteractionHintText\""));
            Assert.False(xaml.Contains("按住 Ctrl 并单击歌词岛才会展开或收起"));
            Assert.True(xaml.Contains("x:Name=\"ExpandablePreviewShortcutRun\""));
            Assert.True(xaml.Contains("FontWeight=\"Bold\""));
            Assert.True(xaml.Contains("Foreground=\"#FF1677FF\""));
            Assert.True(settings.Contains("UpdateExpandableInteractionHint"));
            Assert.True(settings.Contains("ExpandablePreviewShortcutRun.Text = gesture"));
            Assert.True(xaml.Contains("按住 "));
            Assert.True(xaml.Contains("即展开，松开后自动折叠"));
            Assert.True(main.Contains("按住 \" + GetTemporaryInteractionGesture() + \" 即时展开"));
            Assert.False(main.Contains("并单击歌词岛展开"));
        }

        static void AllMeasuredIslandWidthChangesAnimate()
        {
            var main = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(main.Contains("var sizeChanged ="));
            Assert.True(main.Contains("if (IsLoaded && sizeChanged)"));
            Assert.True(main.Contains("var sizeAnimated = ApplyMeasuredIslandSize(animateSize)"));
        }

        static void ExpandedIslandMeasuresUnconstrainedModuleContent()
        {
            var root = GetSolutionRoot();
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var host = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "Modules", "IslandModuleHost.xaml.cs"));

            Assert.True(host.Contains("public Size MeasureContentSize()"));
            Assert.True(host.Contains("ModulePanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity))"));
            Assert.True(main.Contains("var contentSize = ModuleHost.MeasureContentSize();"));
            Assert.True(main.Contains("contentSize.Width + IslandHorizontalShapePadding"));
        }

        static void IslandSizeAnimationAvoidsPerFrameTransparentWindowResizing()
        {
            var root = GetSolutionRoot();
            var main = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml.cs"));
            var xaml = File.ReadAllText(Path.Combine(root, "LyricsIsland.App", "MainWindow.xaml"));
            var animateStart = main.IndexOf("private void AnimateIslandSize", StringComparison.Ordinal);
            var animateEnd = main.IndexOf("private void StopIslandSizeAnimationFrames", animateStart, StringComparison.Ordinal);
            var animate = main.Substring(animateStart, animateEnd - animateStart);
            var frameStart = animate.IndexOf("islandSizeAnimationFrameHandler =", StringComparison.Ordinal);
            var frameEnd = animate.IndexOf("CompositionTarget.Rendering +=", frameStart, StringComparison.Ordinal);
            var frame = animate.Substring(frameStart, frameEnd - frameStart);

            Assert.True(animate.Contains("Width = Math.Max(startWidth, targetWidth)"));
            Assert.True(animate.Contains("Height = Math.Max(startHeight, targetHeight)"));
            Assert.False(frame.Contains("Width = width"));
            Assert.False(frame.Contains("Height = height"));
            Assert.False(main.Contains("Geometry.Parse(IslandGeometryBuilder.BuildTopPath(width, height))"));
            Assert.True(main.Contains("new StreamGeometry()"));
            Assert.True(main.Contains("if (!islandSizeAnimationActive)"));
            Assert.True(xaml.Contains("HorizontalContentAlignment=\"Stretch\""));
            Assert.True(xaml.Contains("x:Name=\"IslandShellTranslate\""));
            Assert.True(xaml.Contains("ClipToBounds=\"True\""));
        }

        static void CoalescesRepeatedAnimationTargets()
        {
            var targets = new AnimationTargetTracker();

            Assert.True(targets.TrySet(120, -4));
            Assert.False(targets.TrySet(120, -4));
            Assert.True(targets.TrySet(260, -4));
            Assert.Equal(260.0, targets.Left);
            Assert.Equal(-4.0, targets.Top);

            targets.Clear();
            Assert.True(targets.TrySet(260, -4));
        }

        static void MainWindowStopsAllRuntimeActivityWhenClosed()
        {
            var main = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "MainWindow.xaml.cs"));

            Assert.True(main.Contains("StopRuntimeActivity();"));
            Assert.True(main.Contains("private void StopRuntimeActivity()"));
            Assert.True(main.Contains("hoverProximityTimer.Stop();"));
            Assert.True(main.Contains("startupHintTimer?.Stop();"));
            Assert.True(main.Contains("StopIslandSizeAnimationFrames();"));
        }

        static void AtomicallyReplacesSettingsFilesWithoutLeavingTemporaryFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "LyricsIsland.AtomicWrite." + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(root, "settings.json");
            try
            {
                AtomicFileWriter.WriteAllText(path, "first");
                Assert.Equal("first", File.ReadAllText(path));

                AtomicFileWriter.WriteAllText(path, "second");
                Assert.Equal("second", File.ReadAllText(path));
                Assert.Equal(0, Directory.GetFiles(root, "*.tmp").Length);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        static void TracksReferenceChangesWithoutTreatingEqualContentAsTheSameObject()
        {
            var tracker = new ReferenceChangeTracker<byte[]>();
            var first = new byte[] { 1, 2, 3 };
            var second = new byte[] { 1, 2, 3 };

            Assert.True(tracker.TryUpdate(first));
            Assert.False(tracker.TryUpdate(first));
            Assert.True(tracker.TryUpdate(second));
            Assert.True(tracker.TryUpdate(null));
            Assert.False(tracker.TryUpdate(null));
        }

        static void ModuleViewsSkipUnchangedRenderingWork()
        {
            var root = Path.Combine(GetSolutionRoot(), "LyricsIsland.App", "Modules");
            var artwork = File.ReadAllText(Path.Combine(root, "AlbumArtModuleView.xaml.cs"));
            var progress = File.ReadAllText(Path.Combine(root, "ProgressModuleView.xaml.cs"));
            var track = File.ReadAllText(Path.Combine(root, "TrackInfoModuleView.xaml.cs"));

            Assert.True(artwork.Contains("artworkChanges.TryUpdate(bytes)"));
            Assert.True(progress.Contains("lastPositionSecond"));
            Assert.True(track.Contains("lastTitle == title"));
        }

        static void CoalescesIdenticalHoverSamplesWithoutLosingChangedSamples()
        {
            var samples = new HoverSampleTracker();

            Assert.True(samples.TryUpdate(10, 20, 0.5));
            Assert.False(samples.TryUpdate(10, 20, 0.5));
            Assert.True(samples.TryUpdate(10, 20, 0.6));
            Assert.True(samples.TryUpdate(11, 20, 0.6));
            samples.Clear();
            Assert.True(samples.TryUpdate(11, 20, 0.6));
        }

        static void SettingsDirtyFingerprintAvoidsASecondJsonDeepClone()
        {
            var source = File.ReadAllText(Path.Combine(
                GetSolutionRoot(), "LyricsIsland.App", "PlacementSettingsWindow.xaml.cs"));
            var start = source.IndexOf("private static string CreateSettingsFingerprint", StringComparison.Ordinal);
            var end = source.IndexOf("private static void SetLayoutProfile", start, StringComparison.Ordinal);
            var method = source.Substring(start, end - start);

            Assert.False(method.Contains("DeepClone"));
            Assert.True(method.Contains("JsonSerializer.Serialize"));
        }

        static string GetSolutionRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "LyricsIsland.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("Could not find solution root.");
            }

            return directory.FullName;
        }

        static int CountOccurrences(string value, string pattern)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }

            return count;
        }
    }

    sealed class FakeMonotonicClock : IMonotonicClock
    {
        public TimeSpan Elapsed { get; private set; }
        public void Advance(TimeSpan value) { Elapsed += value; }
    }

    sealed class FakeLyricsClient : ILyricsClient
    {
        private readonly string lyrics;

        public FakeLyricsClient(string lyrics)
        {
            this.lyrics = lyrics;
        }

        public System.Threading.Tasks.Task<string> GetSyncedLyricsAsync(TrackIdentity track)
        {
            return System.Threading.Tasks.Task.FromResult(lyrics);
        }
    }

    sealed class ThrowingLyricsClient : ILyricsClient
    {
        public System.Threading.Tasks.Task<string> GetSyncedLyricsAsync(TrackIdentity track)
        {
            throw new HttpRequestException("source unavailable");
        }
    }

    sealed class TestSuite
    {
        public int ExitCode { get; private set; }

        public void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                ExitCode = 1;
                Console.WriteLine("FAIL " + name);
                Console.WriteLine(ex.Message);
            }
        }
    }

    static class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException("Expected <" + expected + "> but got <" + actual + ">.");
            }
        }

        public static void True(bool condition)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Expected condition to be true.");
            }
        }

        public static void False(bool condition)
        {
            if (condition)
            {
                throw new InvalidOperationException("Expected condition to be false.");
            }
        }
    }
}
