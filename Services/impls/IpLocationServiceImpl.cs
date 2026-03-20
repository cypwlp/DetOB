using IP2Region.Net.Abstractions;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace OB.Services.impls
{
    public class IpLocationServiceImpl : IIpLocationService
    {
        private readonly ISearcher _searcher;
        private readonly HttpClient _httpClient;

        public IpLocationServiceImpl(ISearcher searcher)
        {
            _searcher = searcher;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        public async Task<string> GetPublicIpAsync()
        {
            try
            {
                return await _httpClient.GetStringAsync("https://api.ipify.org").ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        public string GetCountryByIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return "未知";

            try
            {
                var region = _searcher.Search(ip);
                if (string.IsNullOrEmpty(region))
                    return "未知";

                var parts = region.Split('|');
                return parts.Length > 0 ? parts[0] : "未知";
            }
            catch
            {
                return "查询失败";
            }
        }
    }
}