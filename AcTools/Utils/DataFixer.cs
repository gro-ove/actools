using AcTools.AcdFile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using AcTools.DataFile;

namespace AcTools.Utils {
    public static class DataFixer {
        public static async void TestData(string carDir, double weight, Action<string> error, Action callback) {
            var list = await TestData(carDir, weight);
            foreach (var e in list) error(e);
            callback();
        }

        public static Task<List<string>> TestData(string carDir, double weight) {
            return Task.Run(() => {
                var errors = new List<string>();

                try {
                    var acdFile = Path.Combine(carDir, "data.acd");
                    var acd = File.Exists(acdFile) ? Acd.FromFile(acdFile) : null;
                    
                    var aeroIni = new IniFile(carDir, "aero.ini", acd);
                    if (aeroIni.ContainsKey("DATA")) {
                        errors.Add("acd-obsolete-aero-data");
                    }

                    if (weight > 0) {
                        var carIni = new IniFile(carDir, "car.ini", acd);
                        if (Math.Abs(weight + 75.0 - carIni["BASIC"].GetDouble("TOTALMASS", 0d)) > 90.0) {
                            errors.Add("acd-invalid-weight");
                        }
                    }
                } catch (Exception) {
                    errors.Add("acd-test-error");
                }

                GC.Collect();
                return errors;
            });
        }

        public static void RemoveAeroDataSection(string carDir) {
            var aeroIni = new IniFile(carDir, "aero.ini");
            aeroIni.Remove("DATA");
            aeroIni.Save(true);
        }

        public static double GetWeight(string carDir) {
            var carIni = new IniFile(carDir, "car.ini");
            return carIni["BASIC"].GetDouble("TOTALMASS", 0d) - 75.0;
        }

        public static void SetWeight(string carDir, double newValue) {
            var carIni = new IniFile(carDir, "car.ini");
            carIni["BASIC"]["TOTALMASS"] = (newValue + 75.0).ToString(CultureInfo.InvariantCulture);
            carIni.Save(true);
        }
    }
}
