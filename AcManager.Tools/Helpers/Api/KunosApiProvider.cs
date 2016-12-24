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
using System.Windows;
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
        public static bool OptionNoProxy = false;
        
        public static TimeSpan OptionWebRequestTimeout = TimeSpan.FromSeconds(10d);

        // actual server should be able to respond in four seconds, otherwise there is no sense
        // in communicating with it
        public static TimeSpan OptionDirectRequestTimeout = TimeSpan.FromSeconds(4d);

        public static int ServersNumber => InternalUtils.KunosServersNumber;

        [CanBeNull]
        private static string ServerUri => InternalUtils.GetKunosServerUri(SettingsHolder.Online.OnlineServerId);

        private static void NextServer() {
            InternalUtils.MoveToNextKunosServer();
            Logging.Warning("Fallback to: " + (ServerUri ?? @"NULL"));
        }

        private static HttpRequestCachePolicy _cachePolicy;

        [ItemNotNull]
        private static Task<string> LoadAsync(string uri, TimeSpan? timeout = null) {
            if (!timeout.HasValue) timeout = OptionWebRequestTimeout;
            return OptionUseWebClient ? LoadUsingClientAsync(uri, timeout.Value) : LoadUsingRequestAsync(uri, timeout.Value);
        }

        [NotNull]
        private static string Load(string uri, TimeSpan? timeout = null) {
            if (!timeout.HasValue) timeout = OptionWebRequestTimeout;
            return OptionUseWebClient ? LoadUsingClient(uri, timeout.Value) : LoadUsingRequest(uri, timeout.Value);
        }

        private class TimeoutyWebClient : WebClient {
            private readonly TimeSpan _timeout;

            public TimeoutyWebClient(TimeSpan? timeout = null) {
                _timeout = timeout ?? OptionWebRequestTimeout;
            }

            protected override WebRequest GetWebRequest(Uri uri) {
                var w = base.GetWebRequest(uri);
                if (w == null) return null;
                w.Timeout = (int)_timeout.TotalMilliseconds;
                return w;
            }
        }

        [ItemNotNull]
        private static async Task<string> LoadUsingClientAsync(string uri, TimeSpan timeout) {
            using (var order = KillerOrder.Create(new TimeoutyWebClient(timeout) {
                Headers = {
                    [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent()
                }
            }, timeout)) {
                return await order.Victim.DownloadStringTaskAsync(uri);
            }
        }

        [NotNull]
        private static string LoadUsingClient(string uri, TimeSpan timeout) {
            using (var client = new TimeoutyWebClient(timeout) {
                Headers = {
                    [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent()
                }
            }) {
                return client.DownloadString(uri);
            }
        }

        [ItemNotNull]
        private static async Task<string> LoadUsingRequestAsync(string uri, TimeSpan timeout) {
            using (var order = KillerOrder.Create((HttpWebRequest)WebRequest.Create(uri), timeout)) {
                var request = order.Victim;
                request.Method = "GET";
                request.UserAgent = InternalUtils.GetKunosUserAgent();
                request.Headers.Add("Accept-Encoding", "gzip");

                if (OptionNoProxy) {
                    request.Proxy = null;
                }

                if (OptionForceDisabledCache) {
                    request.CachePolicy = _cachePolicy ??
                            (_cachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore));
                }

                request.Timeout = (int)timeout.TotalMilliseconds;

                string result;
                using (var response = (HttpWebResponse)await request.GetResponseAsync()) {
                    if (response.StatusCode != HttpStatusCode.OK) throw new Exception($@"StatusCode = {response.StatusCode}");

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
        private static string LoadUsingRequest(string uri, TimeSpan timeout) {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = InternalUtils.GetKunosUserAgent();
            request.Headers.Add("Accept-Encoding", "gzip");

            if (OptionNoProxy) {
                request.Proxy = null;
            }

            if (OptionForceDisabledCache) {
                request.CachePolicy = _cachePolicy ??
                        (_cachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore));
            }

            request.ContinueTimeout = (int)timeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)timeout.TotalMilliseconds;
            request.Timeout = (int)timeout.TotalMilliseconds;

            string result;
            using (var response = (HttpWebResponse)request.GetResponse()) {
                if (response.StatusCode != HttpStatusCode.OK) {
                    throw new Exception($@"StatusCode = {response.StatusCode}");
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
        private static T[] LoadListUsingRequest<T>(string uri, TimeSpan timeout, Func<Stream, T[]> deserializationFn) where T : ServerInformation {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = InternalUtils.GetKunosUserAgent();
            request.Headers.Add("Accept-Encoding", "gzip");

            if (OptionNoProxy) {
                request.Proxy = null;
            }

            if (OptionForceDisabledCache) {
                request.CachePolicy = _cachePolicy ??
                        (_cachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore));
            }

            request.ContinueTimeout = (int)timeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)timeout.TotalMilliseconds;
            request.Timeout = (int)timeout.TotalMilliseconds;

            T[] result;
            using (var response = (HttpWebResponse)request.GetResponse()) {
                if (response.StatusCode != HttpStatusCode.OK) {
                    throw new Exception($@"StatusCode = {response.StatusCode}");
                }

                using (var stream = response.GetResponseStream()) {
                    if (stream == null) {
                        throw new Exception(@"ResponseStream = null");
                    }

                    if (string.Equals(response.Headers.Get("Content-Encoding"), @"gzip", StringComparison.OrdinalIgnoreCase)) {
                        using (var deflateStream = new GZipStream(stream, CompressionMode.Decompress)) {
                            result = deserializationFn(deflateStream);
                        }
                    } else {
                        result = deserializationFn(stream);
                    }
                }
            }
            
            return result;
        }

        [CanBeNull]
        public static ServerInformationComplete[] TryToGetList(IProgress<int> progress = null) {
            if (SteamIdHelper.Instance.Value == null) throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);

            for (var i = 0; i < ServersNumber && ServerUri != null; i++) {
                if (progress != null) {
                    var j = i;
                    Application.Current.Dispatcher.Invoke(() => {
                        progress.Report(j);
                    });
                }

                var uri = ServerUri;
                var requestUri = $@"http://{uri}/lobby.ashx/list?guid={SteamIdHelper.Instance.Value}";
                try {
                    var watch = Stopwatch.StartNew();
                    var parsed = LoadListUsingRequest(requestUri, OptionWebRequestTimeout, ServerInformationComplete.Deserialize);
                    Logging.Write($"{watch.Elapsed.TotalMilliseconds:F1} ms");
                    return parsed;
                } catch (Exception e) {
                    Logging.Warning(e.Message);
                }

                NextServer();
            }

            return null;
        }

        [CanBeNull]
        public static MinoratingServerInformation[] TryToGetMinoratingList() {
            try {
                var watch = Stopwatch.StartNew();
                var parsed = LoadListUsingRequest(@"http://www.minorating.com/MRServerLobbyAPI", OptionWebRequestTimeout, MinoratingServerInformation.Deserialize);
                Logging.Write($"{watch.Elapsed.TotalMilliseconds:F1} ms");
                return parsed;
            } catch (Exception e) {
                Logging.Warning(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Parse address from almost any format (such as IP:port, or just IP, or domain name), with or
        /// without any protocol prefix ahead of it.
        /// </summary>
        /// <param name="address">Address in almost any format.</param>
        /// <param name="ip">IP-address.</param>
        /// <param name="port">Port or -1 if port is missing.</param>
        /// <returns>True if parsing is successful.</returns>
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

        private static ServerInformationComplete PrepareLoadedDirectly(ServerInformationComplete result, string ip) {
            if (result.Ip == string.Empty) {
                result.Ip = ip;
            }

            result.LoadedDirectly = true;

            if (result.Durations != null && result.SessionTypes != null) {
                for (var i = 0; i < result.Durations.Length; i++) {
                    if ((Game.SessionType?)result.SessionTypes.ElementAtOrDefault(i) != Game.SessionType.Race &&
                            result.Durations[i] < 60) {
                        result.Durations[i] *= 60;
                    }
                }
            }

            return result;
        }

        [ItemNotNull]
        public static async Task<ServerInformationComplete> GetInformationAsync(string ip, int port) {
            if (SteamIdHelper.Instance.Value == null) throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);

            while (ServerUri != null) {
                var requestUri = $@"http://{ServerUri}/lobby.ashx/single?ip={ip}&port={port}&guid={SteamIdHelper.Instance.Value}";
                try {
                    var result = JsonConvert.DeserializeObject<ServerInformationComplete>(await LoadAsync(requestUri, OptionWebRequestTimeout));
                    if (result.Ip == string.Empty) {
                        result.Ip = ip;
                    }
                    return result;
                } catch (WebException) {
                    NextServer();
                    if (ServerUri == null) {
                        throw;
                    }
                }
            }

            throw new Exception("Out of servers");
        }

        [ItemNotNull]
        public static async Task<ServerInformationComplete> GetInformationDirectAsync(string ip, int portC) {
            var requestUri = $@"http://{ip}:{portC}/INFO";
            return PrepareLoadedDirectly(JsonConvert.DeserializeObject<ServerInformationComplete>(await LoadAsync(requestUri, OptionDirectRequestTimeout)), ip);
        }

        [ItemNotNull]
        public static async Task<ServerCarsInformation> GetCarsInformationAsync(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $@"http://{ip}:{portC}/JSON|{steamId}";
            return JsonConvert.DeserializeObject<ServerCarsInformation>(await LoadAsync(requestUri, OptionDirectRequestTimeout));
        }

        [CanBeNull]
        public static ServerInformationComplete TryToGetInformationDirect(string ip, int portC) {
            var requestUri = $@"http://{ip}:{portC}/INFO";

            try {
                return PrepareLoadedDirectly(JsonConvert.DeserializeObject<ServerInformationComplete>(Load(requestUri, OptionDirectRequestTimeout)), ip);
            } catch (WebException e) {
                Logging.Warning($"Cannot get server information: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot get server information: {requestUri}\n{e}");
                return null;
            }
        }

        [CanBeNull]
        public static BookingResult TryToBook(string ip, int portC, string password, string carId, string skinId, string driverName, string teamName) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var arguments = new[] { carId, skinId, driverName, teamName, steamId, password }.Select(x => x ?? "").JoinToString('|');
            var requestUri = $@"http://{ip}:{portC}/SUB|{HttpUtility.UrlPathEncode(arguments)}";
            
            try {
                var response = Load(requestUri, OptionDirectRequestTimeout);
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
        
        public static void TryToUnbook(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $@"http://{ip}:{portC}/UNSUB|{HttpUtility.UrlEncode(steamId)}";
            
            try {
                // using much bigger timeout to increase chances of unbooking from bad servers
                Load(requestUri, TimeSpan.FromSeconds(30));
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
