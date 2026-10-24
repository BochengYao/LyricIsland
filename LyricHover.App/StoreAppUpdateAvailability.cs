using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Services.Store;

namespace LyricHover.App
{
    internal static class StoreAppUpdateAvailability
    {
        public static async Task<Version> GetLatestAvailableVersionAsync()
        {
            try
            {
                var currentPackageName = Package.Current.Id.Name;
                var updates = await StoreContext.GetDefault().GetAppAndOptionalStorePackageUpdatesAsync();
                return updates
                    .Where(update => update?.Package?.Id != null &&
                        string.Equals(update.Package.Id.Name, currentPackageName, StringComparison.OrdinalIgnoreCase))
                    .Select(update =>
                    {
                        var version = update.Package.Id.Version;
                        return new Version(version.Major, version.Minor, version.Build, version.Revision);
                    })
                    .OrderByDescending(version => version)
                    .FirstOrDefault();
            }
            catch
            {
                // Unpackaged development builds and temporarily unavailable Store services
                // cannot authoritatively confirm that a release is available.
                return null;
            }
        }
    }
}
