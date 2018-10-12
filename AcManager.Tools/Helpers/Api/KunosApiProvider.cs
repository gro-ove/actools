using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Internal;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    public static partial class KunosApiProvider {
        public static bool OptionNoProxy = false;

        public static TimeSpan OptionWebRequestTimeout = TimeSpan.FromSeconds(60d);

        // This timeout looks like too much, but sometimes, with a lot of async requests in background,
        // even requests which usually take couple of milliseconds might go on up to several seconds.
        public static TimeSpan OptionDirectRequestTimeout = TimeSpan.FromSeconds(5d);

        public static int ServersNumber => InternalUtils.KunosServersNumber;

        private static readonly object ServerUriSync = new object();

        [CanBeNull]
        private static string ServerUri {
            get {
                lock (ServerUriSync) {
                    return InternalUtils.GetKunosServerUri(SettingsHolder.Online.OnlineServerId);
                }
            }
        }

        private static void NextServer() {
            lock (ServerUriSync) {
                InternalUtils.MoveToNextKunosServer();
                Logging.Warning("Fallback to: " + (ServerUri ?? @"NULL"));
            }
        }

        private class LoadedData {
            [CanBeNull]
            public string Data;

            public DateTime LastModified;
            public long ServerTimeStamp;
        }

        [ItemNotNull]
        private static async Task<string> LoadAsync(string uri, TimeSpan? timeout = null) {
            if (!timeout.HasValue) timeout = OptionWebRequestTimeout;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            try {
                using (var cancellation = new CancellationTokenSource(timeout.Value))
                using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation.Token).ConfigureAwait(false)) {
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            } catch (Exception e) when (e.IsCancelled()) {
                throw new WebException("Timeout exceeded", WebExceptionStatus.Timeout);
            }
        }

        private static long GetServerTime(HttpResponseMessage responseMessage) {
            var headers = responseMessage.Headers;
            foreach (var header in headers) {
                if (string.Equals(header.Key, "X-Server-Time", StringComparison.OrdinalIgnoreCase)) {
                    return long.TryParse(header.Value?.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
                }
            }

            return 0;
        }

        [ItemNotNull]
        private static async Task<LoadedData> LoadAsync(string uri, DateTime? ifModifiedSince, TimeSpan? timeout = null) {
            if (!timeout.HasValue) timeout = OptionWebRequestTimeout;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (ifModifiedSince.HasValue) {
                request.Headers.IfModifiedSince = DateTime.SpecifyKind(ifModifiedSince.Value, DateTimeKind.Utc);
            }

            try {
                using (var cancellation = new CancellationTokenSource(timeout.Value))
                using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation.Token).ConfigureAwait(false)) {
                    if (response.StatusCode == HttpStatusCode.NotModified) {
                        return new LoadedData { Data = null, LastModified = ifModifiedSince ?? DateTime.Now };
                    }

                    return new LoadedData {
                        Data = await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                        LastModified = response.Content.Headers.LastModified?.DateTime ?? DateTime.Now,
                        ServerTimeStamp = GetServerTime(response)
                    };
                }
            } catch (Exception e) when (e.IsCancelled()) {
                throw new WebException("Timeout exceeded", WebExceptionStatus.Timeout);
            }
        }

        [NotNull]
        private static T[] LoadList<T>(string uri, TimeSpan timeout, Func<Stream, T[]> deserializationFn) where T : ServerInformation {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            try {
                using (var cancellation = new CancellationTokenSource(timeout))
                using (var response = HttpClientHolder.Get().SendAsync(request, cancellation.Token).Result)
                using (var stream = response.Content.ReadAsStreamAsync().Result) {
                    return deserializationFn(stream);
                }
            } catch (Exception e) when (e.IsCancelled()) {
                throw new WebException("Timeout exceeded", WebExceptionStatus.Timeout);
            }
        }

        [CanBeNull]
        public static ServerInformationComplete[] TryToGetList(IProgress<int> progress = null) {
            if (SteamIdHelper.Instance.Value == null) throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);

            for (var i = 0; i < ServersNumber && ServerUri != null; i++) {
                if (progress != null) {
                    var j = i;
                    ActionExtension.InvokeInMainThread(() => { progress.Report(j); });
                }

                var uri = ServerUri;
                var requestUri = $@"http://{uri}/lobby.ashx/list?guid={SteamIdHelper.Instance.Value}";
                ServerInformationComplete[] parsed;

                try {
                    var watch = Stopwatch.StartNew();
                    parsed = LoadList(requestUri, OptionWebRequestTimeout, ServerInformationComplete.Deserialize);
                    Logging.Write($"{watch.Elapsed.TotalMilliseconds:F1} ms");
                } catch (Exception e) {
                    Logging.Warning(e);
                    NextServer();
                    continue;
                }

                if (parsed.Length == 0) return parsed;

                var ip = parsed[0].Ip;
                if (!ip.StartsWith(@"192")) return parsed;

                for (var j = parsed.Length - 1; j >= 0; j--) {
                    var p = parsed[j];
                    if (p.Ip != ip) {
                        return parsed;
                    }
                }

                throw new InformativeException("Kunos server returned gibberish instead of list of servers",
                        "Could it be that you’re using Steam ID without AC linked to it?");
            }

            return null;
        }

        [CanBeNull]
        public static MinoratingServerInformation[] TryToGetMinoratingList() {
            try {
                var watch = Stopwatch.StartNew();
                var parsed = LoadList(@"http://www.minorating.com/MRServerLobbyAPI", OptionWebRequestTimeout, MinoratingServerInformation.Deserialize);

                var passwordsForEverything = true;
                for (var i = 0; i < parsed.Length; i++) {
                    var information = parsed[i];
                    var track = information.TrackId;
                    if (track != null) {
                        var index = track.IndexOf('[');
                        if (index != -1 && track[track.Length - 1] == ']') {
                            information.TrackId = index < track.Length - 2
                                    ? track.Substring(0, index) + "-" + track.Substring(index + 1, track.Length - index - 2) : track.Substring(0, index);
                        }
                    }

                    passwordsForEverything &= information.Password;
                }

                if (passwordsForEverything) {
                    for (var i = 0; i < parsed.Length; i++) {
                        parsed[i].Password = false;
                    }
                }

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
            if (result.Ip != ip) {
                // because, loaded directly, IP might different from global IP
                result.Ip = ip;
            }

            result.LoadedDirectly = true;

            if (result.Durations != null && result.SessionTypes != null) {
                for (var i = 0; i < result.Durations.Length; i++) {
                    if ((Game.SessionType?)result.SessionTypes.ArrayElementAtOrDefault(i) != Game.SessionType.Race) {
                        if (result.Durations[i] < 6000 /* usual value from the original launcher is 60, but it’s not enough */) {
                            result.Durations[i] *= 60;
                        }
                    } else {
                        if (result.Timed) {
                            result.Durations[i] *= 60;
                        }
                    }
                }
            }

            return result;
        }

        private static ServerInformationExtended PrepareLoadedDirectly(ServerInformationExtended result, string ip, long serverTime) {
            if (result.Ip != ip) {
                // because, loaded directly, IP might different from global IP
                result.Ip = ip;
            }

            Logging.Debug(result.Until);
            Logging.Debug(serverTime);

            if (result.Until != 0 && serverTime != 0) {
                result.UntilLocal = DateTime.Now + TimeSpan.FromMilliseconds(result.Until - serverTime);
                result.TimeLeft = Math.Max((long)(result.UntilLocal - DateTime.Now).TotalSeconds, 0);
            }

            result.LoadedDirectly = true;
            return result;
        }

        [ItemNotNull]
        public static async Task<ServerInformationComplete> GetInformationAsync(string ip, int port) {
            var steamId = SteamIdHelper.Instance.Value;
            if (steamId == null) throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);

            while (ServerUri != null) {
                var requestUri = $@"http://{ServerUri}/lobby.ashx/single?ip={ip}&port={port}&guid={steamId}";
                try {
                    var s = (await LoadAsync(requestUri, null, OptionWebRequestTimeout).ConfigureAwait(false)).Data;

                    ServerInformationComplete result;
                    try {
                        result = JsonConvert.DeserializeObject<ServerInformationComplete>(s);
                    } catch (JsonReaderException) {
                        Logging.Warning(s);
                        throw;
                    }

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

            throw new Exception(@"Out of servers");
        }

        [ItemNotNull]
        public static async Task<ServerInformationComplete> GetInformationDirectAsync(string ip, int portC) {
            var requestUri = $@"http://{ip}:{portC}/INFO";
            var loaded = await LoadAsync(requestUri, null, OptionDirectRequestTimeout).ConfigureAwait(false);
            return PrepareLoadedDirectly(JsonConvert.DeserializeObject<ServerInformationComplete>(loaded.Data), ip);
        }

        [ItemNotNull]
        public static async Task<Tuple<ServerInformationExtended, DateTime>> GetExtendedInformationDirectAsync(string ip, int portExt, DateTime? lastModified) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $@"http://{ip}:{portExt}/api/details?guid={steamId}";
            var loaded = await LoadAsync(requestUri, lastModified, OptionDirectRequestTimeout).ConfigureAwait(false);
            return Tuple.Create(loaded.Data == null ? null :
                    PrepareLoadedDirectly(JsonConvert.DeserializeObject<ServerInformationExtended>(loaded.Data), ip, loaded.ServerTimeStamp),
                    loaded.LastModified);
        }

        [ItemNotNull]
        public static async Task<ServerCarsInformation> GetCarsInformationAsync(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $@"http://{ip}:{portC}/JSON|{steamId}";
            var loaded = await LoadAsync(requestUri, null, OptionDirectRequestTimeout).ConfigureAwait(false);
            try {
                return JsonConvert.DeserializeObject<ServerCarsInformation>(loaded.Data);
            } catch (Exception) {
                Logging.Warning(loaded);
                throw;
            }
        }

        [CanBeNull]
        public static async Task<BookingResult> TryToBookAsync(string ip, int portC, string password, string carId, string skinId, string driverName,
                string teamName) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var arguments = new[] { carId, skinId, driverName, teamName, steamId, password }.Select(x => x ?? "").JoinToString('|');
            var requestUri = $@"http://{ip}:{portC}/SUB|{HttpUtility.UrlPathEncode(arguments)}";

            try {
                Logging.Debug("Request: " + requestUri);
                var response = await LoadAsync(requestUri, OptionDirectRequestTimeout);
                Logging.Debug("Response: " + response);
                var split = response.Split(',');
                switch (split[0]) {
                    case "OK":
                        return new BookingResult(TimeSpan.FromSeconds(FlexibleParser.ParseDouble(split[1])));

                    case "ILLEGAL CAR":
                        return new BookingResult(ToolsStrings.Online_BookingResult_IllegalCar);

                    case "INCORRECT PASSWORD":
                        return new BookingResult(ToolsStrings.Online_BookingResult_IncorrectPassword);

                    case "CLOSED":
                        return new BookingResult(ToolsStrings.Online_BookingResult_Closed);

                    case "BLACKLISTED":
                        return new BookingResult(ToolsStrings.Online_BookingResult_Blacklisted);

                    case "SERVER FULL":
                        return new BookingResult(ToolsStrings.Online_BookingResult_ServerFull);

                    default:
                        return new BookingResult(string.Format(ToolsStrings.Online_BookingResult_UnsupportedNonOkMessage, response));
                }
            } catch (WebException e) {
                Logging.Warning($"Cannot book: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot book: {requestUri}\n{e}");
                return null;
            }
        }

        public static async Task TryToUnbookAsync(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $@"http://{ip}:{portC}/UNSUB|{HttpUtility.UrlEncode(steamId)}";

            try {
                // using much bigger timeout to increase chances of unbooking from bad servers
                await LoadAsync(requestUri, TimeSpan.FromSeconds(30));
            } catch (WebException e) {
                Logging.Warning($"Cannot unbook: {requestUri}, {e.Message}");
            } catch (Exception e) {
                Logging.Warning($"Cannot unbook: {requestUri}\n{e}");
            }
        }

        [ItemCanBeNull]
        public static async Task<Tuple<int, TimeSpan>> TryToPingServerAsync(string ip, int port, int timeout, bool logging = false) {
            using (var order = KillerOrder.Create(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            }, timeout)) {
                var socket = order.Victim;
                var buffer = new byte[3];
                if (logging) Logging.Debug("Socket created");

                try {
                    var bytes = BitConverter.GetBytes(200);
                    var endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
                    if (logging) Logging.Debug("Sending bytes to: " + endpoint);

                    await Task.Factory.FromAsync(socket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, endpoint, null, socket),
                            socket.EndSendTo);
                    if (logging) Logging.Debug("Bytes sent");

                    if (order.Killed) {
                        if (logging) Logging.Warning("Timeout exceeded");
                        return null;
                    }

                    var timer = Stopwatch.StartNew();
                    var elapsed = TimeSpan.Zero;

                    if (logging) Logging.Debug("Receiving response…");
                    var begin = socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, a => { elapsed = timer.Elapsed; }, socket);
                    if (begin == null) {
                        if (logging) Logging.Warning("Failed to begin receiving response");
                        return null;
                    }

                    if (logging) Logging.Debug("Waiting for the end of response");
                    await Task.Factory.FromAsync(begin, socket.EndReceive);
                    if (order.Killed) {
                        if (logging) Logging.Warning("Timeout exceeded");
                        return null;
                    }

                    if (logging) Logging.Debug("Response: " + buffer.JoinToString(", "));
                    if (buffer[0] != 200 || buffer[1] + buffer[2] <= 0) {
                        if (logging) Logging.Warning("Invalid response, consider as an error");
                        return null;
                    }

                    if (logging) Logging.Write("Pinging is a success");
                    return new Tuple<int, TimeSpan>(BitConverter.ToInt16(buffer, 1), elapsed);
                } catch (Exception) {
                    return null;
                }
            }
        }

        [CanBeNull]
        public static Tuple<int, TimeSpan> TryToPingServer(string ip, int port, int timeout, bool logging = false) {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            }) {
                var buffer = new byte[3];
                if (logging) Logging.Debug("Socket created");

                try {
                    var bytes = BitConverter.GetBytes(200);
                    var endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
                    if (logging) Logging.Debug("Sending bytes to: " + endpoint);

                    socket.SendTo(bytes, endpoint);
                    if (logging) Logging.Debug("Bytes sent, receiving response…");

                    var timer = Stopwatch.StartNew();
                    socket.Receive(buffer);
                    var elapsed = timer.Elapsed;

                    if (logging) Logging.Debug("Response: " + buffer.JoinToString(", "));
                    if (buffer[0] != 200 || buffer[1] + buffer[2] <= 0) {
                        if (logging) Logging.Warning("Invalid response, consider as an error");
                        return null;
                    }

                    if (logging) Logging.Write("Pinging is a success");
                    return new Tuple<int, TimeSpan>(BitConverter.ToInt16(buffer, 1), elapsed);
                } catch (Exception e) {
                    if (logging) Logging.Warning(e);
                    return null;
                }
            }
        }
    }
}