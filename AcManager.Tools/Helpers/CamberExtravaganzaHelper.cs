using System;
using System.IO;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers {
    public static class CamberExtravaganzaHelper {
        public static readonly string AppId = @"camber-extravaganza";

        public static void UpdateDatabase([NotNull] CarObject car, bool? separateFiles = null) {
            try {
                var directory = Path.Combine(AcPaths.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), AppId);
                if (!Directory.Exists(directory)) return;

                if (!AcSettingsHolder.Python.IsActivated(AppId)) {
                    Logging.Write("App is not active");
                    return;
                }

                if (car.AcdData?.IsEmpty != false) {
                    Logging.Write("Data is damaged");
                    return;
                }

                var kunosCar = car.Author == AcCommonObject.AuthorKunos;
                if (kunosCar) return;

                var filename = Path.Combine(directory, @"tyres_data", @"added-by-cm.json");
                var front = new JObject();
                var rear = new JObject();
                var tyres = car.AcdData.GetIniFile("tyres.ini");
                foreach (var name in tyres.GetExistingSectionNames(@"FRONT", -1)) {
                    Wrap(front, tyres[name]);
                    Wrap(rear, tyres[name.Replace(@"FRONT", @"REAR")]);
                    void Wrap(JObject target, IniFileSection section) {
                        target[section.GetNonEmpty("SHORT_NAME") ?? @"_"] = new JObject {
                            [@"DCAMBER_0"] = section.GetDouble("DCAMBER_0", 1d),
                            [@"DCAMBER_1"] = section.GetDouble("DCAMBER_1", 1d),
                            [@"LS_EXPY"] = section.GetDouble("LS_EXPY", 1d),
                        };
                    }
                }

                File.WriteAllText(filename, new JObject {
                    [car.Id] = new JObject { [@"FRONT"] = front, [@"REAR"] = rear }
                }.ToString(Formatting.Indented));
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public static void UpdateDatabase([NotNull] string carId) {
            var car = CarsManager.Instance.GetById(carId);
            if (car == null) {
                Logging.Write($"Car “{carId}” not found");
                return;
            }

            UpdateDatabase(car);
        }
    }
}