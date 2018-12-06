using System.Linq;
using AcManager.Tools.Helpers.Tyres;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.NeuralTyres;
using AcTools.NeuralTyres.Data;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Tyres {
    public static class NeuralTyresExtension {
        public static TyresEntry ToTyresEntry([NotNull] this NeuralTyresEntry values, [CanBeNull] TyresEntry original,
                [CanBeNull] string name, [CanBeNull] string shortName) {
            var mainSection = original?.MainSection.Clone() ?? new IniFileSection(null);
            var thermalSection = original?.ThermalSection.Clone() ?? new IniFileSection(null);

            mainSection.Set("NAME", name.Or(values.Name));
            mainSection.Set("SHORT_NAME", shortName.Or(values.ShortName));

            foreach (var key in values.Keys.ApartFrom(NeuralTyresEntry.TemporaryKeys)) {
                if (key.StartsWith(TyresExtension.ThermalPrefix)) {
                    var actualKey = key.Substring(TyresExtension.ThermalPrefix.Length);
                    thermalSection.Set(actualKey, Fix(values[key], key));
                } else {
                    mainSection.Set(key, Fix(values[key], key));
                }
            }

            return new TyresEntry(original?.SourceCarId, values.Version, mainSection, thermalSection,
                    original?.WearCurveData, original?.PerformanceCurveData, original?.RearTyres ?? false, null);

            double Fix(double value, string key) {
                var digits = TyresExtension.GetValueDigits(key);
                return digits.HasValue ? FlexibleParser.ParseDouble(value.ToString($@"F{digits.Value}"), value) : value;
            }
        }

        public static NeuralTyresEntry ToNeuralTyresEntry(this TyresEntry entry) {
            return new NeuralTyresEntry(entry.SourceCarId, entry.Version, entry.MainSection, entry.ThermalSection);
        }

        [NotNull]
        public static TyresEntry CreateTyresEntry([NotNull] this TyresMachine machine, double width, double radius, double profile,
                [CanBeNull] string name, [CanBeNull] string shortName) {
            return machine.Conjure(width, radius, profile).ToTyresEntry(null, name, shortName);
        }

        [NotNull]
        public static TyresEntry CreateTyresEntry([NotNull] this TyresMachine machine, [NotNull] TyresEntry original,
                [CanBeNull] string name, [CanBeNull] string shortName) {
            return machine.Conjure(original.Width, original.Radius, original.Radius - original.RimRadius)
                          .ToTyresEntry(original, name, shortName);
        }

        [NotNull]
        public static TyresSet CreateTyresSet([NotNull] this TyresMachine machine, [NotNull] TyresEntry frontOriginal, [NotNull] TyresEntry rearOriginal,
                [CanBeNull] string name, [CanBeNull] string shortName) {
            var front = CreateTyresEntry(machine, frontOriginal, name, shortName);
            var rear = CreateTyresEntry(machine, rearOriginal, name, shortName);
            return new TyresSet(front, rear);
        }

        [NotNull]
        public static TyresSet CreateTyresSet([NotNull] this TyresMachine machine, [NotNull] CarObject car,
                [CanBeNull] string name, [CanBeNull] string shortName) {
            var original = car.GetOriginalTyresSet() ?? car.GetTyresSets().First();
            var front = machine.CreateTyresEntry(original.Front, name, shortName);
            var rear = machine.CreateTyresEntry(original.Rear, name, shortName);
            return new TyresSet(front, rear);
        }
    }
}