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

        [Pure]
        public double CalculateMultipler(double rpm) {
            var baseLevel = Math.Min(1, Math.Pow(rpm / ReferenceRpm, Gamma));
            var boost = Controllers?.Aggregate<TurboControllerDescription, double>(
                    0, /* zero, because apparently AC resets boost to zero if controllers file exists */
                    (current, t) => t.Process(rpm, current)) ?? MaxBoost;
            return Math.Min(Wastegate, boost * baseLevel);
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