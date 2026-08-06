using System;

namespace LyricHover.Core
{
    public enum ProEntitlementKind
    {
        None,
        LegacyPro,
        StorePro
    }

    public static class ProEntitlementPolicy
    {
        public static readonly DateTimeOffset LegacyPurchaseCutoff =
            new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.FromHours(8));

        public static ProEntitlementKind Evaluate(
            bool storeProIsInUserCollection,
            bool appLicenseIsActive,
            bool appSkuIsInUserCollection,
            bool appSkuIsTrial,
            DateTimeOffset? appSkuAcquiredDate)
        {
            if (storeProIsInUserCollection)
            {
                return ProEntitlementKind.StorePro;
            }

            if (appLicenseIsActive &&
                appSkuIsInUserCollection &&
                !appSkuIsTrial &&
                appSkuAcquiredDate.HasValue &&
                appSkuAcquiredDate.Value < LegacyPurchaseCutoff)
            {
                return ProEntitlementKind.LegacyPro;
            }

            return ProEntitlementKind.None;
        }
    }
}
