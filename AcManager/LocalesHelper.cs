using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager {
    [Localizable(false)]
    public static class LocalesHelper {
        public static readonly string[] SupportedLocales = { "en", "en-GB", "en-US" };

        /// <summary>
        /// Careful! Called before used libraries are plugged!
        /// </summary>
        public static void Initialize() {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            //var forceLocale = AppArguments.Get(AppFlag.ForceLocale);
            //if (forceLocale != null) {
            //    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(forceLocale);
            //} else if (!SupportedLocales.Contains(CultureInfo.CurrentUICulture.Name)) {
            //    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            //}

            var langId = AppArguments.Get(AppFlag.ForceLocale) ?? ValuesStorage.GetString("lang") ?? CultureInfo.CurrentUICulture.Name;
            Logging.Write("Locale: " + langId);

            if (SupportedLocales.Contains(langId)) {
                Logging.Write("Supported locale: " + langId);
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(langId);
                return;
            }

            var found = false;

            var locales = FilesStorage.Instance.GetDirectory("Locales");
            var localePackage = Path.Combine(locales, langId + ".pak");
            if (File.Exists(localePackage)) {
                LoadPackage(localePackage);
                found = true;
            }
            
            if (AppArguments.GetBool(AppFlag.UseCustomLocales, true)) {
                var localeDirectory = Path.Combine(locales, langId);
                if (Directory.Exists(localeDirectory)) {
                    Logging.Write("Custom locale: " + localeDirectory);
                    CustomResourceManager.SetCustomSource(localeDirectory);
                    found = true;
                }
            }

            if (found || AppArguments.GetBool(AppFlag.ForceLocale)) {
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(langId);
            } else {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }
        }

        [UsedImplicitly]
        private class PackageManifest {
            [JsonProperty("version")]
            public string Version { get; set; }
        }

        private static void LoadPackage(string filename) {
            try {
                Dictionary<string, Assembly> assemblies;

                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(fs)) {
                    var entry = zip.Entries.FirstOrDefault(x => x.FullName == "Manifest.json");
                    if (entry == null) throw new Exception("Manifest is missing");

                    var manifest = JsonConvert.DeserializeObject<PackageManifest>(entry.Open().ReadAsBytesAndDispose().ToUtf8String());
                    Logging.Write("Locale package: " + manifest.Version);

                    assemblies = zip.Entries.Where(x => x.FullName.EndsWith(".resources.dll"))
                                    .ToDictionary(x => x.FullName.ApartFromLast(".dll"), x => Assembly.Load(x.Open().ReadAsBytesAndDispose()));
                }

                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                    var splitted = args.Name.Split(',');
                    var name = splitted[0];

                    Assembly result;
                    return assemblies.TryGetValue(name, out result) ? result : null;
                };
            } catch (Exception e) {
                Logging.Warning("Cannot load locale package: " + e);
            }
        }
    }
}