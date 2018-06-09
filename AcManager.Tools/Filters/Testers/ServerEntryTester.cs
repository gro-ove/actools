using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Filters.TestEntries;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcTools.Processes;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class ServerEntryTester : IParentTester<ServerEntry>, ITesterDescription {
        public static readonly ServerEntryTester Instance = new ServerEntryTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "country":
                    return nameof(ServerEntry.Country);

                case "ip":
                    return nameof(ServerEntry.Ip);

                case "d":
                case "drivers":
                case "players":
                case "full":
                case "free":
                    return nameof(ServerEntry.CurrentDriversCount);

                case "driver":
                case "player":
                case "driverteam":
                case "playerteam":
                    return nameof(ServerEntry.CurrentDrivers);

                case "ext":
                case "extended":
                    return nameof(ServerEntry.ExtendedMode);

                case "track":
                case "trackid":
                    return nameof(ServerEntry.Track);

                case "weather":
                case "weatherid":
                    return nameof(ServerEntry.WeatherObject);

                case "a":
                case "available":
                case "c":
                case "car":
                case "carid":
                    return nameof(ServerEntry.Cars);

                case "p":
                case "pass":
                case "password":
                    return nameof(ServerEntry.PasswordRequired);

                case "errors":
                case "haserrors":
                    return nameof(ServerEntry.HasErrors);

                case "active":
                    return nameof(ServerEntry.CurrentSessionType);

                case "booking":
                    return nameof(ServerEntry.BookingMode);

                case "practice":
                case "qualification":
                case "race":
                case "drift":
                case "hotlap":
                case "timeattack":
                case "l":
                case "laps":
                    return nameof(ServerEntry.Sessions);

                case "left":
                case "ended":
                    return nameof(ServerEntry.SessionEnd);

                case "name":
                case null:
                    return nameof(ServerEntry.DisplayName);

                case "friend":
                case "friends":
                    return nameof(ServerEntry.HasFriends);

                case "connected":
                case "lastconnected":
                    return nameof(ServerEntry.LastConnected);

                case "sessionscount":
                case "connectedtimes":
                    return nameof(ServerEntry.SessionsCount);

                case "tag":
                case "drivertag":
                    return nameof(ServerEntry.DriversTagsString);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(ServerEntry obj, string key, ITestEntry value) {
            if (key == null) {
                return value.Test(obj.TrackId) || value.Test(obj.Id) || value.Test(obj.DisplayName)
                        || SettingsHolder.Online.FixNamesMode.IntValue != 0 && value.Test(obj.ActualName);
            }

            switch (key) {
                case "country":
                    return value.Test(obj.Country);

                case "ip":
                    return value.Test(obj.Ip);

                case "id":
                    return value.Test(obj.Id);

                case "port":
                    return value.Test(obj.PortHttp);

                case "carid":
                    return obj.Cars?.Any(x => value.Test(x.Id)) == true;

                case "c":
                case "car":
                    return obj.Cars?.Any(x => value.Test(x.Id) || value.Test(x.CarObject?.Name)) == true;

                case "trackid":
                    return value.Test(obj.Track?.Id);

                case "t":
                case "track":
                    return value.Test(obj.Track?.Id) || value.Test(obj.Track?.Name);

                case "w":
                case "weather":
                    return value.Test(obj.WeatherId) || value.Test(obj.WeatherObject?.Name);

                case "a":
                case "available":
                    return obj.Cars?.Where(x => x.Available > 0).Any(x => value.Test(x.Id) || value.Test(x.CarObject?.Name)) == true;

                case "driver":
                case "player":
                    return obj.CurrentDrivers?.Any(x => value.Test(x.Name)) == true;

                case "driverteam":
                case "playerteam":
                    return obj.CurrentDrivers?.Any(x => value.Test(x.Team)) == true;

                case "ext":
                case "extended":
                    return value.Test(obj.ExtendedMode);

                case "cap":
                case "capacity":
                    return value.Test(obj.Capacity);

                case "l":
                case "laps":
                    return value.Test(obj.RaceMode == RaceMode.Laps
                            ? (int)(obj.Sessions?.FirstOrDefault(x => x.Type == Game.SessionType.Race)?.Duration ?? 0) : 0);

                case "timed":
                    return value.Test(obj.RaceMode != RaceMode.Laps);

                case "racetime":
                    value.Set(TestEntryFactories.TimeMinutes);
                    return value.Test(obj.RaceMode != RaceMode.Laps
                            ? TimeSpan.FromSeconds(obj.Sessions?.FirstOrDefault(x => x.Type == Game.SessionType.Race)?.Duration ?? 0) : TimeSpan.Zero);

                case "extra":
                    return value.Test(obj.RaceMode == RaceMode.TimedExtra);

                case "d":
                case "drivers":
                case "players":
                    return value.Test(obj.CurrentDriversCount);

                case "full":
                    return value.Test(obj.CurrentDriversCount == obj.Capacity);

                case "free":
                    return value.Test(obj.Capacity - obj.CurrentDriversCount);

                case "missing":
                    return value.Test(obj.TrackId != null && obj.Track == null || obj.Cars?.Any(x => !x.CarExists) == true);

                case "ping":
                    return obj.Ping.HasValue && value.Test((double)obj.Ping);

                case "p":
                case "pass":
                case "password":
                    return value.Test(obj.PasswordRequired);

                case "errors":
                case "haserrors":
                    return value.Test(obj.HasErrors);

                case "booking":
                    return value.Test(obj.BookingMode);

                case "active":
                    return value.Test(obj.CurrentSessionType?.ToString());

                case "left":
                    value.Set(TestEntryFactories.TimeMinutes);
                    var now = DateTime.Now;
                    return obj.SessionEnd > now && value.Test(obj.SessionEnd - now);

                case "ended":
                    return value.Test(obj.SessionEnd <= DateTime.Now);

                case "friend":
                case "friends":
                    return value.Test(obj.HasFriends);

                case "connected":
                case "lastconnected":
                    return obj.LastConnected.HasValue && value.Test(obj.LastConnected.Value);

                case "sessionscount":
                case "connectedtimes":
                    return value.Test(obj.SessionsCount);

                case "tag":
                case "drivertag":
                    return obj.CurrentDrivers?.Any(x => x.Tags.Any(y => value.Test(y.DisplayName))) == true;
            }

            Game.SessionType sessionType;
            switch (key) {
                case "practice":
                    sessionType = Game.SessionType.Practice;
                    break;

                case "qualification":
                    sessionType = Game.SessionType.Qualification;
                    break;

                case "race":
                    sessionType = Game.SessionType.Race;
                    break;

                case "drift":
                    sessionType = Game.SessionType.Drift;
                    break;

                case "hotlap":
                    sessionType = Game.SessionType.Hotlap;
                    break;

                case "timeattack":
                    sessionType = Game.SessionType.TimeAttack;
                    break;

                case "name":
                    return value.Test(obj.DisplayName)
                            || SettingsHolder.Online.FixNamesMode.IntValue != 0 && value.Test(obj.ActualName);

                default:
                    return false;
            }

            var session = obj.Sessions?.FirstOrDefault(x => x.Type == sessionType);
            return value.Test(TimeSpan.FromSeconds(session?.Duration ?? 0d));
        }

        public bool TestChild(ServerEntry obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "car":
                    return obj.Cars?.Any(x => x.CarObject != null && filter.Test(CarObjectTester.Instance, x.CarObject)) == true;

                case "track":
                    return obj.Track != null && filter.Test(TrackObjectBaseTester.Instance, obj.Track);

                case "weather":
                    return obj.WeatherObject != null && filter.Test(WeatherObjectTester.Instance, obj.WeatherObject);

                case "a":
                case "available":
                    return obj.Cars?.Where(x => x.Available > 0).Any(x => x.CarObject != null && filter.Test(CarObjectTester.Instance, x.CarObject)) == true;
            }

            return false;
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("country", "Country", KeywordType.String, KeywordPriority.Normal),
                new KeywordDescription("ip", "IP", KeywordType.Number, KeywordPriority.Obscured),
                new KeywordDescription("id", "ID", KeywordType.String, KeywordPriority.Obscured),
                new KeywordDescription("port", "Port", KeywordType.Number, KeywordPriority.Obscured),
                new KeywordDescription("car", "Car", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "c"),
                new KeywordDescription("track", "Track", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "t"),
                new KeywordDescription("available", "Available car (in pickup mode)", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "a"),
                new KeywordDescription("driver", "Current driver’s name", KeywordType.String, KeywordPriority.Normal, "player"),
                new KeywordDescription("driverteam", "Current driver’s team", KeywordType.String, KeywordPriority.Normal, "playerteam"),
                // new KeywordDescription("ext", "Extended mode (with AC Server Wrapper)", KeywordType.Flag, KeywordPriority.Normal, "extended"),
                new KeywordDescription("cap", "Capacity", KeywordType.Number, KeywordPriority.Normal, "capacity"),
                new KeywordDescription("laps", "Number of laps", KeywordType.Number, KeywordPriority.Normal, "l"),
                new KeywordDescription("timed", "With race limited by time rather than amount of laps", KeywordType.Flag, KeywordPriority.Obscured),
                new KeywordDescription("racetime", "Time of a race session limited by it", "minutes", KeywordType.TimeSpan, KeywordPriority.Obscured),
                new KeywordDescription("extra", "With race limited by time rather than amount of laps, plus one extra lap", KeywordType.Flag, KeywordPriority.Obscured),
                new KeywordDescription("drivers", "Number of drivers", KeywordType.Number, KeywordPriority.Important, "d", "players"),
                new KeywordDescription("full", "Full (no more empty slots)", KeywordType.Flag, KeywordPriority.Important),
                new KeywordDescription("free", "Number of free slots", KeywordType.Number, KeywordPriority.Important),
                new KeywordDescription("missing", "With missing content", KeywordType.Flag, KeywordPriority.Normal),
                new KeywordDescription("ping", "Ping", KeywordType.Number, KeywordPriority.Normal),
                new KeywordDescription("pass", "With password", KeywordType.Flag, KeywordPriority.Important, "p", "password"),
                new KeywordDescription("errors", "With errors", KeywordType.Flag, KeywordPriority.Obscured, "haserrors"),
                new KeywordDescription("booking", "In booking mode", KeywordType.Flag, KeywordPriority.Important),
                new KeywordDescription("active", "Active session", KeywordType.String, KeywordPriority.Important),
                new KeywordDescription("left", "Time left to the end of current session", "minutes", KeywordType.TimeSpan, KeywordPriority.Normal),
                new KeywordDescription("ended", "With current session ended", KeywordType.Flag, KeywordPriority.Normal),
                new KeywordDescription("friend", "With drivers tagged as friends", KeywordType.Flag, KeywordPriority.Normal, "friends"),
                new KeywordDescription("connected", "Date of previous connection", KeywordType.DateTime, KeywordPriority.Normal, "lastconnected"),
                new KeywordDescription("connectedtimes", "Amount of times server was connected to before", KeywordType.Number, KeywordPriority.Normal, "sessionscount"),
                new KeywordDescription("tag", "Drivers’ tag", KeywordType.Number, KeywordPriority.Normal, "drivertag"),

                new KeywordDescription("practice", "Practice session", KeywordType.TimeSpan | KeywordType.Flag, KeywordPriority.Extra),
                new KeywordDescription("qualification", "Qualify session", KeywordType.TimeSpan | KeywordType.Flag, KeywordPriority.Extra),
                new KeywordDescription("race", "Race session", KeywordType.TimeSpan | KeywordType.Flag, KeywordPriority.Extra),
                // new KeywordDescription("drift", "Drift session", KeywordType.TimeSpan, KeywordPriority.Extra),
                // new KeywordDescription("hotlap", "Hotlap session", KeywordType.TimeSpan, KeywordPriority.Extra),
                // new KeywordDescription("timeattack", "Time attack session", KeywordType.TimeSpan, KeywordPriority.Extra),

                new KeywordDescription("name", "Name", KeywordType.Number, KeywordPriority.Obscured),

            }.Concat(AcCommonObjectTester.Instance.GetDescriptions());
        }
    }
}
