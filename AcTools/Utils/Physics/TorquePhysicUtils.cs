using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcTools.Utils.Physics {
    public class TurboDescription {
        public double MaxBoost, Wastegate, ReferenceRpm, Gamma;

        public double CalculateMultipler(double rpm) {
            return Math.Min(Wastegate, MaxBoost * Math.Min(1, Math.Pow(rpm / ReferenceRpm, Gamma)));
        }

        public static TurboDescription FromIniSection(IniFileSection turboSection) {
            return new TurboDescription {
                MaxBoost = turboSection.GetDouble("MAX_BOOST", 0d),
                Wastegate = turboSection.GetDouble("WASTEGATE", 0d),
                ReferenceRpm = turboSection.GetDouble("REFERENCE_RPM", 1000d),
                Gamma = turboSection.GetDouble("GAMMA", 1d)
            };
        }
    }

    public static class TorquePhysicUtils {
        public static Dictionary<double, double> CombineWithCalculatedTurbo(Dictionary<double, double> torqueValues, params TurboDescription[] turbo) {
            return torqueValues.ToDictionary(x => x.Key, x => x.Value * (1.0 + turbo.Select(y => y.CalculateMultipler(x.Key)).Sum()));
        }

        private const double TorqueRpmToBhpMultipler = 1.0/(9.5488*745.7);

        public static double TorqueToPower(double torque, double rpm) {
            return rpm*torque*TorqueRpmToBhpMultipler;
        }

        public static Dictionary<double, double> LoadCarTorque(string carDir) {
            var powerLut = new LutDataFile(carDir, "power.lut");
            if (!powerLut.Exists() || powerLut.IsEmptyOrDamaged()) throw new FileNotFoundException("Cannot load power.lut", "data/power.lut");

            var engineIni = new IniFile(carDir, "engine.ini");
            if (!engineIni.Exists() || engineIni.IsEmptyOrDamaged()) throw new FileNotFoundException("Cannot load engine.ini", "data/engine.ini");

            var maxRpm = engineIni["ENGINE_DATA"].GetDouble("LIMITER", powerLut.Values.Keys.Max());
            var torqueData = powerLut.Values.Where(x => x.Key <= maxRpm * 1.2 && x.Value > 0).ToDictionary(x => x.Key, x => x.Value);

            string key;
            var turbos = new List<TurboDescription>();
            for (var i = 0; engineIni.ContainsKey(key = "TURBO_" + i); i++) {
                turbos.Add(TurboDescription.FromIniSection(engineIni[key]));
            }

            if (turbos.Count > 0) {
                torqueData = CombineWithCalculatedTurbo(torqueData, turbos.ToArray());
            }

            return torqueData;
        } 
    }
}
