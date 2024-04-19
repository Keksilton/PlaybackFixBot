using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PlaybackFixBot.Services
{
    public class DownloadService
    {
        public const string CachePath = "download_cache/";
        private readonly WebClient _client;
        private readonly ILogger<DownloadService> _logger;
        private readonly IOptionsSnapshot<DownloadServiceSettings> _options;

        public DownloadService(WebClient client, ILogger<DownloadService> logger,
            IOptionsSnapshot<DownloadServiceSettings> options)
        {
            _client = client;
            _logger = logger;
            _options = options;
            var dirInfo = new DirectoryInfo(CachePath);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }
            else
            {
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    file.Delete();
                }
            }
        }

        public async Task<string> DownloadAsync(string url)
        {
            await using var sr = _client.OpenRead(url);
            long fileSize = 0;
            if (_client.ResponseHeaders != null)
            {
                var contentLength = _client.ResponseHeaders[HttpRequestHeader.ContentLength];

                if (!long.TryParse(contentLength, out fileSize) || fileSize > _options.Value.FileSizeLimit)
                    return string.Empty;
            }

            var fileName = Path.Combine(CachePath, Guid.NewGuid().ToString());
            _logger.LogDebug("Downloading file {FileName}, file size: {FileSize}", fileName, fileSize);
            await using var fileStream = File.Create(fileName);
            await sr!.CopyToAsync(fileStream);
            return fileName;
        }

        public async Task<Stream> GetStreamAsync(string url)
        {
            var stream = _client.OpenRead(url);
            if (_client.ResponseHeaders == null) return stream;

            var contentLength = _client.ResponseHeaders[HttpRequestHeader.ContentLength];

            long fileSize = 0;
            if (long.TryParse(contentLength, out fileSize) && fileSize <= _options.Value.FileSizeLimit) return stream;
            _logger.LogDebug("Exceeded size limit {FileSizeLimit}: {FileSize}", _options.Value.FileSizeLimit, fileSize);
            await stream.DisposeAsync();
            return Stream.Null;
        }
    }
}