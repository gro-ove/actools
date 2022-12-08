using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.AcPlugins.CspCommands;
using AcManager.Tools.AcPlugins.Messages;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.ServerPlugins {
    public class HandshakePlugin : AcServerPlugin {
        public Action<string> LogFn;

        private readonly Dictionary<byte, Tuple<string, List<byte[]>>> _handstakeState = new Dictionary<byte, Tuple<string, List<byte[]>>>();

        public override void OnCarUpdate(MsgCarUpdate msg) {
            if (_handstakeState.ContainsKey(msg.CarId)) return;
            _handstakeState[msg.CarId] = Tuple.Create(MathUtils.Random(int.MaxValue).ToString(), new List<byte[]>());
            PluginManager.SendCspCommand(msg.CarId, new CommandSignatureIn { Value = _handstakeState[msg.CarId].Item1 });
            LogFn($"Handshake sent: {msg.CarId} (key: {_handstakeState[msg.CarId].Item1})");
        }

        public override void OnConnectionClosed(MsgConnectionClosed msg) {
            _handstakeState.Remove(msg.CarId);
        }

        public override void OnChatMessage(MsgChat msg) {
            var cspMessage = CspCommandsUtils.TryDeserialize(msg.Message);
            if (cspMessage != null && cspMessage.Key == 3 && cspMessage.TryParse(out CommandSignatureOut o)) {
                var value = _handstakeState.GetValueOrDefault(msg.CarId);
                if (value == null) {
                    LogFn($"Unexpected handshake response: {msg.CarId}, packet: {o.Value[0]} out of {o.Value[1]} ({o.Value[2]} bytes)");
                    return;
                }
                
                LogFn($"Handshake response: {o.UniqueKey}, packet: {o.Value[0]} out of {o.Value[1]} ({o.Value[2]} bytes)");
                value.Item2.Add(o.Value.Slice(3, o.Value[2]));
                if (o.Value[0] + 1 == o.Value[1]) {
                    var signature = value.Item2.SelectMany(x => x).ToArray();
                    LogFn($"Signature arrived: {signature.ToCutBase64()} ({signature.Length} bytes)");
                    VerifySignature(value.Item1, signature, v => LogFn($"Signature tested: {v}"));
                    _handstakeState[msg.CarId] = null;
                }
            }
        }

        private static void VerifySignature(string message, byte[] signature, Action<bool> callback) {
            Task.Run(() => {
                var ret = false;
                try {
                    var process = ProcessExtension.Start(@"C:\Data\Desktop\8\test-ac-signature.exe", new[] {
                        Encoding.ASCII.GetBytes(message).Concat(signature).ToArray().ToCutBase64()
                    }, new ProcessStartInfo {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                    process.WaitForExit();
                    ret = (process.ExitCode == 0);
                } catch (Exception e) {
                    Logging.Warning(e);
                }
                callback(ret);
            });
        }
    }
}