using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class ServerPresetObjectTester : ITester<ServerPresetObject> {
        public static ServerPresetObjectTester Instance = new ServerPresetObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcCommonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(ServerPresetObject obj, string key, ITestEntry value) {
            switch (key) {
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }
    }
}