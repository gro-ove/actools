using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Utils;
using StringBasedFilter;

namespace PreviewUpdater {
    internal class CarTester : IParentTester<string> {
        private readonly string _acRoot;

        public CarTester(string acRoot) {
            _acRoot = acRoot;
        }

        public string ParameterFromKey(string key) {
            return null;
        }

        private string[] _kunosCarsIds;

        private bool TestIfKunosUsingGuids(string id) {
            if (_kunosCarsIds == null) {
                try {
                    _kunosCarsIds = File.ReadAllLines(AcPaths.GetSfxGuidsFilename(_acRoot))
                                        .Select(x => x.Split('/'))
                                        .Where(x => x.Length > 3 && x[1] == "cars" && x[0].EndsWith("event:"))
                                        .Select(x => x[2].ToLowerInvariant())
                                        .Distinct()
                                        .ToArray();
                } catch (Exception) {
                    _kunosCarsIds = new string[] { };
                }
            }

            return _kunosCarsIds.Contains(id);
        }

        public bool Test(string carId, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(carId);

                case "l":
                case "len":
                case "length":
                    return value.Test(carId.Length);

                case "k":
                case "kunos": {
                    return value.Test(TestIfKunosUsingGuids(carId));
                }

                case "a":
                case "age": {
                    var directory = AcPaths.GetCarDirectory(_acRoot, carId);
                    var age = File.GetCreationTime(directory);
                    return value.Test(DateTime.Now - age);
                }

                case "n":
                case "new": {
                    var directory = AcPaths.GetCarDirectory(_acRoot, carId);
                    var age = File.GetCreationTime(directory);
                    return value.Test((DateTime.Now - age).TotalDays < 7d);
                }

                default:
                    return false;
            }
        }

        private readonly Dictionary<string, string[]> _skinsPerCar = new Dictionary<string, string[]>();

        public bool TestChild(string carId, string key, IFilter filter) {
            if (key != "s" && key != "skin") return false;

            string[] skins;
            if (!_skinsPerCar.TryGetValue(carId, out skins)) {
                skins = Directory.GetDirectories(AcPaths.GetCarSkinsDirectory(_acRoot, carId)).Select(Path.GetFileName).ToArray();
                _skinsPerCar[carId] = skins;
            }

            return skins.Any(x => filter.Test(SkinTester.Instance, x));
        }
    }
}