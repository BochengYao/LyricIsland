using System;

namespace LyricHover.App.LyricDock
{
    public enum WidgetsElementMatchKind { None, LocalizedName, ClassName, AutomationId }

    public static class WidgetsElementMatcher
    {
        public static WidgetsElementMatchKind GetMatchKind(string automationId, string className, string name)
        {
            // Stable UIA identity wins. Localized names are intentionally a last-resort fallback.
            if (ContainsWidgetsToken(automationId)) return WidgetsElementMatchKind.AutomationId;
            if (ContainsWidgetsToken(className)) return WidgetsElementMatchKind.ClassName;
            name = name ?? string.Empty;
            return name.IndexOf("widgets", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("小组件", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("小工具", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("ウィジェット", StringComparison.OrdinalIgnoreCase) >= 0
                ? WidgetsElementMatchKind.LocalizedName
                : WidgetsElementMatchKind.None;
        }

        public static bool IsMatch(string automationId, string className, string name) => GetMatchKind(automationId, className, name) != WidgetsElementMatchKind.None;
        public static bool HasStableWidgetsIdentity(string automationId, string className) =>
            ContainsWidgetsToken(automationId) || ContainsWidgetsToken(className);

        private static bool ContainsWidgetsToken(string value) => (value ?? string.Empty).IndexOf("widget", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

