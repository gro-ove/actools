using System;
using System.Windows;
using AcManager.Tools.Starters;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Steamworks;

namespace AcManager.Tools.Helpers {
    public static class SteamTicketProvider {
        private static byte[] _ticket;

        [CanBeNull]
        private static byte[] GetTicketBytes() {
            if (_ticket == null) {
                if (SteamStarter.Initialize(MainExecutingFile.Directory, true)) {
                    _ticket = new byte[1024];
                    var ticketHandle = SteamUser.GetAuthSessionTicket(_ticket, _ticket.Length, out var ticketSize);
                    if (ticketHandle != HAuthTicket.Invalid && ticketSize != 0) {
                        Application.Current.Exit += (sender, args) => SteamUser.CancelAuthTicket(ticketHandle);
                    } else {
                        _ticket = null;
                    }
                }
                if (_ticket == null) {
                    Logging.Warning("Failed to generate Steam ticket");
                    _ticket = new byte[0];
                }
            }
            return _ticket.Length == 0 ? null : _ticket;
        }

        [CanBeNull]
        public static string GetTicketHex() {
            var bytes = GetTicketBytes();
            return bytes == null ? null : BitConverter.ToString(bytes).Replace("-", "");
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