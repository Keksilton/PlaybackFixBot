using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PlaybackFixBot.Services
{
    public class DownloadService
    {
        public const string CACHE_PATH = "download_cache/";
        private readonly WebClient _client;

        public DownloadService(WebClient client)
        {
            _client = client;
            var dirInfo = new DirectoryInfo(CACHE_PATH);
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
            using var sr = _client.OpenRead(url);
            var contlength = _client.ResponseHeaders[HttpRequestHeader.ContentLength];

            var size = long.Parse(contlength);
            if (size > 50 * 1024 * 1024)
                return string.Empty;
            var fname = Path.Combine(CACHE_PATH, Guid.NewGuid().ToString());
            using var fstream = File.Create(fname);
            await sr.CopyToAsync(fstream);
            return fname;
        }
    }
}
