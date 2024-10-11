using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private static T[] LoadList<T>(string uri, TimeSpan timeout, CancellationToken cancellation, Func<Stream, HttpResponseHeaders, T[]> deserializationFn)
                where T : ServerInformation {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            try {
                using (var cancellationTimer = new CancellationTokenSource(timeout))
                using (var cancellationLocal = CancellationTokenSource.CreateLinkedTokenSource(cancellationTimer.Token, cancellation))
                using (var response = HttpClientHolder.Get().SendAsync(request, cancellationLocal.Token).Result)
                using (var stream = response.Content.ReadAsStreamAsync().Result) {
                    return deserializationFn(stream, response.Headers);
                }
            } catch (Exception e) when (e.IsCancelled()) {
                throw new WebException("Timeout exceeded", WebExceptionStatus.Timeout);
            }
        }

        private static string CacheKey() {
            return $@"r={DateTime.Now.ToUnixTimestamp() / 30 % (24 * 60)}";
        }

        [CanBeNull]
        public static ServerInformationComplete[] TryToGetList(IProgress<int> progress = null, CancellationToken cancellation = default) {
            if (SteamIdHelper.Instance.Value == null) throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);

            if (SettingsHolder.Online.CachingServerAvailable && SettingsHolder.Online.UseCachingServer) {
                try {
                    var watch = Stopwatch.StartNew();
                    var ret = LoadList($@"{InternalUtils.GetKunosServerCompressedProxyUri()}?{CacheKey()}",
                            OptionWebRequestTimeout, cancellation, (stream, headers) => {
                                /*Logging.Debug("List headers:");
                                foreach (var p in headers) {
                                    Logging.Debug($"\t{p.Key}: {p.Value.JoinToString("; ")}");
                                }
                                if (headers.TryGetValues("x-cache-stats", out var stats)) {
                                    Logging.Debug("Cache stats: " + stats.JoinToString());
                                }*/
                                using (var deflateStream = new GZipStream(stream, CompressionMode.Decompress)) {
                                    return ServerInformationComplete.Deserialize(deflateStream, headers);
                                }
                            });

                    /*var ret = LoadList(InternalUtils.GetKunosServerProxyUri(), OptionWebRequestTimeout, cancellation,
                            ServerInformationComplete.Deserialize);*/

                    Logging.Write($"Fast loading with proxy lobby server: {watch.Elapsed.TotalMilliseconds:F1} ms");
                    return ret;
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            if (cancellation.IsCancellationRequested) throw new UserCancelledException();
            for (var i = 0; i < ServersNumber && ServerUri != null; i++) {
                if (progress != null) {
                    var j = i;
                    ActionExtension.InvokeInMainThread(() => progress.Report(j));
                }

                var uri = ServerUri;
                var requestUri = $@"http://{uri}/lobby.ashx/list?guid={SteamIdHelper.Instance.Value}&{CacheKey()}";
                ServerInformationComplete[] parsed;

                try {
                    var watch = Stopwatch.StartNew();
                    parsed = LoadList(requestUri, OptionWebRequestTimeout, cancellation, ServerInformationComplete.Deserialize);
                    if (cancellation.IsCancellationRequested) throw new UserCancelledException();
                    Logging.Write($"Regular loading with main lobby server: {watch.Elapsed.TotalMilliseconds:F1} ms");
                } catch (Exception e) {
                    Logging.Warning(e);
                    NextServer();
                    continue;
                }

                if (parsed.Length == 0) return parsed;

                var ip = parsed[0].Ip;
                if (!ip.StartsWith(@"192")) {
                    SettingsHolder.Online.CachingServerAvailable = true;
                    return parsed;
                }

                for (var j = parsed.Length - 1; j >= 0; j--) {
                    var p = parsed[j];
                    if (p.Ip != ip) {
                        SettingsHolder.Online.CachingServerAvailable = true;
                        return parsed;
                    }
                }

                throw new InformativeException("Kunos server returned gibberish instead of list of servers",
                        "Could it be that you’re using Steam ID without AC linked to it?");
            }

            return null;
        }

        [CanBeNull]
        public static MinoratingServerInformation[] TryToGetMinoratingList(CancellationToken cancellation = default) {
            try {
                var watch = Stopwatch.StartNew();
                var parsed = LoadList(@"http://www.minorating.com/MRServerLobbyAPI", OptionWebRequestTimeout,
                        cancellation, MinoratingServerInformation.Deserialize);

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

        [Flags]
        public enum ThirdPartyFlags {
            None = 1,
            SlashTrackIDSeparator = 1,
            Deflated = 2,
        }

        [CanBeNull]
        public static ServerInformationComplete[] TryToGetThirdPartyList(string url, CancellationToken cancellation = default) {
            try {
                ThirdPartyFlags flags = ThirdPartyFlags.None;
                var watch = Stopwatch.StartNew();
                var parsed = LoadList(url, OptionWebRequestTimeout,
                        cancellation, (stream, headers) => {
                            if (headers.TryGetValues("x-flags", out var flagsList)) {
                                foreach (var flag in flagsList) {
                                    if (flag == @"deflate") flags |= ThirdPartyFlags.Deflated;
                                    if (flag == @"slashedTrackID") flags |= ThirdPartyFlags.SlashTrackIDSeparator;
                                }
                            }
                            if (flags.HasFlag(ThirdPartyFlags.Deflated)) {
                                using (var deflateStream = new GZipStream(stream, CompressionMode.Decompress)) {
                                    return ServerInformationComplete.Deserialize(deflateStream, headers);
                                }
                            }
                            return ServerInformationComplete.Deserialize(stream, headers);
                        });

                if (flags.HasFlag(ThirdPartyFlags.SlashTrackIDSeparator)) {
                    for (var i = 0; i < parsed.Length; i++) {
                        var information = parsed[i];
                        var track = information.TrackId;
                        if (track != null) {
                            var index = track.IndexOf('/');
                            if (index != -1) {
                                information.TrackId = track.Substring(0, index) + "/" + track.Substring(index + 1);
                            }
                        }
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
            try {
                var parsed = Regex.Match(address, @"^(?:.*//)?([\w\.]+)(?::(\d+))?(?:/.*)?$");
                if (parsed.Success) {
                    ip = parsed.Groups[1].Value;
                    port = parsed.Groups[2].Success ? int.Parse(parsed.Groups[2].Value, CultureInfo.InvariantCulture) : -1;
                    return true;
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
            ip = null;
            port = 0;
            return false;
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

            if (result.SessionTypes != null && result.Session >= 0 && result.Session < result.SessionTypes.Length) {
                result.Session = result.SessionTypes[result.Session];
            }

            return result;
        }

        [NotNull]
        private static ServerInformationExtended PrepareLoadedDirectly([NotNull] ServerInformationExtended result, string ip, long serverTime) {
            if (result.Ip != ip) {
                // because, loaded directly, IP might different from global IP
                result.Ip = ip;
            }

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
                    var result = ParseJsonOrThrow<ServerInformationComplete>(s);
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

        private static T ParseJsonOrThrow<T>(string data) {
            try {
                return JsonConvert.DeserializeObject<T>(data);
            } catch (Exception) {
                Logging.Warning("Failed to parse: " + data?.Trim());
                if (data?.StartsWith("{") != true) {
                    throw new Exception("Server response is in invalid format");
                }
                throw;
            }
        }

        [ItemNotNull]
        public static async Task<ServerInformationComplete> GetInformationDirectAsync(string ip, int portC) {
            var requestUri = $@"http://{ip}:{portC}/INFO";
            var loaded = await LoadAsync(requestUri, null, OptionDirectRequestTimeout).ConfigureAwait(false);
            return PrepareLoadedDirectly(ParseJsonOrThrow<ServerInformationComplete>(loaded.Data), ip);
        }

        [ItemNotNull]
        public static async Task<Tuple<ServerInformationExtended, DateTime>> GetExtendedInformationDirectAsync(string ip, int portExt, DateTime? lastModified) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $@"http://{ip}:{portExt}/api/details?guid={steamId}";
            var loaded = await LoadAsync(requestUri, lastModified, OptionDirectRequestTimeout).ConfigureAwait(false);
            if (string.IsNullOrEmpty(loaded.Data) && lastModified == null) {
                throw new WebException($@"Response is empty: {requestUri}");
            }
            return Tuple.Create(
                    loaded.Data == null ? null : PrepareLoadedDirectly(ParseJsonOrThrow<ServerInformationExtended>(loaded.Data), ip, loaded.ServerTimeStamp),
                    loaded.LastModified);
        }

        [ItemNotNull]
        public static async Task<ServerCarsInformation> GetCarsInformationAsync(string ip, int portC) {
            var steamId = SteamIdHelper.Instance.Value ?? @"-1";
            var requestUri = $@"http://{ip}:{portC}/JSON|{steamId}";
            var loaded = await LoadAsync(requestUri, null, OptionDirectRequestTimeout).ConfigureAwait(false);
            return ParseJsonOrThrow<ServerCarsInformation>(loaded.Data);
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

        private static Regex _ipRegex = new Regex(@"^\d+\.\d+\.\d+\.\d+", RegexOptions.Compiled);

        private static IPAddress ParseIPAddress(string address) {
            if (address.IndexOf(':') != -1 || _ipRegex.IsMatch(address)) return IPAddress.Parse(address);
            return Dns.GetHostEntry(address).AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork);
        }

        private static Task<IPAddress> ParseIpAddressAsync(string address) {
            if (address.IndexOf(':') != -1 || _ipRegex.IsMatch(address)) return Task.FromResult(IPAddress.Parse(address));
            return Dns.GetHostEntryAsync(address).ContinueWith(r => r.Result.AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork),
                    TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public static Task<string> ResolveIpAddressAsync(string address) {
            if (address.IndexOf(':') != -1 || _ipRegex.IsMatch(address)) return Task.FromResult(address);
            return Dns.GetHostEntryAsync(address).ContinueWith(r => r.Result.AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork).ToString(),
                    TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public class PingResponse {
            public readonly int? PortHttp;
            public readonly TimeSpan PingTime;
            public readonly string Error;

            public PingResponse(int portHttp, TimeSpan time) {
                PortHttp = portHttp;
                PingTime = time;
            }

            public PingResponse(string error) {
                Error = error;
            }
        }

        private class PingingSocket {
            private enum WaitingState : byte {
                Waiting,
                Complete,
                Errored,
                Timeouted
            }

            private class WaitingRecord {
                public readonly TaskCompletionSource<PingResponse> TaskSource = new TaskCompletionSource<PingResponse>();
                public readonly DateTime Start = DateTime.Now;
                public WaitingState State = WaitingState.Waiting;
            }

            private class WaitingComplete {
                public readonly EndPoint Origin;
                public readonly int Mark;
                public readonly DateTime TimePoint;
                public readonly string Error;

                public WaitingComplete(EndPoint origin, int mark, DateTime date) {
                    Origin = origin;
                    Mark = mark;
                    TimePoint = date;
                }

                public WaitingComplete(EndPoint origin, string error) {
                    Origin = origin;
                    Mark = int.MaxValue;
                    Error = error;
                }

                public bool IsFailed => Mark == int.MaxValue;
            }

            private readonly Socket _socket;
            private readonly Dictionary<int, WaitingRecord> _waiting = new Dictionary<int, WaitingRecord>();
            private readonly ConcurrentQueue<WaitingComplete> _received = new ConcurrentQueue<WaitingComplete>();
            private int _loopIndex;
            private bool _loopActive;

            public PingingSocket() {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                    SendTimeout = 5000,
                    ReceiveTimeout = 5000
                };
                _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                Receive();
            }

            private async Task Loop() {
                Logging.Warning($"[NewPing] New loop");
                try {
                    _loopActive = true;
                    var index = ++_loopIndex;
                    var toTimeout = new List<WaitingRecord>();
                    var toRemove = new List<int>();
                    var emptyCounter = 0;
                    while (index == _loopIndex) {
                        // ReSharper disable once InconsistentlySynchronizedField
                        if (_waiting.Count > 0) {
                            emptyCounter = 0;
                            toRemove.Clear();
                            toTimeout.Clear();
                            lock (_waiting) {
                                var timeoutPoint = DateTime.Now - TimeSpan.FromSeconds(2d);
                                var removalPoint = timeoutPoint - TimeSpan.FromSeconds(5d);
                                foreach (var p in _waiting) {
                                    if (p.Value.State == WaitingState.Waiting && p.Value.Start < timeoutPoint) {
                                        p.Value.State = WaitingState.Timeouted;
                                        toTimeout.Add(p.Value);
                                    } else if (p.Value.Start < removalPoint) {
                                        toRemove.Add(p.Key);
                                    }
                                }
                                foreach (var r in toRemove) {
                                    _waiting.Remove(r);
                                }
                            }
                            foreach (var record in toTimeout) {
                                record.TaskSource.TrySetResult(null);
                            }
                        } else if (++emptyCounter > 10) {
                            break;
                        }
                        while (_received.TryDequeue(out var item)) {
                            FinishResponse(item);
                        }
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                } finally {
                    _loopActive = false;
                    Logging.Warning($"[NewPing] Loop has been completed");
                }
            }

            private class ReceiveState {
                public byte[] Buffer = new byte[4];
                public EndPoint EndPoint = new IPEndPoint(IPAddress.Any, 0);
            }

            private void Receive() {
                var state = new ReceiveState();
                _socket.BeginReceiveFrom(state.Buffer, 0, 4, SocketFlags.None, ref state.EndPoint, ReceiveCallback, state);
            }

            private void FinishResponse(WaitingComplete complete) {
                var key = complete.Origin.GetHashCode();
                WaitingRecord existing;
                bool needsCompletion;
                lock (_waiting) {
                    if (_waiting.TryGetValue(key, out existing)) {
                        needsCompletion = existing.State == WaitingState.Waiting;
                        if (needsCompletion) {
                            existing.State = complete.IsFailed ? WaitingState.Errored : WaitingState.Complete;
                        }
                    } else {
                        needsCompletion = false;
                    }
                }
                if (existing == null) {
                    Logging.Warning($"[NewPing] {complete.Origin}: Unexpected weirdo reply: {complete.Error ?? @"<valid>"}");
                } else if (complete.IsFailed) {
                    Logging.Warning($"[NewPing] {complete.Origin}: Receive failure: {complete.Error ?? @"<unknown>"}");
                    if (needsCompletion) {
                        existing.TaskSource.TrySetResult(complete.Error != null ? new PingResponse(complete.Error) : null);
                    }
                } else if (!needsCompletion) {
                    Logging.Warning(existing.State == WaitingState.Timeouted
                            ? $"[NewPing] {complete.Origin}: Timeouted before response ({(existing.Start - DateTime.Now).TotalMilliseconds:F0} ms)"
                            : existing.State == WaitingState.Errored
                                    ? $"[NewPing] {complete.Origin}: Already failed"
                                    : $"[NewPing] {complete.Origin}: Already completed");
                } else {
                    existing.TaskSource.TrySetResult(new PingResponse(complete.Mark, complete.TimePoint - existing.Start));
                }
            }

            private void ReceiveCallback(IAsyncResult ar) {
                var timePoint = DateTime.Now;
                var args = (ReceiveState)ar.AsyncState;

                var receivedBytes = 0;
                string receiveError = null;
                try {
                    receivedBytes = _socket.EndReceiveFrom(ar, ref args.EndPoint);
                } catch (SocketException e) when (e.SocketErrorCode == SocketError.MessageSize) {
                    receivedBytes = -1;
                } catch (Exception e) {
                    receiveError = e.Message;
                }

                // Continue receiving data
                Receive();

                _received.Enqueue(receivedBytes == 3 && args.Buffer[0] == 200
                        ? new WaitingComplete(args.EndPoint, BitConverter.ToInt16(args.Buffer, 1), timePoint)
                        : new WaitingComplete(args.EndPoint, receiveError
                                ?? $"Malformed response, {(receivedBytes == -1 ? "too many" : receivedBytes.ToInvariantString())} bytes, data: {args.Buffer.JoinToString(@", ")}"));
            }

            public Task<PingResponse> SendAsync(EndPoint destination) {
                var bytes = BitConverter.GetBytes((byte)200);
                var key = destination.GetHashCode();
                bool created;
                WaitingRecord record;
                lock (_waiting) {
                    if (!_waiting.TryGetValue(key, out record)) {
                        _waiting.Add(key, record = new WaitingRecord());
                        created = true;
                    } else {
                        created = false;
                    }
                }
                if (created) {
                    try {
                        _socket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, destination, ar => _socket.EndSendTo(ar), null);
                    } catch (Exception e) {
                        _received.Enqueue(new WaitingComplete(destination, $"BeginSendTo(): {e.Message}"));
                    }
                }
                if (!_loopActive) {
                    Loop().Ignore();
                }
                return record.TaskSource.Task;
            }
        }

        private static PingingSocket _pingSocket;

        [ItemCanBeNull]
        public static Task<PingResponse> TryToPingServerAsync(string ip, int port) {
            if (_pingSocket == null) {
                _pingSocket = new PingingSocket();
            }

            if (ip.IndexOf(':') != -1 || _ipRegex.IsMatch(ip)) {
                return _pingSocket.SendAsync(new IPEndPoint(IPAddress.Parse(ip), port));
            }
            return ((Func<Task<PingResponse>>)(async () => {
                var endpoint = new IPEndPoint((await Dns.GetHostEntryAsync(ip).ConfigureAwait(false))
                        .AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork), port);
                return await _pingSocket.SendAsync(endpoint).ConfigureAwait(false);
            }))();
        }

        [ItemCanBeNull]
        public static async Task<PingResponse> TryToPingServerAsyncOld(string ip, int port, int timeout, bool logging = false) {
            using (var order = KillerOrder.Create(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            }, timeout)) {
                var socket = order.Victim;
                var buffer = new byte[3];
                if (logging) Logging.Debug("Socket created");

                try {
                    var bytes = BitConverter.GetBytes(200);
                    var endpoint = new IPEndPoint(await ParseIpAddressAsync(ip), port);
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
                    if (order.Killed || !socket.Connected) return null;
                    var begin = socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, a => { elapsed = timer.Elapsed; }, socket);
                    if (begin == null) {
                        if (logging) Logging.Warning("Failed to begin receiving response");
                        return null;
                    }

                    if (logging) Logging.Debug("Waiting for the end of response");
                    if (order.Killed || !socket.Connected) return null;
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
                    return new PingResponse(BitConverter.ToInt16(buffer, 1), elapsed);
                } catch (Exception e) {
#if DEBUG
                    Logging.Warning(e);
#endif
                    return null;
                }
            }
        }

        /*private static Queue<Socket> _socketsPool = new Queue<Socket>();

        [ItemCanBeNull]
        public static async Task<PingResponse> TryToPingServerAsync(string ip, int port, int timeout, bool logging = false) {
            var socket = _socketsPool.Count > 0 ? _socketsPool.Dequeue() : new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            };

            var buffer = new byte[3];
            if (logging) Logging.Debug("Socket created");

            void Callback(object sender, SocketAsyncEventArgs args) {
                return new PingResponse(BitConverter.ToInt16(buffer, 1), elapsed);
            }

            try {
                var bytes = BitConverter.GetBytes(200);
                var endpoint = new IPEndPoint(await ParseIPAddressAsync(ip), port);
                if (logging) Logging.Debug("Sending bytes to: " + endpoint);

                var e = new SocketAsyncEventArgs { RemoteEndPoint = endpoint };
                e.SetBuffer(bytes, 0, bytes.Length);
                e.Completed += Callback;

                var completedAsync = false;
                try {
                    completedAsync = socket.SendAsync(e);
                } catch (SocketException se) {
                    Console.WriteLine("Socket Exception: " + se.ErrorCode + " Message: " + se.Message);
                }

                if (!completedAsync) {
                    Callback(null, e);
                }

                await Task.Factory.FromAsync(socket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, endpoint, null, socket),
                        socket.EndSendTo);
                if (logging) Logging.Debug("Bytes sent");

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

                if (logging) Logging.Debug("Response: " + buffer.JoinToString(", "));
                if (buffer[0] != 200 || buffer[1] + buffer[2] <= 0) {
                    if (logging) Logging.Warning("Invalid response, consider as an error");
                    return null;
                }

                if (logging) Logging.Write("Pinging is a success");
                return new PingResponse(BitConverter.ToInt16(buffer, 1), elapsed);
            } catch (Exception) {
                return null;
            } finally {
                _socketsPool.Enqueue(socket);
            }
        }*/

        [CanBeNull]
        public static PingResponse TryToPingServer(string ip, int port, int timeout, bool logging = false) {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            }) {
                var buffer = new byte[3];
                if (logging) Logging.Debug("Socket created");

                try {
                    var bytes = BitConverter.GetBytes(200);
                    var endpoint = new IPEndPoint(ParseIPAddress(ip), port);
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
                    return new PingResponse(BitConverter.ToInt16(buffer, 1), elapsed);
                } catch (Exception e) {
                    if (logging) Logging.Warning(e);
                    return null;
                }
            }
        }
    }
}