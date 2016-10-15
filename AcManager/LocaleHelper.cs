using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager {
    [Localizable(false)]
    public static class LocaleHelper {
        public static string SystemCultureName { get; private set; }

        public static bool JustUpdated { get; private set; }

        [CanBeNull]
        public static string LoadedVersion { get; private set; }

        private static readonly string[] SupportedLocales = { "en", "en-gb", "en-us" };

        public static bool IsSupported(string id) {
            return SupportedLocales.Contains(id?.ToLowerInvariant());
        }

        public static async Task InitializeAsync() {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            SystemCultureName = CultureInfo.CurrentUICulture.Name;

            var langId = AppArguments.Get(AppFlag.ForceLocale) ?? SettingsHolder.Locale.LocaleName;

            bool found;
            if (IsSupported(langId)) {
                found = true;
            } else {
                var package = FilesStorage.Instance.GetFilename("Locales", langId + ".pak");
                if (File.Exists(package)) {
                    await LoadPackage(langId, package);
                    found = true;
                } else {
                    found = await TryToLoadPackage(langId, package);
                    if (found) {
                        Logging.Write("Package loaded");
                    }
                }
            }

            if (SettingsHolder.Locale.LoadUnpacked) {
                found = InitializeCustom(langId) || found;
            }

            if (found || AppArguments.GetBool(AppFlag.ForceLocale)) {
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(langId);
            } else {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }
        }

        private static bool InitializeCustom(string langId) {
            var found = false;

            var locales = FilesStorage.Instance.GetDirectory("Locales");
            var googleSheets = Path.Combine(locales, "google-sheets-export.xlsx");
            if (File.Exists(googleSheets)) {
                try {
                    var loaded = SharedLocaleReader.Read(googleSheets, langId);
                    if (loaded.Any()) {
                        CustomResourceManager.SetCustomSource(loaded);
                        found = true;
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            var localeDirectory = Path.Combine(locales, langId);
            if (Directory.Exists(localeDirectory)) {
                Logging.Write(localeDirectory);
                CustomResourceManager.SetCustomSource(localeDirectory);
                found = true;
            }

            return found;
        }

        private static Dictionary<string, Assembly> LoadAssemblies(ZipArchive archive) {
            return archive.Entries.Where(x => x.FullName.EndsWith(".resources.dll"))
                          .ToDictionary(x => x.FullName.ApartFromLast(".dll"), x => Assembly.Load(x.Open().ReadAsBytesAndDispose()));
        }

        private static void SetPackage(IReadOnlyDictionary<string, Assembly> package) {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                var splitted = args.Name.Split(',');
                var name = splitted[0];

                Assembly result;
                return package.TryGetValue(name, out result) ? result : null;
            };
        }

        [ItemCanBeNull]
        private static async Task<byte[]> LoadPackageTimeout(string langId, string version = "0") {
            if (!SettingsHolder.Locale.UpdateOnStart) return null;
            using (var cancellation = new CancellationTokenSource()) {
                cancellation.CancelAfter(500);
                var data = await CmApiProvider.GetDataAsync($"locales/update/{langId}/{version}",
                        cancellation: cancellation.Token);

                if (cancellation.IsCancellationRequested) {
                    Logging.Write("Timeout exceeded");
                }

                return data == null || data.Length == 0 ? null : data;
            }
        }

        private static async Task<bool> TryToLoadPackage(string langId, string localePackage) {
            try {
                var data = await LoadPackageTimeout(langId);
                if (data != null) {
                    try {
                        File.WriteAllBytes(localePackage, data);
                        Logging.Warning("Locale updated");
                    } catch (Exception e) {
                        Logging.Warning("Cannot update locale: " + e);
                    }

                    using (var memory = new MemoryStream(data))
                    using (var updateZip = new ZipArchive(memory)) {
                        SetPackage(LoadAssemblies(updateZip));
                    }

                    return true;
                }
            } catch (Exception e) {
                Logging.Warning("Cannot try to load locale package: " + e);
            }

            return false;
        }

        private static async Task LoadPackage(string langId, string filename) {
            try {
                Dictionary<string, Assembly> assemblies;
                byte[] update;

                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(fs)) {
                    var manifest = LocalePackageManifest.FromArchive(zip);
                    if (manifest == null) throw new Exception("Manifest is missing");
                    
                    LoadedVersion = manifest.Version;
                    update = await LoadPackageTimeout(langId, manifest.Version);

                    if (update != null) {
                        using (var memory = new MemoryStream(update))
                        using (var updateZip = new ZipArchive(memory)) {
                            assemblies = LoadAssemblies(updateZip);

                            manifest = LocalePackageManifest.FromArchive(updateZip);
                            if (manifest == null) throw new Exception("Manifest is missing");

                            LoadedVersion = manifest.Version;
                        }
                    } else {
                        assemblies = LoadAssemblies(zip);
                    }
                }

                if (update != null) {
                    try {
                        JustUpdated = true;
                        File.WriteAllBytes(filename, update);
                        Logging.Write("Locale updated");
                    } catch (Exception e) {
                        Logging.Warning("Cannot update locale: " + e);
                    }
                }

                SetPackage(assemblies);
            } catch (Exception e) {
                Logging.Warning("Cannot load locale package: " + e);
            }
        }
    }
}