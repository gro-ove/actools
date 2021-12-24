using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.AcPlugins.Extras;
using AcManager.Tools.AcPlugins.Messages;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.ServerPlugins {
    public class CmServerPlugin : AcServerPlugin, IAcLeaderboardCommandHelper {
        private readonly Action<ServerPresetObject.LogMessageType, string> _logFn;
        private readonly int _capacity;
        private bool _disposed;
        private AcLeaderboard _leaderboard;

        public CmServerPlugin(Action<ServerPresetObject.LogMessageType, string> logFn, int capacity) {
            _logFn = logFn;
            _capacity = capacity;
            MonitorDisconnectedAsync().Ignore();
        }

        public override void OnInit() {
            var capacity = Math.Max(PluginManager.CurrentSession.MaxClients, _capacity);
            _leaderboard = new AcLeaderboard(capacity, this) { ResetBestLapTimeOnNewSession = true };
        }

        private async Task MonitorDisconnectedAsync() {
            while (!_disposed) {
                _leaderboard?.CheckDisconnected();
                await Task.Delay(TimeSpan.FromSeconds(10d)).ConfigureAwait(false);
            }
        }

        public override void Dispose() {
            _disposed = true;
        }

        [CanBeNull]
        public AcLeaderboard Leaderboard => _leaderboard;

        public class ChatMessage {
            [CanBeNull]
            public AcDriverLeaderboardDetails Author { get; }

            public string Message { get; }

            public ChatMessage(AcDriverLeaderboardDetails author, MsgChat msg) {
                Author = author;
                Message = msg.Message;
            }

            public ChatMessage(string serverMsg, bool isSystemCommand, bool isDirectAddress) {
                Message = serverMsg;
                IsSystemCommand = isSystemCommand;
                IsDirectAddress = isDirectAddress;
            }

            public bool IsSystemCommand { get; }

            public bool IsDirectAddress { get; }
        }

        public class ChatData : NotifyPropertyChanged {
            private readonly Func<string, Task> _sendCallback;

            public ChatData(Func<string, Task> sendCallback) {
                _sendCallback = sendCallback;
            }

            public BetterObservableCollection<ChatMessage> ChatMessages { get; } = new BetterObservableCollection<ChatMessage>();

            private string _chatText;

            public string ChatText {
                get => _chatText;
                set => Apply(value, ref _chatText);
            }

            private AsyncCommand _sendChatCommand;

            public AsyncCommand SendChatCommand => _sendChatCommand ?? (_sendChatCommand = new AsyncCommand(() => {
                var text = ChatText;
                ChatText = null;
                return _sendCallback(text);
            }));
        }

        private void SendMessage(string msg) {
            if (msg.StartsWith(@"/")) {
                PluginManager.AdminCommand(msg);
                _chat?.ChatMessages.Add(new ChatMessage(msg, true, false));
            } else {
                var directMessage = Regex.Match(msg, @"^#(\w+)\b\s*(.+)");
                if (directMessage.Success) {
                    var id = Leaderboard?.Leaderboard.FindIndex(x => x.Driver?.DriverName == directMessage.Groups[1].Value) ?? -1;
                    if (id == -1) id = directMessage.Groups[1].Value.As(255);
                    PluginManager.SendChatMessage((byte)id, directMessage.Groups[2].Value);
                    _chat?.ChatMessages.Add(new ChatMessage(msg, false, true));
                } else {
                    PluginManager.BroadcastChatMessage(msg);
                    _chat?.ChatMessages.Add(new ChatMessage(msg, false, false));
                }
            }
        }

        private ChatData _chat;

        [NotNull]
        public ChatData Chat => _chat ?? (_chat = new ChatData(async msg => {
            SendMessage(msg);
            await Task.Delay(100);
        }));

        public override void OnConnected() {
            _logFn(ServerPresetObject.LogMessageType.Debug, "Connected");
        }

        public override void OnDisconnected() {
            _logFn(ServerPresetObject.LogMessageType.Debug, "Disconnected");
        }

        public override bool OnCommandEntered(string cmd) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Command: `{cmd}`");
            return false;
        }

        public override void OnSessionInfo(MsgSessionInfo msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Session info: {msg.Name}");
            ActionExtension.InvokeInMainThreadAsync(() => _leaderboard.OnSessionInfo(msg));
        }

        public override void OnNewSession(MsgSessionInfo msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"New session: {msg.Name}");
            ActionExtension.InvokeInMainThreadAsync(() => _leaderboard.OnSessionInfo(msg));
        }

        public override void OnSessionEnded(MsgSessionEnded msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Session ended: {msg.ReportFileName}");
        }

        public override void OnNewConnection(MsgNewConnection msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"New connection: {msg.DriverGuid}");
        }

        public override void OnConnectionClosed(MsgConnectionClosed msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Connection closed: {msg.DriverGuid}");
            ActionExtension.InvokeInMainThreadAsync(() => _leaderboard.OnConnectionClosed(msg));
        }

        public override void OnCarInfo(MsgCarInfo msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Car info: {msg.DriverGuid}");
            ActionExtension.InvokeInMainThreadAsync(() => {
                ServerGuidsStorage.RegisterUserName(msg.DriverGuid, msg.DriverName);
                _leaderboard.OnCarInfo(msg);
            });
        }

        public override void OnCarUpdate(MsgCarUpdate msg) {
            ActionExtension.InvokeInMainThreadAsync(() => _leaderboard.OnCarUpdate(msg));
        }

        public override void OnCollision(MsgClientEvent msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Collision: {msg.CarId}, {msg.OtherCarId}, velocity: {msg.RelativeVelocity}");
            ActionExtension.InvokeInMainThreadAsync(() => _leaderboard.OnCollision(msg));
        }

        public override void OnLapCompleted(MsgLapCompleted msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Lap completed: {msg.LapTime}");
            ActionExtension.InvokeInMainThreadAsync(() => _leaderboard.OnLapCompleted(msg, PluginManager.CurrentSession.LapCount));
        }

        public override void OnClientLoaded(MsgClientLoaded msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Client loaded: {msg.CarId}");
        }

        public override void OnChatMessage(MsgChat msg) {
            if (msg.Message.StartsWith("\t\t\t\t$CSP0:")) return;
            ActionExtension.InvokeInMainThreadAsync(() => Chat.ChatMessages.Add(new ChatMessage(_leaderboard.GetDetails(msg.CarId), msg)));
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Chat message: {msg.Message}");
        }

        public override void OnProtocolVersion(MsgVersionInfo msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Protocol version: {msg.Version}");
        }

        public override void OnServerError(MsgError msg) {
            _logFn(ServerPresetObject.LogMessageType.Debug, $"Server error: {msg.ErrorMessage}");
        }

        public override void OnAcServerTimeout() {
            _logFn(ServerPresetObject.LogMessageType.Debug, "Server timeout");
        }

        void IAcLeaderboardCommandHelper.KickPlayer(int carId) {
            var driverInfo = _leaderboard.GetDetails(carId);
            if (MessageDialog.Show($"Are you sure you want to kick {driverInfo?.Driver?.CarName ?? "car #" + carId}?", "Kick driver?",
                    MessageDialogButton.YesNo) == MessageBoxResult.Yes) {
                SendMessage($"/kick_id {carId}");
            }
        }

        void IAcLeaderboardCommandHelper.BanPlayer(int carId) {
            var driverInfo = _leaderboard.GetDetails(carId);
            if (MessageDialog.Show($"Are you sure you want to ban {driverInfo?.Driver?.CarName ?? "car #" + carId}?", "Ban driver?",
                    MessageDialogButton.YesNo) == MessageBoxResult.Yes) {
                SendMessage($"/ban_id {carId}");
            }
        }

        void IAcLeaderboardCommandHelper.MentionInChat(int carId) {
            Chat.ChatText = $@"@{Leaderboard?.GetDetails(carId)?.Driver?.DriverName ?? carId.ToInvariantString()} ";
        }

        void IAcLeaderboardCommandHelper.SendMessageDirectly(int carId) {
            Chat.ChatText = $@"#{Leaderboard?.GetDetails(carId)?.Driver?.DriverName ?? carId.ToInvariantString()} ";
        }
    }
}