using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using AcManager.Internal;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api {
    public static class SteamWebProvider {
        private const string RequestStatsUri = "http://api.steampowered.com/ISteamUserStats/GetUserStatsForGame/v0002/?appid={0}&key={2}&steamid={1}";

        [Localizable(false), CanBeNull]
        public static string[] TryToGetAchievments(string appId, string steamId) {
            var requestUri = string.Format(RequestStatsUri, appId, steamId, InternalUtils.GetSteamApiCode());

            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";

                using (var response = (HttpWebResponse)httpRequest.GetResponse()) {
                    if (response.StatusCode != HttpStatusCode.OK) return null;

                    var result = response.GetResponseStream()?.ReadAsStringAndDispose();
                    if (result == null) return null;

                    var parsed = JObject.Parse(result);
                    return ((JArray)((JObject)parsed["playerstats"])["achievements"]).Select(x => (JObject)x)
                                                                                     .Where(x => (int)x["achieved"] > 0)
                                                                                     .Select(x => (string)x["name"])
                                                                                     .ToArray();
                }
            } catch (WebException e) {
                Logging.Warning($"TryToGetAchievments(): {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"TryToGetAchievments(): {requestUri}\n{e}");
                return null;
            }
        }

        private const string RequestPlayerSummariesUri = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={1}&steamids={0}";

        [Localizable(false), CanBeNull]
        public static string TryToGetUserName(string steamId) {
            var requestUri = string.Format(RequestPlayerSummariesUri, steamId, InternalUtils.GetSteamApiCode());

            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";

                using (var response = (HttpWebResponse)httpRequest.GetResponse()) {
                    if (response.StatusCode != HttpStatusCode.OK) return null;

                    var result = response.GetResponseStream()?.ReadAsStringAndDispose();
                    if (result == null) return null;

                    var parsed = JObject.Parse(result);
                    return (string)(((JArray)((JObject)parsed["response"])["players"]).FirstOrDefault() as JObject)?["personaname"];
                }
            } catch (WebException e) {
                Logging.Warning($"TryToGetUserName(): {requestUri}, {e.Message}");
                return null;
            } catch (Exception e) {
                Logging.Warning($"TryToGetUserName(): {requestUri}\n{e}");
                return null;
            }
        }
    }
}