using System;
using System.Net;
using System.Net.Http;
using AcManager.Internal;

namespace AcManager.Tools.Helpers.Api {
    public static class HttpClientHolder {
        private static HttpClient _httpClient;

        public static HttpClient Get() {
            if (_httpClient == null) {
                var handler = new HttpClientHandler {
                    AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                    AllowAutoRedirect = true,
                    UseCookies = false,
                    UseProxy = !KunosApiProvider.OptionNoProxy
                };

                _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60d) };
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", InternalUtils.GetKunosUserAgent());
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Agent", CmApiProvider.UserAgent);
            }

            return _httpClient;
        }
    }
}