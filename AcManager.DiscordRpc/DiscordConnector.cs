using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.DiscordRpc {
    public class DiscordConnector : IDisposable {
        public static TimeSpan OptionMinReconnectionDelay = TimeSpan.FromSeconds(1);
        public static TimeSpan OptionMaxReconnectionDelay = TimeSpan.FromMinutes(1);
        public static bool OptionVerboseMode = false;

        [CanBeNull]
        public static DiscordConnector Instance { get; private set; }

        public static void Initialize([NotNull] string clientId, IDiscordHandler handler = null) {
            Instance?.Dispose();
            Instance = new DiscordConnector(clientId, handler);
            Instance.RunAsync().Forget();
        }

        [NotNull]
        private readonly string _clientId;
        private readonly int _processId;
        private int? _overrideProcessId;

        [CanBeNull]
        private readonly IDiscordHandler _handler;

        private DiscordConnector(string clientId, IDiscordHandler handler = null) {
            _clientId = clientId;
            _processId = Process.GetCurrentProcess().Id;
            _handler = handler;
        }

        private DiscordConnection _currentConnection;
        private DiscordRichPresence _currentPresence;

        public void Update(DiscordRichPresence presence) {
            _currentPresence = presence;
            if (_currentConnection != null) {
                UpdateSafe(_currentConnection, presence);
            } else {
                Utils.Log("Not yet connected");
            }
        }

        public IDisposable SetAppId(int appId) {
            var oldValue = _overrideProcessId;
            _overrideProcessId = appId;
            return new ActionAsDisposable(() => _overrideProcessId = oldValue);
        }

        private async void UpdateSafe(DiscordConnection connection, DiscordRichPresence presence) {
            try {
                await connection.UpdateAsync(presence, _overrideProcessId ?? _processId);
            } catch (Exception e) {
                Utils.Warn(e.ToString());
            }
        }

        private CancellationTokenSource _currentDelay;

        private async Task RunAsync() {
            var delay = OptionMinReconnectionDelay;

            while (!IsDisposed) {
                try {
                    Utils.Log("(Re)creating connection…");
                    using (var connection = new DiscordConnection(_handler)) {
                        await connection.LaunchAsync(_clientId).ConfigureAwait(false);
                        delay = OptionMinReconnectionDelay;

                        _currentConnection = connection;
                        if (_currentPresence != null) {
                            await connection.UpdateAsync(_currentPresence, _overrideProcessId ?? _processId).ConfigureAwait(false);
                        }

                        await connection.ListenAsync().ConfigureAwait(false);
                        if (connection.IsDisposed) continue;
                    }
                } catch (TimeoutException e) {
                    Utils.Log(e.Message);
                } catch (IOException e) {
                    Utils.Warn(e.Message);
                } catch (DiscordException e) {
                    Utils.Warn(e.Message);
                } catch (Exception e) {
                    Utils.Warn(e.ToString());
                }

                _currentConnection = null;
                using (_currentDelay = new CancellationTokenSource()) {
                    await Task.Delay(delay, _currentDelay.Token).ConfigureAwait(false);
                }

                _currentDelay = null;
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, OptionMaxReconnectionDelay.TotalSeconds));
            }
        }

        public bool IsDisposed { get; private set; }

        public void Dispose() {
            if (IsDisposed) return;
            Utils.Log("Dispose");
            IsDisposed = true;
            _currentConnection?.Dispose();
        }
    }

    public class DiscordException : Exception {
        public DiscordException(string message) : base(message) { }
    }
}