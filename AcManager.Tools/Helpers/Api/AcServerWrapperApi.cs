using System;
using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    [Localizable(false)]
    public static class AcServerWrapperApi {
        public static TimeSpan OptionTimeout = TimeSpan.FromSeconds(10);

        private static string EncodePassword(string password) {
            using (var sha1 = SHA1.Create()) {
                return sha1.ComputeHash(Encoding.UTF8.GetBytes("alaskankleekai" + password))
                                                     .ToHexString().ToLowerInvariant();
            }
        }

        [ItemNotNull]
        public static async Task<AcServerStatus> GetCurrentStateAsync(string ip, int port, string password) {
            try {
                using (var killer = KillerOrder.Create(new CookieAwareWebClient(), OptionTimeout)) {
                    return JsonConvert.DeserializeObject<AcServerStatus>(await killer.Victim.DownloadStringTaskAsync(
                            $"http://{ip}:{port}/api/control/acserver?password={EncodePassword(password)}&_method=STATUS"));
                }
            } catch (Exception e) when (e.IsCanceled()) {
                throw new WebException("Timeout exceeded", WebExceptionStatus.Timeout);
            }
        }
    }

    public class AcServerStatus {
        [JsonProperty("running")]
        public bool IsRunning { get; set; }
    }
}