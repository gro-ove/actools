using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcTools.Utils.Physics {
    public class TurboDescription {
        public double MaxBoost, Wastegate, ReferenceRpm, Gamma;

        [CanBeNull]
        public IReadOnlyList<TurboControllerDescription> Controllers;

        private double GetBoost(double rpm) {
            if (Controllers == null) return MaxBoost;

            var boost = 0d;
            for (var i = Controllers.Count - 1; i >= 0; i--) {
                var controller = Controllers[i];
                boost = controller.Process(rpm, boost);
            }
            return boost;
        }

        [Pure]
        public double CalculateMultipler(double rpm) {
            var baseLevel = Math.Min(1, Math.Pow(rpm / ReferenceRpm, Gamma));
            var result = GetBoost(rpm) * baseLevel;
            return Equals(Wastegate, 0d) ? Math.Min(Wastegate, result) : result;
        }

        [Pure, NotNull]
        public static TurboDescription FromIniSection(IniFileSection turboSection) {
            return new TurboDescription {
                MaxBoost = turboSection.GetDouble("MAX_BOOST", 0d),
                Wastegate = turboSection.GetDouble("WASTEGATE", 0d),
                ReferenceRpm = turboSection.GetDouble("REFERENCE_RPM", 1000d),
                Gamma = turboSection.GetDouble("GAMMA", 1d)
            };
        }
    }
}