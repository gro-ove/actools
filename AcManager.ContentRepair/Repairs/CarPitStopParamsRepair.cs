using System;
using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair.Repairs {
    public class CarPitStopParamsRepair : CarSimpleRepairBase {
        /// <summary>
        /// Sets pit stop parameters.
        /// </summary>
        /// <param name="data">Data to update.</param>
        /// <param name="tyreChange">Time spent to change each tyre.</param>
        /// <param name="fuelLiter">Time spent to put 1 lt of fuel inside the car.</param>
        /// <param name="bodyRepair">Time spent to repair 10% of body damage.</param>
        /// <param name="engineRepair">Time spent to repair 10% of engine damage.</param>
        /// <param name="suspRepair">Time spent to repair 10% of suspension damage.</param>
        public static void SetPitParams(DataWrapper data, TimeSpan tyreChange, TimeSpan fuelLiter, TimeSpan bodyRepair, TimeSpan engineRepair,
                TimeSpan suspRepair) {
            var carIni = data.GetIniFile("car.ini");
            var section = carIni["PIT_STOP"];
            section.Set("TYRE_CHANGE_TIME_SEC", tyreChange.TotalSeconds);
            section.Set("FUEL_LITER_TIME_SEC", fuelLiter.TotalSeconds);
            section.Set("BODY_REPAIR_TIME_SEC", bodyRepair.TotalSeconds);
            section.Set("ENGINE_REPAIR_TIME_SEC", engineRepair.TotalSeconds);
            section.Set("SUSP_REPAIR_TIME_SEC", suspRepair.TotalSeconds);
            carIni.Save();
        }

        /// <summary>
        /// Sets pit stop parameters to common values.
        /// </summary>
        /// <param name="data">Data to update.</param>
        public static void SetPitParams(DataWrapper data) {
            SetPitParams(data, TimeSpan.FromSeconds(10d), TimeSpan.FromSeconds(0.6), TimeSpan.FromSeconds(20d), TimeSpan.FromSeconds(2d),
                    TimeSpan.FromSeconds(30d));
        }

        protected override void Fix(CarObject car, DataWrapper data) {
            SetPitParams(data);
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            if (data.GetIniFile(@"car.ini").ContainsKey(@"PIT_STOP")) return null;
            return new ContentObsoleteSuggestion("Pit stop parameters missing", "You might want to add them.",
                    (p, c) => FixAsync(car, p, c)) {
                AffectsData = true
            };
        }
    }
}