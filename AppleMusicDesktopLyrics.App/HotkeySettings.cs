namespace AppleMusicDesktopLyrics.App
{
    public sealed class HotkeySettings
    {
        public string Earlier { get; set; }

        public string Later { get; set; }

        public string Reset { get; set; }

        public static HotkeySettings CreateDefault()
        {
            return new HotkeySettings
            {
                Earlier = "Ctrl+Alt+Left",
                Later = "Ctrl+Alt+Right",
                Reset = "Ctrl+Alt+Down"
            };
        }
    }
}
