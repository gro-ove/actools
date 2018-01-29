using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties.InGameApp {
    public class CmInGameAppHelper : IDisposable {
        public static readonly string[] OverlayAppIds = { "CMControlsMessages" };

        public static int OptionSocketPort = 52623;

        private bool _isActive;

        private CmInGameAppHelper() {
            _isActive = IsAvailable();
            Logging.Write($"Is app active: {_isActive}");
        }

        private static CmInGameAppHelper _instance;

        public static CmInGameAppHelper GetInstance() {
            return _instance ?? (_instance = new CmInGameAppHelper());
        }

        public static bool IsAvailable() {
            return OverlayAppIds.Any(x => PythonAppsManager.Instance.GetById(x) != null
                    && new IniFile(AcPaths.GetCfgAppsFilename())[x.ToUpperInvariant()].GetBool("ACTIVE", false));
        }

        private static readonly Busy Busy = new Busy();
        private bool _hideAfterwards;
        private bool _killAfterwards;

        private void RunSafe(Func<Task> fn) {
            if (!_isActive) return;
            Busy.Task(async () => {
                try {
                    await fn().ConfigureAwait(false);
                } catch (Exception e) {
                    if (!_warn) {
                        _warn = true;
                        Logging.Error(e);
                    }

                    CloseSocket();
                }
            });
        }

        public void Update([CanBeNull] CmInGameAppParamsBase appParams) {
            if (appParams == null) return;
            RunSafe(async () => {
                await UpdateAsync(appParams).ConfigureAwait(false);
                if (_hideAfterwards) {
                    await HideAppInternalAsync().ConfigureAwait(false);
                } else if (_killAfterwards) {
                    Logging.Debug("Close socket because of _killAfterwards flag");
                    CloseSocket();
                }
            });
        }

        public void HideApp() {
            if (Busy.Is) {
                _hideAfterwards = true;
                return;
            }

            RunSafe(HideAppInternalAsync);
        }

        private Task UpdateAsync([NotNull] CmInGameAppParamsBase appParams) {
            switch (appParams) {
                case CmInGameAppDelayedInputParams delayedInput:
                    return UpdateDelayedAsync(delayedInput);
                case CmInGameAppJoinRequestParams joinRequest:
                    return UpdateDiscordAsync(joinRequest);
                default:
                    Logging.Warning("Not supported: " + appParams.GetType());
                    return Task.Delay(0);
            }
        }

        private async Task HideAppInternalAsync() {
            if (!await EnsureConnectedAsync(false).ConfigureAwait(false)) return;
            await SendRequestAsync(null, null, 0, 0).ConfigureAwait(false);
            _hideAfterwards = false;
            if (_killAfterwards) {
                Logging.Debug("Close socket because of _killAfterwards flag");
                CloseSocket();
            }
        }

        private async Task UpdateDelayedAsync(CmInGameAppDelayedInputParams delayedInput) {
            if (!await EnsureConnectedAsync(true).ConfigureAwait(false)) return;
            await SendRequestAsync(delayedInput.CommandName, null, (delayedInput.Progress * 255).ClampToByte(), 0).ConfigureAwait(false);
        }

        private async Task UpdateDiscordAsync(CmInGameAppJoinRequestParams joinRequest) {
            if (!await EnsureConnectedAsync(true).ConfigureAwait(false)) return;
            var avatar = await GetAvatarAsync(joinRequest).ConfigureAwait(false);
            var response = await SendRequestAsync(joinRequest.UserName, avatar,
                    (joinRequest.YesProgress * 255).ClampToByte(), (joinRequest.NoProgress * 255).ClampToByte()).ConfigureAwait(false);
            switch (response) {
                case AppResponse.Yes:
                    joinRequest.ChoiseCallback?.Invoke(true);
                    break;
                case AppResponse.No:
                    joinRequest.ChoiseCallback?.Invoke(false);
                    break;
                case AppResponse.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [CanBeNull]
        private Socket _socket;

        [CanBeNull]
        private KillerOrder _killer;

        [CanBeNull]
        private Stopwatch _lastConnectionAttempt;

        private async Task<bool> EnsureConnectedAsync(bool reconnect) {
            if (_socket == null || !_socket.Connected) {
                if (!reconnect || _lastConnectionAttempt?.ElapsedMilliseconds < 3000) return false;

                Logging.Debug("Close socket before reconnecting…");
                CloseSocket();

                try {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    _killer = KillerOrder.Create(_socket, TimeSpan.FromSeconds(1));
                    await _socket.ConnectTaskAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), OptionSocketPort)).ConfigureAwait(false);
                    Logging.Debug("Connected to CM in-game app");
                    _killer?.Pause();
                } catch (Exception e) {
                    Logging.Warning(e);
                    CloseSocket();
                }
            }

            return _socket != null;
        }

        private static readonly string AvatarId = "current";
        private static readonly string AvatarDefaultId = "none";

        private string _avatarUserId;

        private static IEnumerable<string> GetAvatarFilename() {
            return OverlayAppIds.Select(x => Path.Combine(AcPaths.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), x, "images",
                    $"avatar_{AvatarId}.png"));
        }

        private enum AppResponse : byte {
            Yes = (byte)'y',
            No = (byte)'n',
            None = (byte)'-'
        }

        private readonly byte[] _sendBuffer = new byte[256], _receiveBuffer = new byte[1];
        private Tuple<int, string, byte[]> _lastFirst, _lastSecond;
        private bool _warn;

        private async Task<AppResponse> SendRequestAsync(string firstString, string secondString, byte firstValue, byte secondValue) {
            if (!await EnsureConnectedAsync(true)) return AppResponse.None;

            var offset = 0;
            if (!SetString(firstString, 64, ref _lastFirst)
                    & !SetString(secondString, 32, ref _lastSecond)
                    & !SetByte(firstValue)
                    & !SetByte(secondValue)
                    & string.IsNullOrEmpty(firstString)) {
                return AppResponse.None;
            }

            try {
                _killer?.Delay();
                var sent = await _socket.SendTaskAsync(_sendBuffer, 0, offset).ConfigureAwait(false);
                if (sent != offset) {
                    Warn($"Failed to sent all data, only {sent} out of {offset}");
                    return AppResponse.None;
                }

                _killer?.Delay();
                var received = await _socket.ReceiveTaskAsync(_receiveBuffer).ConfigureAwait(false);
                if (received != 1) {
                    Warn($"Failed to receive data, only {received} out of {_receiveBuffer.Length}");
                    return AppResponse.None;
                }

                _killer?.Pause();
                return (AppResponse)_receiveBuffer[0];
            } catch (ObjectDisposedException e) {
                Logging.Warning(e.Message);
                CloseSocket();
                return AppResponse.None;
            } catch (Exception e) {
                Logging.Error(e);
                CloseSocket();
                return AppResponse.None;
            }

            bool ToBytes(string s, int maxLength, ref Tuple<int, string, byte[]> last, out byte[] result) {
                if (s == null) s = "";
                if (last?.Item2 != s || last.Item1 != offset) {
                    var v = s.Length > maxLength ? s.Substring(0, maxLength) : s;
                    result = Encoding.UTF8.GetBytes(v);
                    if (result.Length > maxLength * 2) {
                        Array.Resize(ref result, maxLength * 2);
                    }
                    last = Tuple.Create(offset, s, result);
                    return true;
                }

                result = last.Item3;
                return false;
            }

            bool SetString(string s, int maxLength, ref Tuple<int, string, byte[]> last) {
                var result = ToBytes(s, maxLength, ref last, out var data);
                if (result) {
                    _sendBuffer[offset] = (byte)data.Length;
                    Array.Copy(data, 0, _sendBuffer, offset + 1, data.Length);
                }
                offset += data.Length + 1;
                return result;
            }

            bool SetByte(byte b) {
                var o = offset++;
                if (_sendBuffer[o] == b) return false;
                _sendBuffer[o] = b;
                return true;
            }

            void Warn(string warning) {
                if (!_warn) {
                    _warn = true;
                    Logging.Warning(warning);
                }
            }
        }

        [ItemNotNull]
        private async Task<string> GetAvatarAsync(CmInGameAppJoinRequestParams joinRequest) {
            try {
                if (_avatarUserId != joinRequest.UserId && joinRequest.AvatarUrl != null) {
                    _avatarUserId = joinRequest.UserId;
                    using (var wc = new WebClient()) {
                        var avatarBytes = await wc.DownloadDataTaskAsync(joinRequest.AvatarUrl).ConfigureAwait(false);
                        foreach (var filename in GetAvatarFilename().Where(x => Directory.Exists(Path.GetDirectoryName(x)))) {
                            await FileUtils.WriteAllBytesAsync(filename, avatarBytes).ConfigureAwait(false);
                        }
                    }

                    return AvatarId;
                }
            } catch (WebException e) {
                Logging.Error(e.Message);
            } catch (Exception e) {
                Logging.Warning(e);
            }

            return AvatarDefaultId;
        }

        private void CloseSocket() {
            _lastConnectionAttempt = Stopwatch.StartNew();
            DisposeHelper.Dispose(ref _socket);
            DisposeHelper.Dispose(ref _killer);
        }

        public void Dispose() {
            _killAfterwards = true;
            HideApp();

            _instance = null;
            _isActive = false;
        }
    }
}