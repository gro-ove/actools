using System.Collections.Generic;
using AcManager.Tools.Profile;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class LapTimeTester : IParentTester<LapTimeWrapped>, ITesterDescription {
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

                case "c":
                case "car":
                    return value.Test(obj.Car?.DisplayName) || value.Test(obj.Car?.Id);

                case "t":
                case "track":
                    return value.Test(obj.Track?.Name) || value.Test(obj.Track?.IdWithLayout);

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
                case "c":
                case "car":
                    return obj.Car != null && filter.Test(CarObjectTester.Instance, obj.Car);

                case "t":
                case "track":
                    return obj.Track != null && filter.Test(TrackObjectBaseTester.Instance, obj.Track);
            }

            return false;
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("time", "Lap time", KeywordType.TimeSpan, KeywordPriority.Important),
                new KeywordDescription("date", "Date lap time was set", KeywordType.DateTime, KeywordPriority.Normal),
                new KeywordDescription("car", "Car", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "c"),
                new KeywordDescription("track", "Track", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "t"),
                new KeywordDescription("source", "Origin of lap time entry", KeywordType.String, KeywordPriority.Obscured, "type"),
            };
        }
    }
}