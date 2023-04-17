using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.AcPlugins.CspCommands;
using AcManager.Tools.AcPlugins.Messages;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.ServerPlugins {
    public class HandshakeVerificationPlugin : AcServerPlugin {
        private static string GetChecksum(string data) {
            using (var sha = SHA256.Create()) {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(data)).ToCutBase64();
            }
        }
        
        private static string GetFileChecksum(string filename) {
            var ret = "?";
            try {
                using (var sha = SHA256.Create())
                using (var stream = File.Open(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) {
                    ret = sha.ComputeHash(stream).ToCutBase64();
                }
            } catch (Exception e) {
                Logging.Debug($"Failed to read {filename} data: {e.Message}");
            }
            return ret;
        }
        
        public class UserHandshakeState {
            public string ExpectedValue;
            public readonly List<byte[]> CollectedData = new List<byte[]>();
            public readonly Stopwatch Timer = Stopwatch.StartNew();
        }

        private readonly string _dataChecksum;

        public HandshakeVerificationPlugin(string cspConfigData, TrackObjectBase track, IEnumerable<CarObject> cars) {
            var checksums = new StringBuilder();
            checksums.Append(GetChecksum(cspConfigData));
            checksums.Append(GetFileChecksum(Path.Combine(AcRootDirectory.Instance.RequireValue, "system\\data\\surfaces.ini")));
            checksums.Append(GetFileChecksum(Path.Combine(track.DataDirectory, "surfaces.ini")));
            checksums.Append(GetFileChecksum(Path.Combine(track.DataDirectory, "drs_zones.ini")));
            if (!File.Exists(track.ModelsFilename)) {
                checksums.Append(GetFileChecksum(Path.Combine(track.Location, track.Id + ".kn5")));
            } else {
                checksums.Append(GetFileChecksum(track.ModelsFilename));
                foreach (var s in new IniFile(track.ModelsFilename).GetSections("MODEL").Select(x => x.GetNonEmpty("FILE")).NonNull()) {
                    checksums.Append(GetFileChecksum(Path.Combine(track.Location, s)));
                }
            }
            foreach (var car in cars.Distinct()) {
                if (car.AcdData?.IsPacked == true) {
                    checksums.Append(GetFileChecksum(Path.Combine(car.Location, DataWrapper.PackedFileName)));
                }
            }

            _dataChecksum = GetChecksum(checksums.ToString());
        }
        
        public Action<string> LogFn;

        public void KickDriver(byte carId) {
            _handshakeState[carId] = null;
            PluginManager.SendCspCommand(carId, new CommandOverrideBanMessage { Value = "Failed to verify data integrity" });
            PluginManager.RequestKickDriverById(carId);
        }

        private readonly Dictionary<byte, UserHandshakeState> _handshakeState = new Dictionary<byte, UserHandshakeState>();

        public override void OnCarUpdate(MsgCarUpdate msg) {
            if (_handshakeState.TryGetValue(msg.CarId, out var value)) {
                if (value != null && value.Timer.Elapsed > TimeSpan.FromSeconds(10d)) {
                    KickDriver(msg.CarId);
                }
                return;
            }
            _handshakeState[msg.CarId] = new UserHandshakeState { ExpectedValue =  MathUtils.Random(int.MaxValue).ToString() };
            PluginManager.SendCspCommand(msg.CarId, new CommandSignatureVerificationIn { Value = _handshakeState[msg.CarId].ExpectedValue });
            LogFn($"Handshake with verification sent: {msg.CarId} (key: {_handshakeState[msg.CarId].ExpectedValue})");
        }

        public override void OnConnectionClosed(MsgConnectionClosed msg) {
            _handshakeState.Remove(msg.CarId);
        }

        public override void OnChatMessage(MsgChat msg) {
            var cspMessage = CspCommandsUtils.TryDeserialize(msg.Message);
            if (cspMessage != null && cspMessage.Key == 5 && cspMessage.TryParse(out CommandSignatureVerificationOut o)) {
                var value = _handshakeState.GetValueOrDefault(msg.CarId);
                if (value == null) {
                    LogFn($"Unexpected handshake response: {msg.CarId}, packet: {o.Value[0]} out of {o.Value[1]} ({o.Value[2]} bytes)");
                    KickDriver(msg.CarId);
                    return;
                }
                
                LogFn($"Handshake response: {o.UniqueKey}, packet: {o.Value[0]} out of {o.Value[1]} ({o.Value[2]} bytes)");
                value.CollectedData.Add(o.Value.Slice(3, o.Value[2]));
                if (o.Value[0] + 1 == o.Value[1]) { // if packet ID + 1 is the same as a number of expected packets 
                    var signature = value.CollectedData.SelectMany(x => x).ToArray();
                    LogFn($"Signature arrived: {signature.ToCutBase64()} ({signature.Length} bytes)");
                    VerifySignature(value.ExpectedValue + _dataChecksum, signature, v => {
                        LogFn($"Signature tested: {v}");
                        if (!v) {
                            KickDriver(msg.CarId);
                        }
                    });
                    _handshakeState[msg.CarId] = null;
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