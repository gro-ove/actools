using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Helpers.Api.Kunos;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SharpCompress.Compressor;
using SharpCompress.Compressor.Deflate;

namespace AcManager.Tools.Helpers.Api {
    public partial class KunosApiProvider {
        public static bool OptionSaveResponses = false;
        public static bool OptionUseWebClient = false;
        public static bool OptionForceDisabledCache = false;
        public static bool OptionIgnoreSystemProxy = false;
        
        public static int OptionWebRequestTimeout = 5000;

        public static int ServersNumber => InternalUtils.KunosServersNumber;

        [CanBeNull]
        private static string ServerUri => InternalUtils.GetKunosServerUri(SettingsHolder.Online.OnlineServerId);

        private static void NextServer() {
            InternalUtils.MoveToNextKunosServer();
            Logging.Warning("[KunosApiProvider] Fallback to: " + (ServerUri ?? "NULL"));
        }

        private static HttpRequestCachePolicy _cachePolicy;

        private static Task<string> LoadAsync(string uri) {
            return OptionUseWebClient ? LoadUsingClientAsync(uri) : LoadUsingRequestAsync(uri);
        }

        private static string Load(string uri) {
            return OptionUseWebClient ? LoadUsingClient(uri) : LoadUsingRequest(uri);
        }

        private class TimeoutyWebClient : WebClient {
            protected override WebRequest GetWebRequest(Uri uri) {
                var w = base.GetWebRequest(uri);
                if (w == null) return null;
                w.Timeout = OptionWebRequestTimeout;
                return w;
            }
        }

        private static async Task<string> LoadUsingClientAsync(string uri) {
            using (var order = KillerOrder.Create(new WebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent()
                }
            }, OptionWebRequestTimeout)) {
                var result = await order.Victim.DownloadStringTaskAsync(uri);
                return result;
            }
        }

        private static string LoadUsingClient(string uri) {
            using (var client = new TimeoutyWebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent()
                }
            }) {
                return client.DownloadString(uri);
            }
        }

        private static async Task<string> LoadUsingRequestAsync(string uri) {
            using (var order = KillerOrder.Create((HttpWebRequest)WebRequest.Create(uri), OptionWebRequestTimeout)) {
                var request = order.Victim;
                request.Method = "GET";
                request.UserAgent = InternalUtils.GetKunosUserAgent();
                request.Headers.Add("Accept-Encoding", "gzip");

                if (OptionIgnoreSystemProxy) {
                    request.Proxy = null;
                }

                if (OptionForceDisabledCache) {
                    request.CachePolicy = _cachePolicy ??
                            (_cachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore));
                }

                request.Timeout = OptionWebRequestTimeout;

                string result;
                using (var response = (HttpWebResponse)await request.GetResponseAsync()) {
                    if (response.StatusCode != HttpStatusCode.OK) throw new Exception($"StatusCode = {response.StatusCode}");

                    using (var stream = response.GetResponseStream()) {
                        if (stream == null) throw new Exception("ResponseStream = null");

                        if (string.Equals(response.Headers.Get("Content-Encoding"), "gzip", StringComparison.OrdinalIgnoreCase)) {
                            using (var deflateStream = new GZipStream(stream, CompressionMode.Decompress)) {
                                using (var reader = new StreamReader(deflateStream, Encoding.UTF8)) {
                                    result = await reader.ReadToEndAsync();
                                }
                            }
                        } else {
                            using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                                result = await reader.ReadToEndAsync();
                            }
                        }
                    }
                }

                if (OptionSaveResponses) {
                    var filename = FilesStorage.Instance.GetFilename("Logs",
                            $"Dump_{Regex.Replace(uri.Split('/').Last(), @"\W+", "_")}.json");
                    if (!File.Exists(filename)) {
                        File.WriteAllText(FilesStorage.Instance.GetFilename(filename), result);
                    }
                }

                return result;
            }
        }
        
        private static string LoadUsingRequest(string uri) {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = InternalUtils.GetKunosUserAgent();
            request.Headers.Add("Accept-Encoding", "gzip");

            if (OptionIgnoreSystemProxy) {
                request.Proxy = null;
            }

            if (OptionForceDisabledCache) {
                request.CachePolicy = _cachePolicy ??
                        (_cachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore));
            }

            request.Timeout = OptionWebRequestTimeout;

            string result;
            using (var response = (HttpWebResponse)request.GetResponse()) {
                if (response.StatusCode != HttpStatusCode.OK) {
                    throw new Exception($"StatusCode = {response.StatusCode}");
                }

                using (var stream = response.GetResponseStream()) {
                    if (stream == null) {
                        throw new Exception("ResponseStream = null");
                    }

                    if (string.Equals(response.Headers.Get("Content-Encoding"), "gzip", StringComparison.OrdinalIgnoreCase)) {
                        using (var deflateStream = new GZipStream(stream, CompressionMode.Decompress)) {
                            using (var reader = new StreamReader(deflateStream, Encoding.UTF8)) {
                                result = reader.ReadToEnd();
                            }
                        }
                    } else {
                        using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                            result = reader.ReadToEnd();
                        }
                    }
                }
            }

            if (!OptionSaveResponses) return result;

            var filename = FilesStorage.Instance.GetFilename("Logs",
                                                             $"Dump_{Regex.Replace(uri.Split('/').Last(), @"\W+", "_")}.json");
            if (!File.Exists(filename)) {
                File.WriteAllText(FilesStorage.Instance.GetFilename(filename), result);
            }
            return result;
        }

        [CanBeNull]
        public static ServerInformation[] TryToGetList() {
            if (SteamIdHelper.Instance.Value == null) throw new Exception("Steam ID is missing");

            for (var i = 0; i < ServersNumber && ServerUri != null; i++) {
                var uri = ServerUri;
                var requestUri = $"http://{uri}/lobby.ashx/list?guid={SteamIdHelper.Instance.Value}";
                try {
                    var watch = Stopwatch.StartNew();
                    var data = Load(requestUri);
                    var loadTime = watch.Elapsed;
                    watch.Restart();
                    var parsed = JsonConvert.DeserializeObject<ServerInformation[]>(data);
                    var parsingTime = watch.Elapsed;
                    Logging.Write($"[KunosApiProvider] List (loading/parsing): {loadTime.TotalMilliseconds:F1} ms/{parsingTime.TotalMilliseconds:F1} ms");
                    return parsed;
                } catch (WebException e) {
                    Logging.Warning("Cannot get servers list: {0}, {1}", requestUri, e.Message);
                } catch (Exception e) {
                    Logging.Warning("Cannot get servers list: {0}\n{1}", requestUri, e);
                }

                NextServer();
            }

            return null;
        }

        [CanBeNull]
        public static ServerInformation TryToGetInformation(string ip, int port) {
            if (SteamIdHelper.Instance.Value == null) throw new Exception("Steam ID is missing");

            while (ServerUri != null) {
                var requestUri = $"http://{ServerUri}/lobby.ashx/single?ip={ip}&port={port}&guid={SteamIdHelper.Instance.Value}";
                try {
                    var result = JsonConvert.DeserializeObject<ServerInformation>(Load(requestUri));
                    if (result.Ip == string.Empty) {
                        result.Ip = ip;
                    }
                    return result;
                } catch (WebException e) {
                    Logging.Warning("Cannot get server information: {0}, {1}", requestUri, e.Message);
                } catch (Exception e) {
                    Logging.Warning("Cannot get server information: {0}\n{1}", requestUri, e);
                }

                NextServer();
            }

            return null;
        }

        public static bool ParseAddress(string address, out string ip, out int port) {
            var parsed = Regex.Match(address, @"^(?:.*//)?((?:\d+\.){3}\d+)(?::(\d+))?(?:/.*)?$");
            if (!parsed.Success) {
                parsed = Regex.Match(address, @"^(?:.*//)?([\w\.]+)(?::(\d+))(?:/.*)?$");
                if (!parsed.Success) {
                    ip = null;
                    port = 0;
                    return false;
                }

                ip = Dns.GetHostEntry(parsed.Groups[1].Value).AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToString();
            } else {
                ip = parsed.Groups[1].Value;
            }

            port = parsed.Groups[2].Success ? int.Parse(parsed.Groups[2].Value, CultureInfo.InvariantCulture) : -1;
            return true;
        }

        [CanBeNull]
        public static ServerInformation TryToGetInformationDirect([NotNull] string address) {
            if (address == null) throw new ArgumentNullException(nameof(address));

            string ip;
            int port;
            return ParseAddress(address, out ip, out port) && port > 0 ? TryToGetInformationDirect(ip, port) : null;
        }

        [ItemCanBeNull]
        public static async Task<ServerInformation> TryToGetInformationDirectAsync(string ip, int portC) {
            var requestUri = $"http://{ip}:{portC}/INFO";

            try {
                var result = JsonConvert.DeserializeObject<ServerInformation>(await LoadAsync(requestUri));
                if (result.Ip == string.Empty) {
                    result.Ip = ip;
                }
                return result;
            } catch (WebException e) {
                Logging.Warning("Cannot get server information: {0}, {1}", requestUri, e.Message);
                return null;
            } catch (Exception e) {
                Logging.Warning("Cannot get server information: {0}\n{1}", requestUri, e);
                return null;
            }
        }

        [CanBeNull]
        public static ServerInformation TryToGetInformationDirect(string ip, int portC) {
            var requestUri = $"http://{ip}:{portC}/INFO";

            try {
                var result = JsonConvert.DeserializeObject<ServerInformation>(Load(requestUri));
                if (result.Ip == string.Empty) {
                    result.Ip = ip;
                }
                return result;
            } catch (WebException e) {
                Logging.Warning("Cannot get server information: {0}, {1}", requestUri, e.Message);
                return null;
            } catch (Exception e) {
                Logging.Warning("Cannot get server information: {0}\n{1}", requestUri, e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<ServerActualInformation> TryToGetCurrentInformationAsync(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? "-1";
            var requestUri = $"http://{ip}:{portC}/JSON|{steamId}";

            try {
                return JsonConvert.DeserializeObject<ServerActualInformation>(await LoadAsync(requestUri));
            } catch (WebException e) {
                Logging.Warning("Cannot get server information: {0}, {1}", requestUri, e.Message);
                return null;
            } catch (Exception e) {
                Logging.Warning("Cannot get actual server information: {0}\n{1}", requestUri, e);
                return null;
            }
        }

        [CanBeNull]
        public static ServerActualInformation TryToGetCurrentInformation(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? "-1";
            var requestUri = $"http://{ip}:{portC}/JSON|{steamId}";

            try {
                return JsonConvert.DeserializeObject<ServerActualInformation>(Load(requestUri));
            } catch (WebException e) {
                Logging.Warning("Cannot get server information: {0}, {1}", requestUri, e.Message);
                return null;
            } catch (Exception e) {
                Logging.Warning("Cannot get actual server information: {0}\n{1}", requestUri, e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<Tuple<int, TimeSpan>> TryToPingServerAsync(string ip, int port, int timeout) {
            using (var order = KillerOrder.Create(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            }, timeout)) {
                var socket = order.Victim;
                var buffer = new byte[3];

                try {
                    var bytes = BitConverter.GetBytes(200);
                    var endpoint = new IPEndPoint(IPAddress.Parse(ip), port);

                    await Task.Factory.FromAsync(socket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, endpoint, null, socket),
                            socket.EndSendTo);
                    if (order.Killed) return null;

                    var timer = Stopwatch.StartNew();
                    var elapsed = TimeSpan.Zero;

                    var begin = socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, a => { elapsed = timer.Elapsed; }, socket);
                    if (begin == null) return null;

                    await Task.Factory.FromAsync(begin, socket.EndReceive);
                    if (order.Killed) return null;

                    return buffer[0] != 200 || buffer[1] + buffer[2] <= 0 ? null :
                            new Tuple<int, TimeSpan>(BitConverter.ToInt16(buffer, 1), elapsed);
                } catch (Exception) {
                    return null;
                }
            }
        }

        [CanBeNull]
        public static Tuple<int, TimeSpan> TryToPingServer(string ip, int port, int timeout) {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            }) {
                var buffer = new byte[3];
                try {
                    socket.SendTo(BitConverter.GetBytes(200), new IPEndPoint(IPAddress.Parse(ip), port));
                    var timer = Stopwatch.StartNew();
                    socket.Receive(buffer);

                    return buffer[0] != 200 || buffer[1] + buffer[2] <= 0 ? null :
                            new Tuple<int, TimeSpan>(BitConverter.ToInt16(buffer, 1), timer.Elapsed);
                } catch (Exception) {
                    return null;
                }
            }
        }
    }
}
