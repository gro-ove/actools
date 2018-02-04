using AcManager.Tools.Filters.Testers;
using StringBasedFilter;

namespace AcManager.Tools.Tyres {
    public class TyresEntryTester : IParentTester<TyresEntry> {
        public static readonly TyresEntryTester Instance = new TyresEntryTester();

        public string ParameterFromKey(string key) {
            return null;
        }

        public bool Test(TyresEntry obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(obj.DisplayName);
                case "n":
                case "name":
                    return value.Test(obj.Name);
                case "p":
                case "params":
                    return value.Test(obj.DisplayParams);
                case "profile":
                    return value.Test(obj.DisplayProfile);
                case "radius":
                case "rimradius":
                    return value.Test(obj.DisplayRimRadius);
                case "width":
                    return value.Test(obj.DisplayWidth);
                case "a":
                case "g":
                case "grade":
                    return value.Test(obj.AppropriateLevelFront.ToString()) || value.Test(obj.AppropriateLevelRear.ToString());
                case "fa":
                case "fg":
                case "fgrade":
                    return value.Test(obj.AppropriateLevelFront.ToString());
                case "ra":
                case "rg":
                case "rgrade":
                    return value.Test(obj.AppropriateLevelRear.ToString());
                case "car":
                    return value.Test(obj.SourceCarId) || value.Test(obj.Source?.DisplayName);
                case "v":
                case "ver":
                case "version":
                    return value.Test(obj.Version);
            }

            return false;
        }

        public bool TestChild(TyresEntry obj, string key, IFilter filter) {
            switch (key) {
                case "car":
                    return obj.Source != null && filter.Test(CarObjectTester.Instance, obj.Source);
            }
            return false;
        }
    }
}