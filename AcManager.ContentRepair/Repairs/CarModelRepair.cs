using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Repairs {
    [UsedImplicitly]
    public class CarModelRepair : CarRepairBase {
        private static readonly string[] SuspensionNodes = { "SUSP_LF", "SUSP_RF", "SUSP_LR", "SUSP_RR" };

        private Task<bool> FixAsync([NotNull] CarObject car, Action<Kn5> fix, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing car…"));
            return Task.Run(() => {
                var kn5Filename = AcPaths.GetMainCarFilename(car.Location, car.AcdData, false);
                if (kn5Filename == null || !File.Exists(kn5Filename)) return false;

                var kn5 = Kn5.FromFile(kn5Filename);
                fix.Invoke(kn5);
                kn5.SaveRecyclingOriginal(kn5Filename);
                return true;
            });
        }

        public static async Task<bool> FixSuspensionNodesAsync(CarObject car, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing car…"));
            return await Task.Run(() => {
                var kn5Filename = AcPaths.GetMainCarFilename(car.Location, car.AcdData, false);
                if (kn5Filename == null || !File.Exists(kn5Filename)) return false;

                var kn5 = Kn5.FromFile(kn5Filename);
                FixSuspensionNodes(kn5);
                kn5.SaveRecyclingOriginal(kn5Filename);
                return true;
            });
        }

        private static void FixSuspensionNodes(Kn5 kn5) {
            foreach (var name in SuspensionNodes.Where(name => kn5.FirstByName(name) == null)) {
                var node = Kn5Node.CreateBaseNode(name);
                var wheel = kn5.FirstByName(name.Replace("SUSP", "WHEEL"))?.Transform;
                if (wheel != null) {
                    node.Transform = wheel;
                }

                kn5.RootNode.Children.Add(node);
            }
        }

        private ContentRepairSuggestion TestSuspensionNodes(CarObject car, Kn5 kn5) {
            if (SuspensionNodes.All(name => kn5.FirstByName(name) != null)) return null;

            return new ContentObsoleteSuggestion("Suspension nodes missing",
                    "Might cause crashes, especially in showroom.",
                    (p, c) => FixAsync(car, FixSuspensionNodes, p, c));
        }

        private static IEnumerable<Kn5Material.ShaderProperty> GetFresnelProperties(Kn5 kn5) {
            return kn5.Materials.Values
                      .Where(value => value.ShaderName.Contains(@"MultiMap") &&
                              value.GetPropertyByName("sunSpecular")?.ValueA > 0.5f &&
                              value.GetPropertyByName("useDetail")?.ValueA >= 1f)
                      .Select(x => x.GetPropertyByName("fresnelMaxLevel"))
                      .Where(x => x != null && x.ValueA >= 0.2);
        }

        private static void FixFrensel(Kn5 kn5) {
            foreach (var property in GetFresnelProperties(kn5).Where(x => x.ValueA <= 0.4)) {
                property.ValueA *= 2f;
            }
        }

        private ContentRepairSuggestion TestFrensel(CarObject car, Kn5 kn5) {
            var any = false;
            foreach (var property in GetFresnelProperties(kn5)) {
                any = true;
                if (property.ValueA > 0.4) return null;
            }

            if (!any) return null;
            return new ContentObsoleteSuggestion("Car paint might be not reflective enough",
                    "In one of updates, Kunos changed behavior of some shaders, and so now you need to set fresnelMaxLevel to be about two times bigger.",
                    (p, c) => FixAsync(car, FixFrensel, p, c));
        }

        public override IEnumerable<ContentRepairSuggestion> GetSuggestions(CarObject car) {
            var kn5Filename = AcPaths.GetMainCarFilename(car.Location, car.AcdData, false);
            if (kn5Filename == null || !File.Exists(kn5Filename)) return new ContentRepairSuggestion[0];

            var kn5 = Kn5.FromFile(kn5Filename);

            return new[] {
                TestFrensel(car, kn5),
                TestSuspensionNodes(car, kn5)
            }.NonNull();
        }

        public override bool AffectsData => false;
    }
}