using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using FirstFloor.ModernUI.Helpers;
using SharpCompress.Archives.Zip;

namespace AcManager.Tools.Helpers {
    public enum BeepingNoiseType {
        [Description("Disabled")]
        Disabled = 0,

        [Description("System")]
        System = 1,

        [Description("Custom")]
        Custom = 2
    }

    public static class BeepingNoise {
        public static async Task Play(BeepingNoiseType type) {
            switch (type) {
                case BeepingNoiseType.Disabled:
                    break;
                case BeepingNoiseType.System:
                    SystemSounds.Exclamation.Play();
                    break;
                case BeepingNoiseType.Custom:
                    try {
                        var data = await CmApiProvider.GetStaticDataBytesAsync("audio_error", TimeSpan.FromDays(3));
                        if (data != null) {
                            using (var stream = new MemoryStream(data))
                            using (var zip = ZipArchive.Open(stream)) {
                                var entry = zip.Entries.FirstOrDefault(x => x.Key == @"error.wav");
                                if (entry == null) throw new Exception("Invalid data");
                                using (var s = entry.OpenEntryStream()) {
                                    new SoundPlayer(s).Play();
                                }
                            }
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                    break;
            }
        }
    }
}