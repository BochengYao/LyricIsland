using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LyricHover.Core;

namespace LyricHover.App
{
    internal sealed class AppUpdateChecker
    {
        internal const string FeatureEndpoint = "https://lyric-island.top/api/app-update";
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public async Task<AppUpdateRelease> CheckAsync(
            Version currentVersion,
            string locale,
            CancellationToken cancellationToken)
        {
            using (var response = await HttpClient.GetAsync(FeatureEndpoint, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return AppUpdateFeedParser.TryGetAvailableUpdate(json, currentVersion, locale, out var release)
                    ? release
                    : null;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(6)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LyricHover-UpdateCheck/1.0");
            return client;
        }
    }
}
