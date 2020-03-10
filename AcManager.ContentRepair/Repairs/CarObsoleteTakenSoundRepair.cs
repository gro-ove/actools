using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Repairs {
    [UsedImplicitly]
    public class CarObsoleteTakenSoundRepair : CarRepairBase {
        private static readonly int OptionChecksumFastSize = 16000;

        [CanBeNull]
        private static string ChecksumFast(string filename) {
            var buffer = new byte[OptionChecksumFastSize];

            try {
                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    fs.Read(buffer, 0, buffer.Length);
                }
            } catch (Exception) {
                return null;
            }

            using (var md5 = new MD5CryptoServiceProvider()) {
                return Convert.ToBase64String(md5.ComputeHash(buffer));
            }
        }

        [CanBeNull]
        private static string GetSoundbankChecksum(string carId) {
            return ChecksumFast(Path.Combine(AcPaths.GetCarDirectory(AcRootDirectory.Instance.RequireValue, carId), @"sfx",
                    carId + @".bank"));
        }

        private async Task<bool> FixAsync([NotNull] CarObject car, CarObject donor, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing sound…"));
            await car.ReplaceSound(donor);
            return true;
        }

        public override IEnumerable<ContentRepairSuggestion> GetSuggestions(CarObject car) {
            var soundDonor = car.GetSoundOrigin().ConfigureAwait(false).GetAwaiter().GetResult();
            var soundDonorObject = soundDonor == null ? null : CarsManager.Instance.GetById(soundDonor);
            if (soundDonorObject == null || soundDonor == @"tatuusfa1") return new ContentRepairSuggestion[0];

            var soundDonorChecksum = GetSoundbankChecksum(soundDonor);
            if (soundDonorChecksum == null) return new ContentRepairSuggestion[0];

            var soundChecksum = GetSoundbankChecksum(car.Id);
            if (soundChecksum == null || soundChecksum == soundDonorChecksum) return new ContentRepairSuggestion[0];

            return new[] {
                new ContentObsoleteSuggestion("Sound might be obsolete",
                        $"Judging by GUIDs, it looks like sound for this car is taken from {soundDonorObject.DisplayName}, but soundbank is different. Most likely, it was taken before some update and might not work properly now.",
                        (p, c) => FixAsync(car, soundDonorObject, p, c))
            };
        }

        public override bool AffectsData => false;
    }
}