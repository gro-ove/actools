using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Api {
    public static class StringsHelper {
        public static IEnumerable<int> ToPortsDiapason(this string s) {
            return s.ToDiapason(1025, 65535);
        }
    }

    public partial class KunosApiProvider {
        public static int OptionLanSocketTimeout = 200;
        public static int OptionLanPollTimeout = 100;

        private class FoundServerInformation {
            public string Ip;
            public int Port;
        }

        private static FoundServerInformation BroadcastPing(IPAddress broadcastAddress, int port) {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                SendTimeout = OptionLanSocketTimeout,
                ReceiveTimeout = OptionLanSocketTimeout,
                Blocking = false
            }) {
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var buffer = new byte[3];
                try {
                    socket.SendTo(BitConverter.GetBytes(200), SocketFlags.DontRoute, new IPEndPoint(broadcastAddress, port));
                    if (socket.Poll(OptionLanPollTimeout * 1000, SelectMode.SelectRead)) {
                        socket.ReceiveFrom(buffer, ref remoteEndPoint);
                    }
                } catch (SocketException) {
                    return null;
                }

                if (buffer[0] != 200 || buffer[1] + buffer[2] <= 0) {
                    return null;
                }

                var foundServer = remoteEndPoint as IPEndPoint;
                if (foundServer == null) {
                    return null;
                }

                return new FoundServerInformation {
                    Ip = foundServer.Address.ToString(),
                    Port = BitConverter.ToInt16(buffer, 1)
                };
            }
        }

        private static IEnumerable<IPAddress> GetBroadcastAddresses() {
            var ignored = SettingsHolder.Online.IgnoredInterfaces.ToList();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => !ignored.Contains(x.Id)).ToList();

            return from address in Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                   let mask = interfaces.SelectMany(x => x.GetIPProperties().UnicastAddresses)
                           .FirstOrDefault(x => x.Address.AddressFamily == AddressFamily.InterNetwork && Equals(x.Address, address))?.IPv4Mask
                   where mask != null
                   select address.GetBroadcastAddress(mask);
        }

        public static ServerInformation[] TryToGetLanList(IEnumerable<int> ports) {
            var result = new List<ServerInformation>();
            Parallel.ForEach(
                    GetBroadcastAddresses()
                    .SelectMany(x => ports.Select(y => new {
                        BroadcastIp = x,
                        Port = y
                    })),
                    (entry, ipLoopState) => {
                        var found = BroadcastPing(entry.BroadcastIp, entry.Port);
                        if (found == null) return;

                        try {
                            var information = TryToGetInformationDirect(found.Ip, found.Port);
                            if (information == null) return;

                            Logging.Write($"[LAN SERVERS] Found: {information.Name} ({found.Ip}:{found.Port})");
                            information.IsLan = true;
                            result.Add(information);
                        } catch (Exception e) {
                            Logging.Write("[LAN SERVERS] Error: " + e);
                        }
                    });
            return result.ToArray();
        }

        public static ServerInformation[] TryToGetLanList() {
            return TryToGetLanList(SettingsHolder.Online.LanPortsEnumeration.ToPortsDiapason());
        }

        public static void TryToGetLanList(Action<ServerInformation> foundCallback, IEnumerable<int> ports) {
            Parallel.ForEach(
                    GetBroadcastAddresses()
                    .SelectMany(x => ports.Select(y => new {
                        BroadcastIp = x,
                        Port = y
                    })),
                    (entry, ipLoopState) => {
                        var found = BroadcastPing(entry.BroadcastIp, entry.Port);
                        if (found == null) return;

                        try {
                            var information = TryToGetInformationDirect(found.Ip, found.Port);
                            if (information == null) return;

                            information.IsLan = true;
                            Application.Current.Dispatcher.InvokeAsync(() => foundCallback(information));
                        } catch (Exception e) {
                            Logging.Write("[LAN SERVERS] Error: " + e);
                        }
                    });
        }

        public static void TryToGetLanList(Action<ServerInformation> foundCallback) {
            TryToGetLanList(foundCallback, SettingsHolder.Online.LanPortsEnumeration.ToPortsDiapason());
        }
    }
}
