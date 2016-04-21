using System;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcTools.Processes;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class ServerEntryTester : IParentTester<ServerEntry> {
        public static ServerEntryTester Instance = new ServerEntryTester();

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

                case "track":
                    return nameof(ServerEntry.Track);

                case "a":
                case "available":
                case "car":
                    return nameof(ServerEntry.Cars);

                case "p":
                case "password":
                    return nameof(ServerEntry.PasswordRequired);

                case "errors":
                case "haserrors":
                    return nameof(ServerEntry.HasErrors);

                case "active":
                case "booking":
                case "practice":
                case "qualification":
                case "race":
                case "drift":
                case "hotlap":
                case "timeattack":
                    return nameof(ServerEntry.Sessions);

                case "left":
                case "ended":
                    return nameof(ServerEntry.SessionEnd);
            }

            return AcObjectTester.InnerParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(ServerEntry obj, string key, ITestEntry value) {
            if (key == null) {
                return value.Test(obj.Id) || value.Test(obj.DisplayName);
            }

            switch (key) {
                case "country":
                    return value.Test(obj.Country);

                case "ip":
                    return value.Test(obj.Ip);

                case "car":
                    return obj.Cars?.Any(x => value.Test(x.CarObject.Id) || value.Test(x.CarObject.Name)) == true;

                case "a":
                case "available":
                    return obj.Cars?.Where(x => x.Available > 0).Any(x => value.Test(x.CarObject.Id) || value.Test(x.CarObject.Name)) == true;

                case "driver":
                case "player":
                    return obj.CurrentDrivers?.Any(x => value.Test(x.Name)) == true;

                case "driverteam":
                case "playerteam":
                    return obj.CurrentDrivers?.Any(x => value.Test(x.Team)) == true;

                case "track":
                    return value.Test(obj.Track?.Id) || value.Test(obj.Track?.Name);

                case "c":
                case "cap":
                case "capacity":
                    return value.Test(obj.Capacity);

                case "d":
                case "drivers":
                case "players":
                    return value.Test(obj.CurrentDriversCount);

                case "full":
                    return value.Test(obj.CurrentDriversCount == obj.Capacity);

                case "free":
                    return value.Test(obj.Capacity - obj.CurrentDriversCount);

                case "missing":
                    return value.Test(obj.Track == null || obj.CarsOrTheirIds.Any(x => !x.CarExists));

                case "ping":
                    return obj.Ping.HasValue && value.Test((double)obj.Ping);

                case "p":
                case "password":
                    return value.Test(obj.PasswordRequired);

                case "errors":
                case "haserrors":
                    return value.Test(obj.HasErrors);

                case "active":
                    var activeSession = obj.Sessions.FirstOrDefault(x => x.IsActive);
                    return activeSession != null && value.Test(activeSession.Type.ToString());

                case "left":
                    var now = DateTime.Now;
                    return obj.SessionEnd > now && value.Test((obj.SessionEnd - now).TotalMinutes);

                case "ended":
                    return value.Test(obj.SessionEnd <= DateTime.Now);
            }

            Game.SessionType sessionType;
            switch (key) {
                case "booking":
                    sessionType = Game.SessionType.Booking;
                    break;

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

                default:
                    return AcObjectTester.Instance.Test(obj, key, value);
            }

            var session = obj.Sessions.FirstOrDefault(x => x.Type == sessionType);
            return session != null && value.Test(session.Duration / 60d);
        }

        public bool TestChild(ServerEntry obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "car":
                    return obj.Cars?.Any(x => filter.Test(CarObjectTester.Instance, x.CarObject)) == true;

                case "track":
                    return obj.Track != null && filter.Test(TrackBaseObjectTester.Instance, obj.Track);

                case "a":
                case "available":
                    return obj.Cars?.Where(x => x.Available > 0).Any(x => filter.Test(CarObjectTester.Instance, x.CarObject)) == true;
            }

            return false;
        }
    }
}
