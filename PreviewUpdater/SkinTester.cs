using StringBasedFilter;

namespace PreviewUpdater {
    internal class SkinTester : ITester<string> {
        public string ParameterFromKey(string key) {
            return null;
        }

        public bool Test(string carId, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(carId);

                case "l":
                case "len":
                case "length":
                    return value.Test(carId.Length);
            }

            return false;
        }

        public static SkinTester Instance { get; } = new SkinTester();
    }
}