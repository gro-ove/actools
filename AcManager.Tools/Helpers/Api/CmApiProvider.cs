using System;
using System.Text;
using System.Threading.Tasks;
using AcManager.Internal;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    public class CmApiProvider {
        #region Initialization
        public static readonly string UserAgent;

        static CmApiProvider() {
            UserAgent = $"ContentManager/{BuildInformation.AppVersion} ({Environment.OSVersion.Version}; {(Environment.Is64BitOperatingSystem ? "x64" : "x32")})";
        }
        #endregion

        public static string GetString(string url) {
            try {
                var result = InternalUtils.CmGetData(url, UserAgent);
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning($"[CMAPIPROVIDER] Cannot read as UTF8 from {url}: " + e);
                return null;
            }
        }

        public static async Task<string> GetStringAsync(string url) {
            try {
                var result = await InternalUtils.CmGetDataAsync(url, UserAgent);
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

        public static Task<byte[]> GetDataAsync(string url) {
            return InternalUtils.CmGetDataAsync(url, UserAgent);
        }
    }
}
