namespace LyricHover.Core
{
    public sealed class ProEntitlementPresentation
    {
        public const string ActiveDescription =
            "感谢你对LyricHover的支持。你将抢先体验所有新功能，支持者徽章已发放，点击右侧按钮即可查看。更多专属权益，正在陆续加入。";

        private ProEntitlementPresentation(
            string title,
            string description,
            string buttonText,
            bool useBadgeIcon)
        {
            Title = title;
            Description = description;
            ButtonText = buttonText;
            UseBadgeIcon = useBadgeIcon;
        }

        public string Title { get; }

        public string Description { get; }

        public string ButtonText { get; }

        public bool UseBadgeIcon { get; }

        public static ProEntitlementPresentation For(ProEntitlementKind kind)
        {
            switch (kind)
            {
                case ProEntitlementKind.LegacyPro:
                    return new ProEntitlementPresentation(
                        "已自动激活 Pro，感谢你曾经购买并支持 LYRIC HOVER。",
                        ActiveDescription,
                        "查看我的支持者徽章",
                        true);
                case ProEntitlementKind.StorePro:
                    return new ProEntitlementPresentation(
                        "Pro 支持计划：已加入",
                        ActiveDescription,
                        "查看我的支持者徽章",
                        true);
                default:
                    return new ProEntitlementPresentation(
                        "Pro 支持计划",
                        "通过 Microsoft Store 升级 Pro，支持LyricHover持续开发，并解锁更多专属权益。",
                        "升级 Pro · ¥7",
                        false);
            }
        }
    }
}
