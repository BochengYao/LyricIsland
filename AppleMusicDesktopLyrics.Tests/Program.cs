using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AppleMusicDesktopLyrics.Core;

namespace AppleMusicDesktopLyrics.Tests
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
            suite.Run("parses lyrics package without translation", ParsesLyricsPackageWithoutTranslation);
            suite.Run("detects whether lyrics package has translation", DetectsWhetherLyricsPackageHasTranslation);
            suite.Run("gets current lyric line duration", GetsCurrentLyricLineDuration);
            suite.Run("tracks lyric text changes for animation", TracksLyricTextChangesForAnimation);
            suite.Run("returns an empty line before the first lyric", ReturnsEmptyLineBeforeFirstLyric);
            suite.Run("builds stable cache paths from song identity", BuildsStableCachePathsFromSongIdentity);
            suite.Run("evicts least recently used song cache files to stay under size limit", EvictsLeastRecentlyUsedSongCacheFilesToStayUnderSizeLimit);
            suite.Run("uses lrc lib search as the primary lyrics lookup", UsesLrcLibSearchAsPrimaryLyricsLookup);
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
            suite.Run("allows only one named application instance", AllowsOnlyOneNamedApplicationInstance);
            suite.Run("signals the existing application instance", SignalsExistingApplicationInstance);
            suite.Run("keeps island visible while playing even without lyrics", KeepsIslandVisibleWhilePlayingEvenWithoutLyrics);
            suite.Run("keeps island visible during startup hint", KeepsIslandVisibleDuringStartupHint);
            suite.Run("calculates top-only overlay positions", CalculatesTopOnlyOverlayPositions);
            suite.Run("calculates hidden top-only overlay positions", CalculatesHiddenTopOnlyOverlayPositions);
            suite.Run("snaps dragged overlay to top edge", SnapsDraggedOverlayToTopEdge);
            suite.Run("keeps pointer drag locked to top edge", KeepsPointerDragLockedToTopEdge);
            suite.Run("selects overlay shape path for dock edge", SelectsOverlayShapePathForDockEdge);
            suite.Run("mouse avoidance settings panel has enough layout rows", MouseAvoidanceSettingsPanelHasEnoughLayoutRows);
            suite.Run("mouse avoidance settings exposes hover aspect ratio preview", MouseAvoidanceSettingsExposesHoverAspectRatioPreview);
            suite.Run("mouse avoidance settings exposes click through option", MouseAvoidanceSettingsExposesClickThroughOption);
            suite.Run("click through keeps left drag available", ClickThroughKeepsLeftDragAvailable);
            suite.Run("settings window exposes theme mode switcher", SettingsWindowExposesThemeModeSwitcher);
            suite.Run("settings first open text uses theme resources", SettingsFirstOpenTextUsesThemeResources);
            suite.Run("line mode segment uses theme-aware colors", LineModeSegmentUsesThemeAwareColors);
            suite.Run("uses apple music ocr fallback when lyrics sources miss", UsesAppleMusicOcrFallbackWhenLyricsSourcesMiss);
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

        static void EvictsLeastRecentlyUsedSongCacheFilesToStayUnderSizeLimit()
        {
            var root = Path.Combine(Path.GetTempPath(), "AppleMusicDesktopLyrics.Tests." + Guid.NewGuid().ToString("N"));
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

        static void AllowsOnlyOneNamedApplicationInstance()
        {
            var name = "AppleMusicDesktopLyrics.Tests." + Guid.NewGuid().ToString("N");
            using (var first = SingleInstanceGuard.TryAcquire(name))
            using (var second = SingleInstanceGuard.TryAcquire(name))
            {
                Assert.True(first.HasHandle);
                Assert.False(second.HasHandle);
            }
        }

        static void SignalsExistingApplicationInstance()
        {
            var name = "AppleMusicDesktopLyrics.Tests." + Guid.NewGuid().ToString("N");
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
            var xaml = File.ReadAllText(Path.Combine(GetSolutionRoot(), "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml"));
            var panelStart = xaml.IndexOf("x:Name=\"HoverSettingsPanel\"", StringComparison.Ordinal);
            var columnsStart = xaml.IndexOf("<Grid.ColumnDefinitions>", panelStart, StringComparison.Ordinal);
            var rowDefinitions = xaml.Substring(panelStart, columnsStart - panelStart);
            var rowCount = CountOccurrences(rowDefinitions, "<RowDefinition");

            Assert.True(rowCount >= 8);
            Assert.True(xaml.Contains("x:Name=\"HoverDetectionRangeSlider\""));
            Assert.True(xaml.Contains("Minimum=\"60\""));
        }

        static void MouseAvoidanceSettingsExposesHoverAspectRatioPreview()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "OverlayPlacementSettings.cs"));
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "MainWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"HoverAuraAspectRatioSlider\""));
            Assert.True(xaml.Contains("x:Name=\"HoverShapePreviewEllipse\""));
            Assert.True(xaml.Contains("x:Name=\"HoverPreviewIsland\""));
            Assert.True(xaml.Contains("x:Name=\"HoverPreviewAura\""));
            Assert.True(xaml.Contains("底层内容预览"));
            Assert.True(xaml.Contains("歌词光影预览"));
            Assert.True(settingsSource.Contains("HoverAuraAspectRatio"));
            Assert.False(mainWindowSource.Contains("HoverAuraSize * 1.5"));
            Assert.False(mainWindowSource.Contains("HoverAuraSize * 0.73"));
        }

        static void MouseAvoidanceSettingsExposesClickThroughOption()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "OverlayPlacementSettings.cs"));
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "MainWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"PassThroughOnHoverCheckBox\""));
            Assert.True(settingsSource.Contains("PassThroughOnHover"));
            Assert.True(mainWindowSource.Contains("WM_NCHITTEST"));
            Assert.True(mainWindowSource.Contains("HTTRANSPARENT"));
        }

        static void ClickThroughKeepsLeftDragAvailable()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "MainWindow.xaml.cs"));

            Assert.False(mainWindowSource.Contains("msg == WM_NCHITTEST && ShouldPassThroughMouseHit()"));
            Assert.True(mainWindowSource.Contains("BeginPotentialHorizontalDrag"));
            Assert.True(mainWindowSource.Contains("ForwardClickThroughToUnderlyingWindow"));
            Assert.True(mainWindowSource.Contains("DragStartThreshold"));
        }

        static void SettingsWindowExposesThemeModeSwitcher()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml"));
            var settingsSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "OverlayPlacementSettings.cs"));
            var windowSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml.cs"));

            Assert.True(xaml.Contains("x:Name=\"ThemeToggleRoot\""));
            Assert.True(xaml.Contains("ToolTip=\"浅色模式\""));
            Assert.True(xaml.Contains("ToolTip=\"深色模式\""));
            Assert.True(xaml.Contains("ToolTip=\"跟随系统\""));
            Assert.True(xaml.Contains("SettingsControlBackgroundBrush"));
            Assert.True(xaml.Contains("SettingsSelectedForegroundBrush"));
            Assert.True(settingsSource.Contains("SettingsThemePreference"));
            Assert.True(windowSource.Contains("ResolveDarkSettingsTheme"));
            Assert.True(windowSource.Contains("UpdateThemeResources"));
            Assert.False(windowSource.Contains("foreach (var control in FindVisualChildren<Control>(root))"));
        }

        static void SettingsFirstOpenTextUsesThemeResources()
        {
            var root = GetSolutionRoot();
            var xaml = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml"));
            var windowSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml.cs"));

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
            var xaml = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "PlacementSettingsWindow.xaml"));
            var lineModeStart = xaml.IndexOf("x:Name=\"LineModeSegmentRoot\"", StringComparison.Ordinal);
            var lineModeEnd = xaml.IndexOf("x:Name=\"ShowTranslationCheckBox\"", lineModeStart, StringComparison.Ordinal);
            var lineModeBlock = xaml.Substring(lineModeStart, lineModeEnd - lineModeStart);

            Assert.True(xaml.Contains("x:Name=\"LineModeSegmentRoot\""));
            Assert.True(lineModeBlock.Contains("Background=\"{DynamicResource SettingsControlPressedBackgroundBrush}\""));
            Assert.False(lineModeBlock.Contains("Background=\"#EEF1F6\""));
            Assert.True(xaml.Contains("<Setter Property=\"Opacity\" Value=\"1\" />"));
        }

        static void UsesAppleMusicOcrFallbackWhenLyricsSourcesMiss()
        {
            var root = GetSolutionRoot();
            var mainWindowSource = File.ReadAllText(Path.Combine(root, "AppleMusicDesktopLyrics.App", "MainWindow.xaml.cs"));
            var fallbackSourcePath = Path.Combine(root, "AppleMusicDesktopLyrics.App", "AppleMusicOcrLyricsReader.cs");

            Assert.True(File.Exists(fallbackSourcePath));
            var fallbackSource = File.ReadAllText(fallbackSourcePath);
            Assert.True(mainWindowSource.Contains("appleMusicOcrLyricsReader"));
            Assert.True(mainWindowSource.Contains("TryReadAppleMusicOcrFallbackAsync"));
            Assert.True(mainWindowSource.Contains("Apple Music 内置歌词识别"));
            Assert.True(fallbackSource.Contains("tesseract.exe"));
            Assert.True(fallbackSource.Contains("CopyFromScreen"));
            Assert.True(fallbackSource.Contains("BuildHighContrastWhiteTextImage"));
            Assert.True(fallbackSource.Contains("Codex") && fallbackSource.Contains("插件"));
        }

        static string GetSolutionRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AppleMusicDesktopLyrics.sln")))
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
