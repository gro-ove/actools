using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    [Localizable(false)]
    public class LocalePackageManifest {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("coverity")]
        public double Coverity { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }


        [CanBeNull]
        public static LocalePackageManifest FromArchive(ZipArchive zip) {
            try {
                return JsonConvert.DeserializeObject<LocalePackageManifest>(zip.Entries.First(x => x.FullName == "Manifest.json")
                                                                               .Open().ReadAsBytesAndDispose().ToUtf8String());
            } catch (Exception e) {
                Logging.Warning("Cannot load manifest: " + e);
                return null;
            }
        }

        [CanBeNull]
        public static LocalePackageManifest FromPackage(string filename) {
            try {
                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var zip = new ZipArchive(fs)) {
                    return FromArchive(zip);
                }
            } catch (Exception e) {
                Logging.Warning("Cannot load manifest: " + e);
                return null;
            }
        }
    }
}