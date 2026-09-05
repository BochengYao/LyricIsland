namespace LyricHover.App
{
    public enum LyricsSourcePreference
    {
        Automatic = 0,
        LrcLib = 1,
        QQMusic = 2,
        // Value 3 belonged to the retired KuGou lyrics source. Do not reuse it,
        // so persisted numeric preferences cannot silently select another source.
        NetEase = 4
    }
}
