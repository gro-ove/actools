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

        public static int OptionPingTimeout = 2000;
        public static int OptionWebRequestTimeout = 5000;

        public static int ServersNumber => InternalUtils.KunosServersNumber;

        private static bool _skipProxyUri;

        [CanBeNull]
        private static string ServerUri => InternalUtils.GetKunosServerUri(SettingsHolder.Online.OnlineServerId);

        [CanBeNull]
        private static string ServerProxyUri => InternalUtils.GetKunosServerProxyUri();

        private static void NextServer() {
            InternalUtils.MoveToNextKunosServer();
            Logging.Warning("[KUNOSAPIPROVIDER] Fallback to: " + (ServerUri ?? "NULL"));
        }

        private static HttpRequestCachePolicy _cachePolicy;

        private static string Load(string uri, bool deflated = false) {
            return OptionUseWebClient ? LoadUsingClient(uri, deflated) : LoadUsingRequest(uri, deflated);
        }

        private static string LoadUsingClient(string uri, bool deflated) {
            using (var client = new WebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent()
                }
            }) {
                return client.DownloadString(uri);
            }
        }
        
        private static string LoadUsingRequest(string uri, bool deflated) {
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

                    if (deflated || string.Equals(response.Headers.Get("Content-Encoding"), "gzip", StringComparison.OrdinalIgnoreCase)) {
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

            while (ServerUri != null) {
                var uri = SettingsHolder.Online.UseFastServer && !_skipProxyUri ? ServerProxyUri : ServerUri;
                var requestUri = $"http://{uri}/lobby.ashx/list?guid={SteamIdHelper.Instance.Value}";
                try {
                    var watch = Stopwatch.StartNew();
                    var data = Load(requestUri, SettingsHolder.Online.UseFastServer);
                    var loadTime = watch.Elapsed;
                    watch.Restart();
                    var parsed = JsonConvert.DeserializeObject<ServerInformation[]>(data);
                    var parsingTime = watch.Elapsed;
                    Logging.Write($"[KUNOSAPIPROVIDER] List (loading/parsing): {loadTime.TotalMilliseconds:F1} ms/{parsingTime.TotalMilliseconds:F1} ms");
                    return parsed;
                } catch (Exception e) {
                    Logging.Warning("cannot get servers list: {0}\n{1}", requestUri, e);
                }

                if (SettingsHolder.Online.UseFastServer && !_skipProxyUri) {
                    _skipProxyUri = true;
                } else {
                    NextServer();
                }
            }

            return null;
        }

        [CanBeNull]
        public static ServerInformation TryToGetInformation(string ip, int port) {
            if (SteamIdHelper.Instance.Value == null) throw new Exception("Steam ID is missing");

            while (ServerUri != null) {
                var requestUri = $"http://{ServerUri}/lobby.ashx/single?ip={ip}&port={port}&{SteamIdHelper.Instance.Value}";
                try {
                    var result = JsonConvert.DeserializeObject<ServerInformation>(Load(requestUri));
                    if (result.Ip == string.Empty) {
                        result.Ip = ip;
                    }
                    return result;
                } catch (Exception e) {
                    Logging.Warning("cannot get server information: {0}\n{1}", requestUri, e);
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

        [CanBeNull]
        public static ServerInformation TryToGetInformationDirect(string ip, int portC) {
            var requestUri = $"http://{ip}:{portC}/INFO";

            try {
                var result = JsonConvert.DeserializeObject<ServerInformation>(Load(requestUri));
                if (result.Ip == string.Empty) {
                    result.Ip = ip;
                }
                return result;
            } catch (Exception e) {
                Logging.Warning("cannot get server information: {0}\n{1}", requestUri, e);
                return null;
            }
        }

        [CanBeNull]
        public static ServerActualInformation TryToGetCurrentInformation(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? "-1";
            var requestUri = $"http://{ip}:{portC}/JSON|{steamId}";

            try {
                return JsonConvert.DeserializeObject<ServerActualInformation>(Load(requestUri));
            } catch (Exception e) {
                Logging.Warning("cannot get actual server information: {0}\n{1}", requestUri, e);
                return null;
            }
        }

        [CanBeNull]
        public static TimeSpan? TryToPingServer(string ip, int port) {
            int httpPort;
            return TryToPingServer(ip, port, out httpPort);
        }

        [CanBeNull]
        public static TimeSpan? TryToPingServer(string ip, int port, out int httpPort) {
            return TryToPingServer(ip, port, OptionPingTimeout, out httpPort);
        }

        [CanBeNull]
        public static TimeSpan? TryToPingServer(string ip, int port, int timeout, out int httpPort) {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            }) {
                var buffer = new byte[3];
                try {
                    socket.SendTo(BitConverter.GetBytes(200), new IPEndPoint(IPAddress.Parse(ip), port));
                    var timer = Stopwatch.StartNew();
                    socket.Receive(buffer);

                    if (buffer[0] != 200 || buffer[1] + buffer[2] <= 0) {
                        httpPort = -1;
                        return null;
                    }

                    httpPort = BitConverter.ToInt16(buffer, 1);
                    return timer.Elapsed;
                } catch (Exception) {
                    httpPort = -1;
                    return null;
                }
            }
        }
    }
}
