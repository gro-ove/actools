using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.ContentRepair;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentRepairUi.Critical {
    [UsedImplicitly]
    public class CarMissingSoundRepair : CarRepairBase {
        public static readonly CarMissingSoundRepair Instance = new CarMissingSoundRepair();

        private Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> FixAsync([NotNull] CarObject car, CarObject donor) {
            return async (p, c) => {
                p?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing sound…"));
                await car.ReplaceSound(donor);
                return true;
            };
        }

        private Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> RenameAsync([NotNull] CarObject car, string filename) {
            return (p, c) => {
                if (!File.Exists(car.SoundbankFilename)) {
                    File.Move(filename, car.SoundbankFilename);
                    return Task.FromResult(File.Exists(car.SoundbankFilename));
                }
                return Task.FromResult(false);
            };
        }

        [CanBeNull]
        private ContentRepairSuggestion CheckMissingSound(CarObject car) {
            if (car.Author == "Kunos" || File.Exists(car.SoundbankFilename)) {
                return null;
            }

            if (CheapRun) {
                return new CommonErrorSuggestion("Missing soundbank", "",
                        (p, c) => Task.FromResult(false));
            }

            var renamed = new DirectoryInfo(Path.GetDirectoryName(car.SoundbankFilename) ?? ".").GetFiles("*.bank")
                    .Where(x => x.Length > 1e6 && x.Length < 200e6).ToList();
            if (renamed.Count > 0) {
                var target = renamed.Count == 1 ? renamed[0].Name : "it";
                var ret = new CommonErrorSuggestion("Missing soundbank",
                        $"Seems like soundbank has an incorrect name. Would you like to rename {target} back to “{Path.GetFileName(car.SoundbankFilename)}”?",
                        RenameAsync(car, renamed[0].FullName)) {
                            AffectsData = false
                        };
                if (renamed.Count > 1) {
                    ret.FixCaption = $"Use “{renamed[0].Name}”";
                }
                foreach (var alternative in renamed.Skip(1)) {
                    ret.AlternateFix($"Use “{alternative.Name}”", RenameAsync(car, alternative.FullName));
                }
                return ret;
            }

            var soundDonor = car.GetSoundOrigin().ConfigureAwait(false).GetAwaiter().GetResult();
            var soundDonorObject = soundDonor == null ? null : CarsManager.Instance.GetById(soundDonor);
            if (soundDonorObject != null && soundDonor != @"tatuusfa1") {
                return new CommonErrorSuggestion("Missing soundbank",
                        $"Judging by GUIDs, it looks like sound for this car is taken from {soundDonorObject.DisplayName}, but soundbank is missing. Copy it here to stop AC from crashing?",
                        FixAsync(car, soundDonorObject)) {
                            AffectsData = false
                        };
            }

            return new CommonErrorSuggestion("Missing soundbank",
                    $"Seems like soundbank is missing and it might cause AC to crash. Replace it with something else?",
                    (progress, token) => CarSoundReplacer.Replace(car)) {
                        ShowProgressDialog = false,
                        AffectsData = false
                    };
        }

        public override IEnumerable<ContentRepairSuggestion> GetSuggestions(CarObject car) {
            return new[] {
                CheckMissingSound(car)
            }.NonNull();
        }

        public override bool AffectsData => false;
    }
}