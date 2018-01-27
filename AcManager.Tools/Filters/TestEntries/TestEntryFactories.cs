using FirstFloor.ModernUI.Helpers;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Filters.TestEntries {
    public static class TestEntryFactories {
        public static ITestEntryFactory UnitsOne = new TestEntryFactory(ToUnitsOnePostfix);
        public static ITestEntryFactory DistanceMeters = new TestEntryFactory(ToMetersPostfix);
        public static ITestEntryFactory DistanceKilometers = new TestEntryFactory(ToKilometersPostfix);
        public static ITestEntryFactory SpeedKph = new TestEntryFactory(ToSpeedKphPostfix);
        public static ITestEntryFactory PowerBhp = new TestEntryFactory(ToPowerBhpPostfix);
        public static ITestEntryFactory TorqueNm = new TestEntryFactory(ToTorqueNmPostfix);
        public static ITestEntryFactory WeightKg = new TestEntryFactory(ToWeightKgPostfix);
        public static ITestEntryFactory FileSizeMegabytes = new TestEntryFactory(ConvertFileSizeMegabytes);
        public static ITestEntryFactory TimeSeconds = new TimeSpanTestEntry(@"second");
        public static ITestEntryFactory TimeMinutes = new TimeSpanTestEntry(@"minute");
        public static ITestEntryFactory TimeDays = new TimeSpanTestEntry(@"day");

        private static bool ConvertFileSizeMegabytes(string value, out double parsed) {
            if (!LocalizationHelper.TryParseReadableSize(value, @"mb", out var bytes)) {
                parsed = 0d;
                return false;
            }

            parsed = bytes;
            return true;
        }

        private static double ToUnitsOnePostfix(string postfix) {
            switch (postfix) {
                case "kkk":
                    return 1e9;
                case "kk":
                    return 1e6;
                case "k":
                case "thou":
                    return 1e3;
                case "hund":
                    return 1e2;
                default:
                    return 1d;
            }
        }

        private static double ToMetersPostfix(string postfix) {
            switch (postfix) {
                case "mi":
                case "mile":
                    return 1609.34;
                case "k":
                case "km":
                case "kilo":
                    return 1e3;
                case "yd":
                case "yard":
                    return 0.9144;
                case "ft":
                case "foot":
                case "feet":
                    return 0.3048;
                case "in":
                case "inch":
                    return 0.0254;
                case "cm":
                case "cent":
                    return 0.01;
                case "mm":
                case "mill":
                    return 0.001;
                default:
                    return 1d;
            }
        }

        private static double ToKilometersPostfix(string postfix) {
            switch (postfix) {
                case "mi":
                case "mile":
                    return 1.60934;
                case "km":
                case "kilo":
                    return 1;
                case "m":
                case "metr":
                case "mete":
                    return 0.001;
                case "yd":
                case "yard":
                    return 0.0009144;
                case "ft":
                case "foot":
                case "feet":
                    return 0.0003048;
                case "in":
                case "inch":
                    return 2.54E-05;
                case "cm":
                case "cent":
                    return 1E-05;
                case "mm":
                case "mill":
                    return 1E-06;
                default:
                    return 1d;
            }
        }

        private static double ToSpeedKphPostfix(string postfix) {
            switch (postfix) {
                case "ms":
                case "mps":
                    return 3.6;
                case "knot":
                    return 1.852;
                case "mph":
                case "mh":
                    return 1.60934;
                case "fps":
                case "fs":
                    return 1.09728;
                default:
                    return 1d;
            }
        }

        private static double ToPowerBhpPostfix(string postfix) {
            switch (postfix) {
                case "gw":
                    return 1341020;
                case "mw":
                    return 1341.02;
                case "kw":
                    return 1.34102;
                case "w":
                    return 0.00134102;
                default:
                    return 1d;
            }
        }

        private static double ToTorqueNmPostfix(string postfix) {
            switch (postfix) {
                case "ft":
                case "fl":
                case "ftlb":
                    return 1.35581795;
                default:
                    return 1d;
            }
        }

        private static double ToWeightKgPostfix(string postfix) {
            switch (postfix) {
                case "t":
                case "tonn":
                    return 1000;
                case "st":
                case "ston":
                    return 6.35029;
                case "lb":
                case "lbs":
                    return 0.453592;
                case "oz":
                case "ounc":
                    return 0.0283495;
                case "g":
                case "gram":
                    return 0.001;
                default:
                    return 1d;
            }
        }

    }
}