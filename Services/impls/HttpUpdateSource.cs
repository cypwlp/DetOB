using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace OB.Services.impls
{
    public class HttpUpdateSource : IUpdateSource
    {
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;

        public HttpUpdateSource(string baseUrl, HttpClient httpClient = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<VelopackAssetFeed> GetReleaseFeed(
            IVelopackLogger log,
            string? channel,
            string? baseUrl,
            Guid? stagingId,
            VelopackAsset? latestLocalRelease)
        {
            string releasesJsonUrl = $"{_baseUrl}/releases/releases.json";
            var response = await _httpClient.GetAsync(releasesJsonUrl);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var feed = await JsonSerializer.DeserializeAsync<VelopackAssetFeed>(stream);
            return feed;
        }

        public async Task DownloadReleaseEntry(
            IVelopackLogger log,
            VelopackAsset releaseEntry,
            string localFile,
            Action<int>? progress,
            CancellationToken cancellationToken)
        {
            string fileUrl = $"{_baseUrl}/releases/{releaseEntry.FileName}";
            using var response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(localFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalRead += bytesRead;
                if (totalBytes > 0 && progress != null)
                {
                    progress((int)((double)totalRead / totalBytes * 100));
                }
            }
        }
    }
}