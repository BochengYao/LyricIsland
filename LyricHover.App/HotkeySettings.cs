using System;
using System.Windows.Input;

namespace LyricHover.App
{
    public sealed class HotkeySettings
    {
        public string Earlier { get; set; }

        public string Later { get; set; }

        public string Reset { get; set; }

        public string TemporaryInteraction { get; set; }

        public static HotkeySettings CreateDefault()
        {
            return new HotkeySettings
            {
                Earlier = "Ctrl+Alt+Left",
                Later = "Ctrl+Alt+Right",
                Reset = "Ctrl+Alt+Down",
                TemporaryInteraction = "Ctrl"
            };
        }

        public void Normalize()
        {
            var defaults = CreateDefault();
            Earlier = string.IsNullOrWhiteSpace(Earlier) ? defaults.Earlier : Earlier.Trim();
            Later = string.IsNullOrWhiteSpace(Later) ? defaults.Later : Later.Trim();
            Reset = string.IsNullOrWhiteSpace(Reset) ? defaults.Reset : Reset.Trim();
            TemporaryInteraction = string.IsNullOrWhiteSpace(TemporaryInteraction)
                ? defaults.TemporaryInteraction
                : TemporaryInteraction.Trim();
        }
    }

    public static class HotkeyGestureParser
    {
        public static bool TryParseGlobal(string value, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;
            foreach (var token in Split(value))
            {
                if (TryAddModifier(token, ref modifiers))
                {
                    continue;
                }

                Key key;
                if (!TryParseKey(token, out key) || virtualKey != 0)
                {
                    return false;
                }

                virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            }

            return virtualKey != 0;
        }

        public static bool IsPressed(string value)
        {
            var tokens = Split(value);
            if (tokens.Length == 0)
            {
                return false;
            }

            foreach (var token in tokens)
            {
                if (IsModifierPressed(token))
                {
                    continue;
                }

                Key key;
                if (!TryParseKey(token, out key) || !Keyboard.IsKeyDown(key))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] Split(string value)
        {
            return (value ?? string.Empty).Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool TryAddModifier(string token, ref uint modifiers)
        {
            switch ((token ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "alt": modifiers |= 0x0001; return true;
                case "ctrl":
                case "control": modifiers |= 0x0002; return true;
                case "shift": modifiers |= 0x0004; return true;
                case "win":
                case "windows": modifiers |= 0x0008; return true;
                default: return false;
            }
        }

        private static bool IsModifierPressed(string token)
        {
            switch ((token ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "alt": return (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
                case "ctrl":
                case "control": return (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                case "shift": return (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                case "win":
                case "windows": return (Keyboard.Modifiers & ModifierKeys.Windows) != 0;
                default: return false;
            }
        }

        private static bool TryParseKey(string token, out Key key)
        {
            var normalized = (token ?? string.Empty).Trim();
            if (normalized.Length == 1 && char.IsDigit(normalized[0]))
            {
                normalized = "D" + normalized;
            }

            return Enum.TryParse(normalized, true, out key) && key != Key.None;
        }
    }
}
