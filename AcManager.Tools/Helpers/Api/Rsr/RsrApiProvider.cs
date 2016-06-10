using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Api.Rsr {
    public class RsrEventInformation {
        public string CarId;
        public string TrackId;
        public string TrackLayoutId;
    }

    public static class RsrApiProvider {
        private static string GetUrl(int eventId) {
            return $"http://www.radiators-champ.com/RSRLiveTiming/index.php?page=event_rank&eventId={eventId.ToInvariantString()}";
        }

        [ItemCanBeNull]
        public static async Task<RsrEventInformation> GetEventInformationAsync(int eventId, CancellationToken cancellation = default(CancellationToken)) {
            try {
                using (var client = new WebClient { Headers = {
                    [HttpRequestHeader.UserAgent] = "Assetto Corsa Launcher",
                    ["X-User-Agent"] = CmApiProvider.UserAgent
                } }) {
                    var result = await client.DownloadDataTaskAsync(GetUrl(eventId));
                    if (cancellation.IsCancellationRequested) return null;



                    return null;
                }
            } catch (Exception e) {
                if (!cancellation.IsCancellationRequested) {
                    Logging.Warning($"[RsrApiProvider] Cannot get {eventId}: " + e);
                }

                return null;
            }
        }
    }
}
