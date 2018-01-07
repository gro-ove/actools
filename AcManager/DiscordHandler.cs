using System;
using System.Text;
using System.Threading;
using System.Windows;
using AcManager.DiscordRpc;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager {
    public class DiscordHandler : IDiscordHandler {
        public bool HandlesJoin => true;
        public bool HandlesSpectate => false;
        public bool HandlesJoinRequest => true;

        public void OnFirstConnectionEstablished(string appId) {
            CustomUriSchemeHelper.RegisterDiscordClass(appId);
        }

        public void JoinRequest(DiscordJoinRequest request, CancellationToken cancellation, Action<DiscordJoinRequestReply> callback) {
            ActionExtension.InvokeInMainThreadAsync(async () => {
                try {
                    var dialog = new DiscordJoinRequestDialog(request);
                    cancellation.Register(() => ActionExtension.InvokeInMainThreadAsync(() => {
                        if (!dialog.IsLoaded) {
                            dialog.Loaded += (s, e) => dialog.Close();
                        } else if (dialog.IsVisible) {
                            try {
                                dialog.Close();
                            } catch {
                                // ignored
                            }
                        }
                    }));

                    switch (await dialog.ShowAndWaitAsync()) {
                        case MessageBoxResult.Yes:
                            callback(DiscordJoinRequestReply.Yes);
                            break;
                        case MessageBoxResult.No:
                            callback(DiscordJoinRequestReply.No);
                            break;
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            });
        }

        public void Spectate(string secret) {
            Logging.Debug(secret);
        }

        public void Join(string secret) {
            ActionExtension.InvokeInMainThreadAsync(async () => {
                try {
                    var j = JObject.Parse(Decrypt(secret, JoinKey));
                    var ip = (string)j["i"];
                    var port = (int)j["p"];
                    var password = (string)j["w"];
                    Logging.Debug($"{ip}:{port}");
                    await ArgumentsHandler.JoinInvitation(ip, port, password);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t join invitation", e);
                }
            });
        }

        private static readonly string JoinKey = "lwtwhevd947orz3reg47f05k";
        private static readonly string MatchKey = "g00s0kc70be133klull6uo1s";

        private static void Xor(byte[] data, byte[] key) {
            int dataLength = data.Length, keyLength = key.Length;
            for (int i = 0, k = 0; i < dataLength; i++, k++) {
                if (k == keyLength) k = 0;
                data[i] ^= key[k];
            }
        }

        private static string Encrypt(string s, string key) {
            var data = Encoding.UTF8.GetBytes(s);
            Xor(data, Encoding.UTF8.GetBytes(key));
            return data.ToCutBase64();
        }

        private static string Decrypt(string s, string key) {
            var data = s.FromCutBase64();
            Xor(data, Encoding.UTF8.GetBytes(key));
            return data == null ? null : Encoding.UTF8.GetString(data);
        }

        public static string GetJoinSecret(string ip, int port, string password) {
            return Encrypt(new JObject { ["i"] = ip, ["p"] = port, ["w"] = password }.ToString(Formatting.None), JoinKey);
        }

        public static string GetMatchSecret(string ip, int port, string password) {
            return Encrypt(new JObject { ["i"] = ip, ["p"] = port, ["w"] = password }.ToString(Formatting.None), MatchKey);
        }
    }
}