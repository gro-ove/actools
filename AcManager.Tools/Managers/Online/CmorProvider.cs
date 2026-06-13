using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace AcManager.Tools.Managers.Online {
    public static class CmorProvider {
        #if DEBUG
        private static string _serviceUrl = "http://127.0.0.1:22220";
        #else
        private static string _serviceUrl = "https://cmor.acstuff.club";
        #endif
        private static string _sessionToken;
        private static HashSet<ServerEntry> _awaiting;
        private static Dictionary<ulong, uint> _records;

        private static void FillServer(ServerEntry target) {
            if (!_records.TryGetValue(target.Id64, out uint votes)) votes = 0;
            target.SyncVotes((ushort)(votes >> 16), (ushort)votes);
        }

        public static void EnsureInitialized(ServerEntry target) {
            if (!SettingsHolder.Online.UseCommunityRating) return;
            if (_records != null) {
                FillServer(target);
                return;
            }
            var awaitingCur = _awaiting;
            if (awaitingCur == null) {
                var created = new HashSet<ServerEntry>();
                awaitingCur = Interlocked.CompareExchange(ref _awaiting, created, null);
                if (awaitingCur == null) {
                    awaitingCur = created;
                    LoadRatingsAsync().ContinueWithInMainThread(r => {
                        _records = (r.Status == TaskStatus.RanToCompletion ? r.Result : null) ?? new Dictionary<ulong, uint>();
                        lock (_awaiting) {
                            foreach (var server in _awaiting) {
                                FillServer(server);
                            }
                            _awaiting = null;
                        }
                    });
                } 
            }
            lock (awaitingCur) {
                awaitingCur.Add(target);
            }
        }

        private static async Task<Dictionary<ulong, uint>> LoadRatingsAsync() {
            try {
                using (var order = KillerOrder.Create(new CookieAwareWebClient {
                    Headers = { [@"Accept"] = @"application/octet-stream" }
                }, TimeSpan.FromMinutes(2))) {
                    var data = await order.Victim.DownloadDataTaskAsync(new Uri($"{_serviceUrl}/rate", UriKind.RelativeOrAbsolute)).ConfigureAwait(false);
                    Logging.Debug("/rate data: " + data.Length);
                    return await Task.Run(() => {
                        var decom = new DeflateStream(
                                new MemoryStream(data),
                                CompressionMode.Decompress).ReadAsBytesAndDispose();
                        if (decom.Length % 12 != 0) {
                            throw new Exception("Malformed ratings data");
                        }
                        var buf = new Dictionary<ulong, uint>(decom.Length / 12);
                        for (var i = 0; i < decom.Length; i += 12) {
                            buf[BitConverter.ToUInt64(decom, i)] = BitConverter.ToUInt32(decom, i + 8);
                        }
                        return buf;
                    }).ConfigureAwait(false);
                }
            } catch (Exception e) {
                Logging.Error($"Failed to parse rating data: {e}");
                return new Dictionary<ulong, uint>();
            }
        }

        private static async Task<JObject> RequestAsync(Func<CookieAwareWebClient, Task<string>> callback, bool forceAuth) {
            for (int i = 0;; ++i) {
                if (string.IsNullOrEmpty(_sessionToken)) {
                    try {
                        if (await RefreshSessionTokenAsync()) i = 1;
                    } catch (Exception) when (!forceAuth) {
                        i = 1;
                    }
                }
                using (var order = KillerOrder.Create(new CookieAwareWebClient {
                    Headers = { [@"Accept"] = @"application/json", [@"X-Session-Token"] = _sessionToken }
                }, TimeSpan.FromMinutes(2))) {
                    try {
                        var response = JObject.Parse(await callback(order.Victim));
                        var error = response.GetStringValueOnly("error");
                        if (error == null) return response;
                        throw new InformativeException($"Service error: {error}", "Please try again later.");
                    } catch (WebException e) {
                        var webResponse = e.Response as HttpWebResponse;
                        if (i == 0 && webResponse != null && webResponse.StatusCode == HttpStatusCode.Unauthorized) {
                            _sessionToken = string.Empty;
                        } else {
                            string errMsg = null;
                            if (webResponse != null && webResponse.StatusCode == HttpStatusCode.BadRequest
                                && webResponse.ContentType.StartsWith(@"application/json")) {
                                try {
                                    var stream = webResponse.GetResponseStream();
                                    if (stream != null) {
                                        var error = JObject.Parse(Encoding.UTF8.GetString(await stream.ReadAsBytesAndDisposeAsync().ConfigureAwait(false)))?
                                                .GetStringValueOnly("error");
                                        if (!string.IsNullOrEmpty(error)) {
                                            errMsg = error;
                                        }
                                    }
                                } catch {
                                    // ignored
                                }
                            }
                            throw new InformativeException(errMsg ?? $"Service error: {e.Message}", errMsg == null ? "Please try again later." : null, e);
                        }
                    }
                }
            }
        }

        public static Task<JObject> GetAsync(string endpoint, bool forceAuth) {
            return RequestAsync(client => client.DownloadStringTaskAsync(new Uri($"{_serviceUrl}/{endpoint}", UriKind.RelativeOrAbsolute)), forceAuth);
        }

        public static Task<JObject> PostAsync(string endpoint, JObject payload) {
            return RequestAsync(client => client.UploadStringTaskAsync(new Uri($"{_serviceUrl}/{endpoint}", UriKind.RelativeOrAbsolute),
                    payload.ToString(Formatting.None)), true);
        }

        private static async Task<bool> RefreshSessionTokenAsync() {
#if DEBUG
            Logging.Debug($"Refreshing auth token: {_sessionToken ?? "<null>"}");
#endif
            if (_sessionToken == null) {
                var pieces = ValuesStorage.GetEncrypted(".cmor.auth", string.Empty)?.Split(';');
                if (pieces?.Length == 2 && DateTime.Now - pieces[0].As(0L).ToDateTime() < TimeSpan.FromDays(5)) {
                    _sessionToken = pieces[1];
                }
                if (!string.IsNullOrEmpty(_sessionToken)) return false;
            }
            _sessionToken = string.Empty;
            var steamToken = SteamTicketProvider.GetTicketHex();
            if (string.IsNullOrEmpty(steamToken)) {
                throw new InformativeException("Failed to communicate with Steam", "Please make sure Steam is connected to the internet.");
            }
#if DEBUG
            Logging.Debug($"Got Steam token: {steamToken.Length}");
#endif
            using (var order = KillerOrder.Create(new CookieAwareWebClient {
                Headers = { [@"Accept"] = @"application/json" }
            }, TimeSpan.FromMinutes(1))) {
                var response = JObject.Parse(await order.Victim.UploadStringTaskAsync(new Uri($"{_serviceUrl}/session", UriKind.RelativeOrAbsolute),
                        new JObject { ["steamToken"] = steamToken }.ToString(Formatting.None)));
                var sessionToken = response.GetStringValueOnly("token");
#if DEBUG
                Logging.Debug($"Got session token: {sessionToken ?? "<null>"}");
#endif
                if (sessionToken != null) {
                    ValuesStorage.SetEncrypted(".cmor.auth", $"{DateTime.Now.ToUnixTimestamp().ToInvariantString()};{sessionToken}");
                    _sessionToken = sessionToken;
                } else {
                    throw new InformativeException("Failed to authorize", "Please try again later.");
                }
            }
            return true;
        }
    }
}