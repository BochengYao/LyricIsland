using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LyricHover.Core;
using Windows.Services.Store;

namespace LyricHover.App
{
    internal sealed class StoreProEntitlementService
    {
        private readonly ProEntitlementResolver resolver;

        public StoreProEntitlementService(string localApplicationDataRoot)
        {
            var productDataRoot = ProductDataDirectory.Prepare(localApplicationDataRoot);
            resolver = new ProEntitlementResolver(
                new ProEntitlementCache(Path.Combine(productDataRoot, "pro-entitlement.json")));
        }

        public Task<ProEntitlementResult> RefreshAsync()
        {
            return resolver.ResolveAsync(QueryStoreAsync);
        }

        private static async Task<ProEntitlementEvidence> QueryStoreAsync()
        {
            var storeContext = StoreContext.GetDefault();
            var appLicense = await storeContext.GetAppLicenseAsync();
            var currentAppResult = await storeContext.GetStoreProductForCurrentAppAsync();
            var associatedProducts = await storeContext.GetAssociatedStoreProductsAsync(new[] { "Durable" });

            if (appLicense == null ||
                currentAppResult == null ||
                currentAppResult.ExtendedError != null ||
                currentAppResult.Product == null ||
                associatedProducts == null ||
                associatedProducts.ExtendedError != null)
            {
                throw new InvalidOperationException("Microsoft Store entitlement query failed.");
            }

            var proProduct = associatedProducts.Products.Values.FirstOrDefault(product =>
                string.Equals(
                    product.InAppOfferToken,
                    PlacementSettingsWindow.MicrosoftStoreProProductId,
                    StringComparison.OrdinalIgnoreCase));
            var storeProIsInUserCollection = proProduct?.IsInUserCollection == true;
            var proSkuAcquiredDate = proProduct?.Skus
                .Where(sku => sku.CollectionData != null && !sku.CollectionData.IsTrial)
                .OrderBy(sku => sku.CollectionData.AcquiredDate)
                .Select(sku => (DateTimeOffset?)sku.CollectionData.AcquiredDate)
                .FirstOrDefault();

            var entitledSkus = currentAppResult.Product.Skus
                .Where(sku => sku.CollectionData != null)
                .OrderBy(sku => sku.CollectionData.AcquiredDate)
                .ToList();
            var entitledSku = entitledSkus.FirstOrDefault(sku => !sku.CollectionData.IsTrial)
                ?? entitledSkus.FirstOrDefault();
            var appSkuIsInUserCollection =
                currentAppResult.Product.IsInUserCollection &&
                entitledSku != null;
            var appSkuIsTrial =
                appLicense.IsTrial ||
                entitledSku?.CollectionData?.IsTrial == true;
            DateTimeOffset? appSkuAcquiredDate = entitledSku?.CollectionData?.AcquiredDate;

            var kind = ProEntitlementPolicy.Evaluate(
                storeProIsInUserCollection,
                appLicense.IsActive,
                appSkuIsInUserCollection,
                appSkuIsTrial,
                appSkuAcquiredDate);
            var acquiredAtUtc = kind == ProEntitlementKind.StorePro
                ? proSkuAcquiredDate
                : kind == ProEntitlementKind.LegacyPro
                    ? appSkuAcquiredDate
                    : null;
            return new ProEntitlementEvidence(kind, acquiredAtUtc);
        }
    }
}
