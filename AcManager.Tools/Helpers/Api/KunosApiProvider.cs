using System;
using System.Collections.Generic;
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
using System.Web;
using AcManager.Internal;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace AcManager.Tools.Helpers.Api {
    public partial class KunosApiProvider {
        public static bool OptionSaveResponses = false;
        public static bool OptionUseWebClient = false;
        public static bool OptionForceDisabledCache = false;
        public static bool OptionIgnoreSystemProxy = false;
        
        public static int OptionWebRequestTimeout = 10000;

        public static int ServersNumber => InternalUtils.KunosServersNumber;

        [CanBeNull]
        private static string ServerUri => InternalUtils.GetKunosServerUri(SettingsHolder.Online.OnlineServerId);

        private static void NextServer() {
            InternalUtils.MoveToNextKunosServer();
            Logging.Warning("Fallback to: " + (ServerUri ?? @"NULL"));
        }

        private static HttpRequestCachePolicy _cachePolicy;

        [ItemNotNull]
        private static Task<string> LoadAsync(string uri) {
            return OptionUseWebClient ? LoadUsingClientAsync(uri) : LoadUsingRequestAsync(uri);
        }

        [NotNull]
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

        [ItemNotNull]
        private static async Task<string> LoadUsingClientAsync(string uri) {
            using (var order = KillerOrder.Create(new WebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent()
                }
            }, OptionWebRequestTimeout)) {
                return await order.Victim.DownloadStringTaskAsync(uri);
            }
        }

        [NotNull]
        private static string LoadUsingClient(string uri) {
            using (var client = new TimeoutyWebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent()
                }
            }) {
                return client.DownloadString(uri);
            }
        }

        [ItemNotNull]
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
                        if (stream == null) throw new Exception(@"ResponseStream = null");

                        if (string.Equals(response.Headers.Get("Content-Encoding"), @"gzip", StringComparison.OrdinalIgnoreCase)) {
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

        [NotNull]
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

            request.ContinueTimeout = OptionWebRequestTimeout;
            request.ReadWriteTimeout = OptionWebRequestTimeout;
            request.Timeout = OptionWebRequestTimeout;

            string result;
            using (var response = (HttpWebResponse)request.GetResponse()) {
                if (response.StatusCode != HttpStatusCode.OK) {
                    throw new Exception($"StatusCode = {response.StatusCode}");
                }

                using (var stream = response.GetResponseStream()) {
                    if (stream == null) {
                        throw new Exception(@"ResponseStream = null");
                    }

                    if (string.Equals(response.Headers.Get("Content-Encoding"), @"gzip", StringComparison.OrdinalIgnoreCase)) {
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

            var filename = FilesStorage.Instance.GetFilename(@"Logs",
                                                             $"Dump_{Regex.Replace(uri.Split('/').Last(), @"\W+", "_")}.json");
            if (!File.Exists(filename)) {
                File.WriteAllText(FilesStorage.Instance.GetFilename(filename), result);
            }
            return result;
        }

        [NotNull]
        private static ServerInformation[] LoadListUsingRequest(string uri) {
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

            request.ContinueTimeout = OptionWebRequestTimeout;
            request.ReadWriteTimeout = OptionWebRequestTimeout;
            request.Timeout = OptionWebRequestTimeout;

            ServerInformation[] result;
            using (var response = (HttpWebResponse)request.GetResponse()) {
                if (response.StatusCode != HttpStatusCode.OK) {
                    throw new Exception($"StatusCode = {response.StatusCode}");
                }

                using (var stream = response.GetResponseStream()) {
                    if (stream == null) {
                        throw new Exception(@"ResponseStream = null");
                    }

                    if (string.Equals(response.Headers.Get("Content-Encoding"), @"gzip", StringComparison.OrdinalIgnoreCase)) {
                        using (var deflateStream = new GZipStream(stream, CompressionMode.Decompress)) {
                            result = ServerInformation.DeserializeSafe(deflateStream);
                        }
                    } else {
                        result = ServerInformation.DeserializeSafe(stream);
                    }
                }
            }
            
            return result;
        }

        [CanBeNull]
        public static ServerInformation[] TryToGetList() {
            if (SteamIdHelper.Instance.Value == null) throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);

            for (var i = 0; i < ServersNumber && ServerUri != null; i++) {
                var uri = ServerUri;
                var requestUri = $"http://{uri}/lobby.ashx/list?guid={SteamIdHelper.Instance.Value}";
                try {
                    var watch = Stopwatch.StartNew();
                    var parsed = LoadListUsingRequest(requestUri);
                    var loadTime = watch.Elapsed;
                    Logging.Write($"List (loading+parsing): {loadTime.TotalMilliseconds:F1} ms");
                    return parsed;
                } catch (WebException e) {
                    Logging.Warning($"Cannot get servers list: {requestUri}, {e.Message}");
                } catch (Exception e) {
                    Logging.Warning($"Cannot get servers list: {requestUri}\n{e}");
                }

                NextServer();
            }

            return null;
        }

        [CanBeNull]
        public static ServerInformation TryToGetInformation(string ip, int port) {
            if (SteamIdHelper.Instance.Value == null) throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);

            while (ServerUri != null) {
                var requestUri = $"http://{ServerUri}/lobby.ashx/single?ip={ip}&port={port}&guid={SteamIdHelper.Instance.Value}";
                try {
                    var result = JsonConvert.DeserializeObject<ServerInformation>(Load(requestUri));
                    if (result.Ip == string.Empty) {
                        result.Ip = ip;
                    }
                    return result;
                } catch (WebException e) {
                    Logging.Warning($"Cannot get server information: {requestUri}, {e.Message}");
                } catch (Exception e) {
                    Logging.Warning($"Cannot get server information: {requestUri}\n{e}");
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

        private static ServerInformation PrepareLan(ServerInformation result, string ip) {
            if (result.Ip == string.Empty) {
                result.Ip = ip;
            }

            result.L = true;

            for (var i = 0; i < result.Durations.Length; i++) {
                if ((Game.SessionType?)result.SessionTypes.ElementAtOrDefault(i) != Game.SessionType.Race &&
                        result.Durations[i] < 60) {
                    result.Durations[i] *= 60;
                }
            }

            return result;
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
                return PrepareLan(JsonConvert.DeserializeObject<ServerInformation>(await LoadAsync(requestUri)), ip);
            } catch (WebException e) {
                Logging.Warning($"Cannot get server information: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot get server information: {requestUri}\n{e}");
                return null;
            }
        }

        [CanBeNull]
        public static ServerInformation TryToGetInformationDirect(string ip, int portC) {
            var requestUri = $"http://{ip}:{portC}/INFO";

            try {
                return PrepareLan(JsonConvert.DeserializeObject<ServerInformation>(Load(requestUri)), ip);
            } catch (WebException e) {
                Logging.Warning($"Cannot get server information: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot get server information: {requestUri}\n{e}");
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<ServerActualInformation> TryToGetCurrentInformationAsync(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $"http://{ip}:{portC}/JSON|{steamId}";

            try {
                return JsonConvert.DeserializeObject<ServerActualInformation>(await LoadAsync(requestUri));
            } catch (WebException e) {
                Logging.Warning($"Cannot get server information: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot get actual server information: {requestUri}\n{e}");
                return null;
            }
        }

        [CanBeNull]
        public static ServerActualInformation TryToGetCurrentInformation(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $"http://{ip}:{portC}/JSON|{steamId}";

            try {
                return JsonConvert.DeserializeObject<ServerActualInformation>(Load(requestUri));
            } catch (WebException e) {
                Logging.Warning($"Cannot get server information: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot get actual server information: {requestUri}\n{e}");
                return null;
            }
        }

        [CanBeNull]
        public static BookingResult TryToBook(string ip, int portC, string password, string carId, string skinId, string driverName, string teamName) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var arguments = new[] { carId, skinId, driverName, teamName, steamId, password }.Select(x => x ?? "").JoinToString('|');
            var requestUri = $"http://{ip}:{portC}/SUB|{HttpUtility.UrlEncode(arguments)}";
            
            try {
                var response = Load(requestUri);
                var split = response.Split(',');
                switch (split[0]) {
                    case "OK":
                        return new BookingResult(TimeSpan.FromSeconds(FlexibleParser.ParseDouble(split[1])));

                    case "ILLEGAL CAR":
                        return new BookingResult("Please, select a car supported by the server");

                    case "INCORRECT PASSWORD":
                        return new BookingResult("The password is not valid");

                    case "CLOSED":
                        return new BookingResult("Booking is closed");

                    case "BLACKLISTED":
                        return new BookingResult("You have been blacklisted on this server");

                    case "SERVER FULL":
                        return new BookingResult("Server is full");

                    default:
                        return new BookingResult($"Server says: “{response}”");
                }
            } catch (WebException e) {
                Logging.Warning($"Cannot book: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot book: {requestUri}\n{e}");
                return null;
            }
        }

        [CanBeNull]
        public static void TryToUnbook(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $"http://{ip}:{portC}/UNSUB|{HttpUtility.UrlEncode(steamId)}";
            
            try {
                Load(requestUri);
            } catch (WebException e) {
                Logging.Warning($"Cannot unbook: {requestUri}, {e.Message}");
            } catch (Exception e) {
                Logging.Warning($"Cannot unbook: {requestUri}\n{e}");
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
