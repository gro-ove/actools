using AcManager.Tools.Profile;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class LapTimeTester : IParentTester<LapTimeWrapped> {
        public static readonly LapTimeTester Instance = new LapTimeTester();
        
        public string ParameterFromKey(string key) {
            switch (key) {
                case "c":
                case "car":
                    return nameof(LapTimeWrapped.Car);

                case "t":
                case "track":
                    return nameof(LapTimeWrapped.Track);
            }

            return null;
        }

        public bool Test(LapTimeWrapped obj, string key, ITestEntry value) {
            if (key == null) {
                return value.Test(obj.Entry.CarId) || obj.Car != null && value.Test(obj.Car.DisplayName) ||
                        value.Test(obj.Entry.TrackId) || obj.Track != null && value.Test(obj.Track.Name);
            }

            switch (key) {
                case "time":
                    return value.Test(obj.Entry.LapTime);
                    
                case "date":
                    return value.Test(obj.Entry.EntryDate);

                case "source":
                case "type":
                    return value.Test(obj.Entry.Source);
            }

            return false;
        }

        public bool TestChild(LapTimeWrapped obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "car":
                    return obj.Car != null && filter.Test(CarObjectTester.Instance, obj.Car);

                case "track":
                    return obj.Track != null && filter.Test(TrackObjectBaseTester.Instance, obj.Track);
            }

            return false;
        }
    }
}