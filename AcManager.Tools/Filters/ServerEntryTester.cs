using System.Linq;
using AcManager.Tools.Managers.Online;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class ServerEntryTester : IParentTester<ServerEntry> {
        public static ServerEntryTester Instance = new ServerEntryTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "country":
                    return nameof(ServerEntry.Country);

                case "d":
                case "drivers":
                case "players":
                case "full":
                case "left":
                    return nameof(ServerEntry.CurrentDriversCount);

                case "track":
                    return nameof(ServerEntry.Track);

                case "a":
                case "available":
                case "car":
                    return nameof(ServerEntry.Cars);

                case "driver":
                    return nameof(ServerEntry.CurrentDrivers);

                case "password":
                    return nameof(ServerEntry.PasswordRequired);
            }

            return AcObjectTester.InnerParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(ServerEntry obj, string key, ITestEntry value) {
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
                    return obj.CurrentDrivers.Any(x => value.Test(x.Name));

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

                case "left":
                    return value.Test(obj.Capacity - obj.CurrentDriversCount);

                case "missing":
                    return value.Test(obj.Track == null || obj.CarsOrTheirIds.Any(x => !x.CarExists));

                case "p":
                case "ping":
                    return obj.Ping.HasValue && value.Test((double)obj.Ping);

                case "password":
                    return value.Test(obj.PasswordRequired);
            }

            return AcObjectTester.Instance.Test(obj, key, value);
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
