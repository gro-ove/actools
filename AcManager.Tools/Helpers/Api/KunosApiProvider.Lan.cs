using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Managers.Online;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

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

        private class Progress {
            public int Total;
            public int Current;
        }

        [CanBeNull]
        private static ServerInformationComplete TryToGetInformationDirect(string ip, int portC) {
            var requestUri = $@"http://{ip}:{portC}/INFO";

            try {
                return PrepareLoadedDirectly(
                        JsonConvert.DeserializeObject<ServerInformationComplete>(LoadAsync(requestUri, OptionDirectRequestTimeout).Result), ip);
            } catch (WebException e) {
                Logging.Warning($"Cannot get server information: {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"Cannot get server information: {requestUri}\n{e}");
                return null;
            }
        }

        private static void TryToGetLanList(ItemAddCallback<ServerInformation> foundCallback, IEnumerable<int> ports, [CanBeNull] Progress progress,
                CancellationToken cancellation) {
            var addresses = GetBroadcastAddresses().ToList();

            // ReSharper disable PossibleMultipleEnumeration
            if (progress != null) {
                progress.Total = ports.Count() * addresses.Count;
                progress.Current = 0;
            }

            var entries = addresses.SelectMany(x => ports.Select(y => new {
                BroadcastIp = x,
                Port = y
            }));
            // ReSharper enable PossibleMultipleEnumeration

            try {
                Parallel.ForEach(entries, new ParallelOptions {
                    CancellationToken = cancellation,
                    MaxDegreeOfParallelism = (Environment.ProcessorCount - 1).Clamp(1, 4)
                }, (entry, ipLoopState) => {
                    cancellation.ThrowIfCancellationRequested();

                    var found = BroadcastPing(entry.BroadcastIp, entry.Port);
                    if (found != null) {
                        try {
                            var information = TryToGetInformationDirect(found.Ip, found.Port);
                            if (information == null) return;

                            information.IsLan = true;
                            ActionExtension.InvokeInMainThreadAsync(() => foundCallback(information));
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }

                    if (progress != null) {
                        Interlocked.Increment(ref progress.Current);
                    }
                });
            } catch (Exception e) when (e.IsCanceled()) {}
        }

        public static void TryToGetLanList(ItemAddCallback<ServerInformation> foundCallback, IEnumerable<int> ports) {
            TryToGetLanList(foundCallback, ports, null, default(CancellationToken));
        }

        public static void TryToGetLanList(ItemAddCallback<ServerInformation> foundCallback) {
            TryToGetLanList(foundCallback, SettingsHolder.Online.LanPortsEnumeration.ToPortsDiapason(), null, default(CancellationToken));
        }

        public static async Task TryToGetLanListAsync(ItemAddCallback<ServerInformation> foundCallback, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var holder = progress == null ? null : new Progress();
            DispatcherTimer timer = null;

            try {
                if (holder != null) {
                    timer = new DispatcherTimer(DispatcherPriority.Background, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher) {
                        Interval = TimeSpan.FromSeconds(0.1),
                        IsEnabled = true
                    };

                    timer.Tick += (sender, args) => {
                        progress.Report(new AsyncProgressEntry($"Scanned {holder.Current} of {holder.Total}", holder.Current, holder.Total));
                    };
                }

                await Task.Run(() => {
                    TryToGetLanList(foundCallback, SettingsHolder.Online.LanPortsEnumeration.ToPortsDiapason(), holder, cancellation);
                }, cancellation);
            } finally {
                if (holder != null && timer != null) {
                    timer.IsEnabled = false;
                }
            }
        }
    }
}
