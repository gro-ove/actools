using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.DiscordRpc {
    internal class DiscordConnection : IDisposable {
        public static TimeSpan OptionJoinRequestTimeout = TimeSpan.FromSeconds(30);

        private enum Opcode : uint {
            Handshake = 0,
            Frame = 1,
            Close = 2,
            Ping = 3,
            Pong = 4,
        }

        private readonly Encoding _encoding = Encoding.UTF8;
        private readonly IDiscordHandler _handler;

        public DiscordConnection(IDiscordHandler handler) {
            _handler = handler;
        }

        private NamedPipeClientStream _pipe;
        private string _cdnHost;

        private static bool _connected;

        public async Task LaunchAsync(string appId) {
            if (_pipe != null) throw new DiscordException("Already launched");
            _pipe = null;

            for (var i = 0; i <= 9; i++) {
                var pipeName = $@"discord-ipc-{i}";
                if (WaitNamedPipe($@"\\.\pipe\{pipeName}", 0)) {
                    _pipe = new NamedPipeClientStream(pipeName);
                    try {
                        _pipe.Connect(0);
                        break;
                    } catch {
                        _pipe.Dispose();
                    }
                }
            }

            if (_pipe == null) throw new TimeoutException("Pipe not found");
            Write(Opcode.Handshake, new JObject { ["v"] = 1, ["client_id"] = appId }.ToString(Formatting.None));

            var response = await ReadAsync();
            if (response == null) return;

            if ((string)response["cmd"] != "DISPATCH" || (string)response["evt"] != "READY") return;

            _cdnHost = (string)response["data"]?["config"]?["cdn_host"];
            Utils.Log("Connected");

            if (!_connected) {
                _connected = true;
                _handler.OnFirstConnectionEstablished(appId);
            }

            if (_handler.HandlesJoin) await Execute("SUBSCRIBE", new JObject { ["evt"] = "ACTIVITY_JOIN" });
            if (_handler.HandlesSpectate) await Execute("SUBSCRIBE", new JObject { ["evt"] = "ACTIVITY_SPECTATE" });
            if (_handler.HandlesJoinRequest) await Execute("SUBSCRIBE", new JObject { ["evt"] = "ACTIVITY_JOIN_REQUEST" });
        }

        public async Task ListenAsync() {
            var buffer = new byte[1];
            while (!IsDisposed) {
                // Let’s try to stay in the same thread

                while (_reply != null && _reply.Item1 == null) {
                    await Task.Delay(100);
                }

                if (_reply != null) {
                    await ReplyWith(_reply.Item1, _reply.Item2);
                    _reply = null;
                }

                await Enqueue(async () => {
                    if (PeekNamedPipe(_pipe.SafePipeHandle, buffer, 1, out _, out var bytesAvailable, out _) && buffer[0] != 0 && bytesAvailable >= 8) {
                        ProcessMessage(await ReadAsync().ConfigureAwait(false));
                    }
                });

                await Task.Delay(100);
            }

            Utils.Log("Listening finished");
        }

        public Task UpdateAsync(DiscordRichPresence presence, int pid) {
            var details = presence.Details.Limit(128, "Unknown details");
            var activity = new JObject {
                ["state"] = presence.State.Limit(128, "Unknown state"),
                ["details"] = details,
                ["instance"] = presence.Instance
            };

            if (presence.Start.HasValue || presence.End.HasValue) {
                activity["timestamps"] = new JObject();
                if (presence.Start.HasValue) activity["timestamps"]["start"] = presence.Start.Value.ToTimestamp();
                if (presence.End.HasValue) activity["timestamps"]["end"] = presence.End.Value.ToTimestamp();
            }

            if (presence.LargeImage != null || presence.SmallImage != null) {
                activity["assets"] = new JObject();

                if (presence.LargeImage != null) {
                    activity["assets"]["large_image"] = presence.LargeImage.Key.Limit(32, DiscordImage.OptionDefaultImage);
                    activity["assets"]["large_text"] = presence.LargeImage.Text.Limit(128, details);
                }

                if (presence.SmallImage != null) {
                    activity["assets"]["small_image"] = presence.SmallImage.Key.Limit(32, DiscordImage.OptionDefaultImage);
                    activity["assets"]["small_text"] = presence.SmallImage.Text.Limit(128, details);
                }
            }

            activity["party"] = new JObject {
                ["id"] = (presence.Party?.Id).Limit(128, $"_party{pid}"),
            };

            if (presence.Party != null) {
                var size = new JArray { Math.Max(presence.Party.Size, 1) };
                if (presence.Party.Capacity > 0) {
                    size.Add(presence.Party.Capacity);
                }

                if (size.Count == 2) {
                    activity["party"]["size"] = size;
                }
            }

            if (presence.Party?.MatchSecret != null || presence.Party?.JoinSecret != null || presence.Party?.SpectateSecret != null) {
                activity["secrets"] = new JObject();
                if (presence.Party.MatchSecret != null) activity["secrets"]["match"] = presence.Party.MatchSecret.Limit(128, "_matchSecret");
                if (presence.Party.JoinSecret != null) activity["secrets"]["join"] = presence.Party.JoinSecret.Limit(128, "_joinSecret");
                if (presence.Party.SpectateSecret != null) activity["secrets"]["spectate"] = presence.Party.SpectateSecret.Limit(128, "_spectateSecret");
            }

            var footprint = activity.ToString(Formatting.None);
            if (footprint == _currentActivity) return Task.Delay(0);
            _currentActivity = footprint;

            return Execute("SET_ACTIVITY", new JObject {
                ["args"] = new JObject {
                    ["pid"] = pid,
                    ["activity"] = activity
                }
            });
        }

        private string _currentActivity;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool WaitNamedPipe(string name, int timeout);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool PeekNamedPipe(SafeHandle handle, byte[] buffer, uint bufferSize, out uint bytesRead, out uint bytesAvailable,
                out uint bytesLeft);

        private Tuple<string, DiscordJoinRequestReply> _reply;

        private async Task ReplyWith(string userId, DiscordJoinRequestReply reply) {
            for (var i = 0; i < 2; i++) {
                try {
                    Utils.Log("Replying to request: " + reply);
                    await Execute(reply == DiscordJoinRequestReply.Yes ? "SEND_ACTIVITY_JOIN_INVITE" : "CLOSE_ACTIVITY_JOIN_REQUEST",
                            new JObject {
                                ["args"] = new JObject { ["user_id"] = userId }
                            });
                    Utils.Log("Replied");
                    break;
                } catch (Exception e) {
                    Utils.Warn(e.Message);
                    if (e.Message != "no join request for that user") {
                        break;
                    }
                }
            }
        }

        private void ProcessMessage(JToken j) {
            switch ((string)j["evt"]) {
                case "ACTIVITY_JOIN":
                    if (_handler?.HandlesJoin == true) {
                        _handler.Join((string)j["data"]["secret"]);
                    }
                    break;
                case "ACTIVITY_SPECTATE":
                    if (_handler?.HandlesSpectate == true) {
                        _handler.Spectate((string)j["data"]["secret"]);
                    }
                    break;
                case "ACTIVITY_JOIN_REQUEST":
                    if (_handler?.HandlesJoinRequest == true) {
                        var u = j["data"]["user"];
                        var userId = (string)u["id"];
                        var avatar = (string)u["avatar"];

                        var cancellation = new CancellationTokenSource(OptionJoinRequestTimeout);
                        cancellation.Token.Register(() => {
                            cancellation.Cancel();
                            cancellation.Dispose();
                        });

                        _reply = Tuple.Create<string, DiscordJoinRequestReply>(null, DiscordJoinRequestReply.Ignore);
                        _handler.JoinRequest(new DiscordJoinRequest {
                            UserId = _cdnHost,
                            AvatarUrl = string.IsNullOrEmpty(avatar) ? null : $"https://{_cdnHost}/avatars/{userId}/{avatar}.png",
                            Discriminator = (string)u["discriminator"],
                            UserName = (string)u["username"],
                        }, cancellation.Token, reply => {
                            // ReplyWith(userId, reply).Forget();
                            _reply = Tuple.Create(userId, reply);
                        });
                    }
                    break;
                default:
                    Utils.Warn($"Unsupported message: {j.ToString(Formatting.Indented)}");
                    break;
            }
        }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private async Task Enqueue(Func<Task> taskGenerator) {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try {
                await taskGenerator().ConfigureAwait(false);
            } finally {
                _semaphore.Release();
            }
        }

        private int _nonce;

        private Task Execute(string command, JObject data) {
            return Enqueue(async () => {
                var nonce = WriteCommand(command, data);
                ProcessResponse(await ReadAsync(), nonce);
            });
        }

        private int WriteCommand(string command, JObject data) {
            var nonce = ++_nonce;
            data["cmd"] = command;
            data["nonce"] = nonce;
            Write(Opcode.Frame, data.ToString(Formatting.None));
            Utils.Log("Message written");
            return nonce;
        }

        private static void ProcessResponse(JToken response, int nonce) {
            Utils.Log("Response is here");
            if (response == null) throw new DiscordException("No response");

            var responseNonce = response.GetIntValueOnly("nonce");
            if (responseNonce != nonce) throw new DiscordException($"Nonce mismatch: {nonce}≠{responseNonce}");
            if (response.GetStringValueOnly("evt") == "ERROR") {
                var message = response["data"]?.GetStringValueOnly("message");
                Utils.Warn(message);
                throw new DiscordException(message);
            }
        }

        private void Write(Opcode opcode, string data) {
            if (IsDisposed) throw new DiscordException("Disposed");
            Utils.Log($"Write: {opcode}; {data}");

            var result = Fit((int)(1.5 * data.Length + 8));
            var length = _encoding.GetBytes(data, 0, data.Length, result, 8);
            GetBytes((int)opcode, result, 0);
            GetBytes(length, result, 1);
            _pipe.Write(result, 0, length + 8);
            _pipe.Flush();
        }

        public static unsafe void GetBytes(int value, byte[] result, int offset) {
            fixed (byte* numPtr = result) {
                *((int*)numPtr + offset) = value;
            }
        }

        private byte[] _buffer;

        private byte[] Fit(int size) {
            if (size > 100000) throw new DiscordException("Too much data");
            return _buffer?.Length >= size ? _buffer : (_buffer = new byte[(int)(size * 1.2)]);
        }

        [ItemCanBeNull]
        private async Task<JToken> ReadAsync(CancellationToken cancellation = default) {
            if (IsDisposed) throw new DiscordException("Disposed");

            // 400 bytes to fit all necessary stuff
            var bytes = Fit(400);

            // Read header
            await _pipe.ReadAsync(bytes, 0, 8, cancellation).ConfigureAwait(false);
            var opcode = (Opcode)BitConverter.ToInt32(bytes, 0);
            var size = BitConverter.ToInt32(bytes, 4);

            // Read data
            bytes = Fit(size);
            await _pipe.ReadAsync(bytes, 0, size, cancellation).ConfigureAwait(false);
            Utils.Log($"Read: {opcode}; {_encoding.GetString(bytes, 0, size)}");

            switch (opcode) {
                case Opcode.Close:
                    Dispose();
                    return JToken.Parse(_encoding.GetString(bytes, 0, size));
                case Opcode.Frame:
                    return JToken.Parse(_encoding.GetString(bytes, 0, size));
                case Opcode.Ping:
                    Write(Opcode.Pong, _encoding.GetString(bytes, 0, size));
                    return null;
                case Opcode.Pong:
                    return null;
                case Opcode.Handshake:
                    throw new DiscordException($"Unexpected opcode: {opcode}");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool IsDisposed { get; private set; }

        public void Dispose() {
            if (IsDisposed) return;
            IsDisposed = true;
            _pipe?.Dispose();
        }
    }
}