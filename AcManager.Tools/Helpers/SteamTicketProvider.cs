using System;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Steamworks;

namespace AcManager.Tools.Helpers {
    /// <summary>
    /// Provides Steam auth tickets for download requests. Used when URLs contain auth=steam
    /// to prove identity to servers that validate via Steam (BeginAuthSession or Web API).
    /// </summary>
    public static class SteamTicketProvider {
        /// <summary>
        /// Query param that indicates the server requires a Steam ticket.
        /// When present in URL, CM adds Authorization: Bearer &lt;hex-ticket&gt; to the request.
        /// </summary>
        public const string AuthParam = "auth=steam";

        /// <summary>
        /// Generates a Steam auth session ticket and returns it as a hex string for Bearer auth.
        /// Returns null if Steam is not initialized or ticket generation fails.
        /// Uses GetAuthSessionTicket (game session). Compatible with Steamworks.NET 5.x+ (3-param overload).
        /// </summary>
        [CanBeNull]
        public static string GetTicketHex() {
            try {
                if (!SteamAPI.Init()) return null;

                var ticketBuffer = new byte[1024];
                var ticketHandle = SteamUser.GetAuthSessionTicket(ticketBuffer, ticketBuffer.Length, out var ticketSize);
                if (ticketHandle == HAuthTicket.Invalid || ticketSize == 0) return null;

                try {
                    var ticketBytes = new byte[ticketSize];
                    Array.Copy(ticketBuffer, ticketBytes, (int)ticketSize);
                    return BitConverter.ToString(ticketBytes).Replace("-", "");
                } finally {
                    SteamUser.CancelAuthTicket(ticketHandle);
                }
            } catch (Exception e) {
                Logging.Warning("Steam ticket generation failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Checks if the URL contains the auth=steam param (case-insensitive).
        /// </summary>
        public static bool UrlRequiresSteamTicket([NotNull] string url) {
            if (string.IsNullOrEmpty(url)) return false;
            var queryStart = url.IndexOf('?');
            if (queryStart < 0) return false;
            var query = url.Substring(queryStart);
            return query.IndexOf(AuthParam, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
