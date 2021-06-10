using System;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.AcPlugins.Extras;
using AcManager.Tools.AcPlugins.Messages;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        public class CmServerPlugin : AcServerPlugin {
            private readonly Action<LogMessageType, string> _logFn;
            private readonly int _capacity;
            private AcLeaderboard _leaderboard;

            public event EventHandler Updated;

            public CmServerPlugin(Action<LogMessageType, string> logFn, int capacity) {
                _logFn = logFn;
                _capacity = capacity;
            }

            public override void OnBulkCarUpdateFinished() {
                Updated?.Invoke(this, EventArgs.Empty);
            }

            public override void OnInit() {
                var capacity = Math.Max(PluginManager.CurrentSession.MaxClients, _capacity);
                _leaderboard = new AcLeaderboard(capacity) { ResetBestLapTimeOnNewSession = true };
            }

            [CanBeNull]
            public AcLeaderboard Leaderboard => _leaderboard;

            public class ChatMessage {
                public string Author { get; }
                public string Message { get; }

                public ChatMessage(MsgChat msg) {
                    Author = msg.CarId.ToInvariantString();
                    Message = msg.Message;
                }
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

            private ChatData _chat;

            public ChatData Chat => _chat ?? (_chat = new ChatData(async msg => {
                PluginManager.SendChatMessage(255, msg);
                await Task.Delay(500);
            }));

            public override void OnConnected() {
                _logFn(LogMessageType.Debug, "CM plugin connected");
            }

            public override void OnDisconnected() {
                _logFn(LogMessageType.Debug, "CM plugin disconnected");
            }

            public override bool OnCommandEntered(string cmd) {
                _logFn(LogMessageType.Debug, $"CM plugin command: `{cmd}`");
                return false;
            }

            public override void OnSessionInfo(MsgSessionInfo msg) {
                _logFn(LogMessageType.Debug, $"CM plugin session info: {msg.Name}");
                _leaderboard.OnSessionInfo(msg);
            }

            public override void OnNewSession(MsgSessionInfo msg) {
                _logFn(LogMessageType.Debug, $"CM plugin new session: {msg.Name}");
                _leaderboard.OnSessionInfo(msg);
            }

            public override void OnSessionEnded(MsgSessionEnded msg) {
                _logFn(LogMessageType.Debug, $"CM plugin session ended: {msg.ReportFileName}");
            }

            public override void OnNewConnection(MsgNewConnection msg) {
                _logFn(LogMessageType.Debug, $"CM plugin new connection: {msg.DriverGuid}");
            }

            public override void OnConnectionClosed(MsgConnectionClosed msg) {
                _logFn(LogMessageType.Debug, $"CM plugin connection closed: {msg.DriverGuid}");
                _leaderboard.OnConnectionClosed(msg);
            }

            public override void OnCarInfo(MsgCarInfo msg) {
                _logFn(LogMessageType.Debug, $"CM plugin car info: {msg.DriverGuid}");
                _leaderboard.OnCarInfo(msg);
            }

            public override void OnCarUpdate(MsgCarUpdate msg) {
                _logFn(LogMessageType.Debug, $"CM plugin car update: {msg.WorldPosition}");
                _leaderboard.OnCarUpdate(msg);
            }

            public override void OnCollision(MsgClientEvent msg) {
                _logFn(LogMessageType.Debug, $"CM plugin collision: {msg.CarId}, {msg.OtherCarId}");
            }

            public override void OnLapCompleted(MsgLapCompleted msg) {
                _logFn(LogMessageType.Debug, $"CM plugin lap completed: {msg.LapTime}");
                _leaderboard.OnLapCompleted(msg, PluginManager.CurrentSession.LapCount);
            }

            public override void OnClientLoaded(MsgClientLoaded msg) {
                _logFn(LogMessageType.Debug, $"CM plugin client loaded: {msg.CarId}");
            }

            public override void OnChatMessage(MsgChat msg) {
                Chat.ChatMessages.Add(new ChatMessage(msg));
                _logFn(LogMessageType.Debug, $"CM plugin chat message: {msg.Message}");
            }

            public override void OnProtocolVersion(MsgVersionInfo msg) {
                _logFn(LogMessageType.Debug, $"CM plugin protocol version: {msg.Version}");
            }

            public override void OnServerError(MsgError msg) {
                _logFn(LogMessageType.Debug, $"CM plugin server error: {msg.ErrorMessage}");
            }

            public override void OnAcServerTimeout() {
                _logFn(LogMessageType.Debug, "CM plugin server timeout");
            }
        }
    }
}