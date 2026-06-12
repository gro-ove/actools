using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using AcManager.Tools.Starters;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Steamworks;

namespace AcManager.Tools.Helpers {
    public static class SteamTicketProvider {
        private static byte[] _ticket;
        private static Stopwatch _ticketAge;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GetTicketBytesImpl() {
            _ticket = new byte[1024];
            var ticketHandle = SteamUser.GetAuthSessionTicket(_ticket, _ticket.Length, out var ticketSize);
            if (ticketHandle != HAuthTicket.Invalid && ticketSize != 0) {
                Application.Current.Exit += (sender, args) => SteamUser.CancelAuthTicket(ticketHandle);
            } else {
                _ticket = null;
            }
        }

        [CanBeNull]
        public static byte[] GetTicketBytes() {
            if (_ticketAge == null || _ticketAge.Elapsed.TotalMinutes > 5) {
                _ticketAge = Stopwatch.StartNew();
                if (SteamStarter.Initialize(MainExecutingFile.Directory, true)) {
                    GetTicketBytesImpl();
                    if (_ticket == null) {
                        Logging.Warning("Failed to generate Steam ticket");
                    }
                } else {
                    Logging.Warning("Failed to initialize Steam API");
                }
            }
            return _ticket;
        }

        [CanBeNull]
        public static string GetTicketHex() {
            var bytes = GetTicketBytes();
            return bytes?.ToLowerCaseHexString();
        }

        public static bool UrlRequiresSteamTicket([NotNull] string url) {
            try {
                return new Uri(url, UriKind.RelativeOrAbsolute).GetQueryParam("auth") == "steam";
            } catch (Exception e) {
                Logging.Warning($"Failed to parse URL: {e}");
                return false;
            }
        }
    }
}