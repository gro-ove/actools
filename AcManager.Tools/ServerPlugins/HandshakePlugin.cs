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
        public class UserHandshakeState {
            public string ExpectedValue;
            public List<byte[]> CollectedData = new List<byte[]>();
        }
        
        public Action<string> LogFn;

        private readonly Dictionary<byte, UserHandshakeState> _handstakeState = new Dictionary<byte, UserHandshakeState>();

        public override void OnCarUpdate(MsgCarUpdate msg) {
            if (_handstakeState.ContainsKey(msg.CarId)) return;
            _handstakeState[msg.CarId] = new UserHandshakeState { ExpectedValue =  MathUtils.Random(int.MaxValue).ToString() };
            PluginManager.SendCspCommand(msg.CarId, new CommandSignatureIn { Value = _handstakeState[msg.CarId].ExpectedValue });
            LogFn($"Handshake sent: {msg.CarId} (key: {_handstakeState[msg.CarId].ExpectedValue})");
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
                value.CollectedData.Add(o.Value.Slice(3, o.Value[2]));
                if (o.Value[0] + 1 == o.Value[1]) { // if packet ID + 1 is the same as a number of expected packets 
                    var signature = value.CollectedData.SelectMany(x => x).ToArray();
                    LogFn($"Signature arrived: {signature.ToCutBase64()} ({signature.Length} bytes)");
                    VerifySignature(value.ExpectedValue, signature, v => LogFn($"Signature tested: {v}"));
                    _handstakeState[msg.CarId] = null;
                }
            }
        }

        private static void VerifySignature(string message, byte[] signature, Action<bool> callback) {
            Task.Run(() => {
                var ret = false;
                try {
                    var process = ProcessExtension.Start(@"C:\Development\actools\test-ac-signature.exe", new[] {
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