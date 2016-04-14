using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    public partial class CmApiProvider {
        public static bool OptionDisableChecksumChecking = false;

        public const string ChecksumHeader = "X-Data-Checksum";

        #region Initialization
        public static readonly string UserAgent;

        static CmApiProvider() {
            UserAgent = $"ContentManager/{BuildInformation.AppVersion} ({Environment.OSVersion.Version}; {(Environment.Is64BitOperatingSystem ? "x64" : "x32")})";
        }
        #endregion

        private static bool TestChecksum(byte[] response, string targetChecksum) {
            if (OptionDisableChecksumChecking) return true;
            if (ChecksumSalt == null) return false;

            using (var sha1 = SHA1.Create()) {
                return sha1.ComputeHash(
                    Encoding.UTF8.GetBytes(sha1.ComputeHash(response).ToHexString() + ChecksumSalt)
                ).ToHexString() == targetChecksum;
            }
        }

        public static byte[] GetData(string url) {
            if (ServerAddress == null) return null;

            try {
                using (var client = new WebClient {
                    Headers = {
                        [HttpRequestHeader.UserAgent] = UserAgent
                    }
                }) {
                    var result = client.DownloadData(ServerAddress + url);

                    var checksum = client.ResponseHeaders.Get(ChecksumHeader);
                    if (checksum != null && TestChecksum(result, checksum)) {
                        return result;
                    }

                    Logging.Warning("[CMAPIPROVIDER] Checksum is missing or wrong");
                    return null;
                }
            } catch (Exception e) {
                Logging.Warning($"[CMAPIPROVIDER] Cannot get {url}: " + e);
                return null;
            }
        }
        
        public static async Task<byte[]> GetDataAsync(string url) {
            if (ServerAddress == null) return null;

            try {
                using (var client = new WebClient { Headers = { [HttpRequestHeader.UserAgent] = UserAgent } }) {
                    var result = await client.DownloadDataTaskAsync(ServerAddress + url);

                    var checksum = client.ResponseHeaders.Get(ChecksumHeader);
                    if (checksum != null && TestChecksum(result, checksum)) {
                        return result;
                    }

                    Logging.Warning("[CMAPIPROVIDER] Checksum is missing or wrong");
                    return null;
                }
            } catch (Exception e) {
                Logging.Warning($"[CMAPIPROVIDER] Cannot get {url}: " + e);
                return null;
            }
        }

        public static string GetString(string url) {
            try {
                var result = GetData(url);
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning($"[CMAPIPROVIDER] Cannot read as UTF8 from {url}: " + e);
                return null;
            }
        }

        public static async Task<string> GetStringAsync(string url) {
            try {
                var result = await GetDataAsync(url);
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning($"[CMAPIPROVIDER] Cannot read as UTF8 from {url}: " + e);
                return null;
            }
        }

        public static T Get<T>(string url) {
            try {
                var json = GetString(url);
                return json == null ? default(T) : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning($"[CMAPIPROVIDER] Cannot read as JSON from {url}: " + e);
                return default(T);
            }
        }

        public static async Task<T> GetAsync<T>(string url) {
            try {
                var json = await GetStringAsync(url);
                return json == null ? default(T) : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning($"[CMAPIPROVIDER] Cannot read as JSON from {url}: " + e);
                return default(T);
            }
        }
    }
}
