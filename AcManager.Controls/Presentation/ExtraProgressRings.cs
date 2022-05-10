using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.Presentation {
    public static class ExtraProgressRings {
        public static string OptionAnimationDevelopment = null;

        private static Dictionary<string, Lazy<Style>> _styles = new Dictionary<string, Lazy<Style>> { ["Default"] = null };

        [NotNull]
        public static IReadOnlyDictionary<string, Lazy<Style>> StylesLazy => _styles;

        public static event EventHandler StylesUpdated;

        public static void Initialize() {
            LoadStylesAsync();
        }

        private const string DataId = "loading_icons";

        private static void LoadStylesAsync() {
            if (OptionAnimationDevelopment != null && File.Exists(OptionAnimationDevelopment)) {
                using (var stream = File.Open(OptionAnimationDevelopment, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    _styles = ReadStyles(stream);
                }
                SimpleDirectoryWatcher.WatchFile(OptionAnimationDevelopment, () => {
                    ActionExtension.InvokeInMainThreadAsync(() => {
                        using (var stream = File.Open(OptionAnimationDevelopment, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                            _styles = ReadStyles(stream);
                        }
                    });
                });
                return;
            }

            LoadStylesImmediately();
            Task.Delay(3000).ContinueWith(t => EnsureStylesUpdated()).Ignore();
        }

        private static void LoadStylesImmediately() {
            try {
                var filename = FilesStorage.Instance.GetFilename("Static", $"{DataId}.zip");
                if (File.Exists(filename)) {
                    using (var stream = File.OpenRead(filename)) {
                        SetStylesFromArchive(stream);
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e.Message);
            }
        }

        private static async Task EnsureStylesUpdated() {
            try {
                var data = await CmApiProvider.GetStaticDataBytesIfUpdatedAsync(DataId, TimeSpan.FromDays(30));
                if (data != null) {
                    using (var stream = new MemoryStream(data, false)) {
                        SetStylesFromArchive(stream);
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e.Message);
            }
        }

        private static void SetStylesFromArchive(Stream stream) {
            using (var archive = new ZipArchive(stream))
            using (var entry = archive.GetEntry("ModernProgressRing.Imported.xaml")?.Open()) {
                _styles = ReadStyles(entry) ?? _styles;
            }
        }

        [Pure, CanBeNull]
        private static Dictionary<string, Lazy<Style>> ReadStyles([CanBeNull] Stream stream) {
            if (stream == null) return null;

            var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;

            var result = new Dictionary<string, Lazy<Style>> { ["Default"] = null };
            ActionExtension.InvokeInMainThreadAsync(() => {
                var dictionary = (ResourceDictionary)XamlReader.Load(memory);
                var list = (string[])dictionary["ProgressRingStyles"];
                foreach (var key in list) {
                    result[key] = new Lazy<Style>(() => {
                        try {
                            var ret = (Style)dictionary[key];
                            ret.Seal();
                            return ret;
                        } catch (Exception e) {
#if DEBUG
                            NonfatalError.Notify("Can’t parse style", e);
#endif
                            Logging.Warning(e);
                            return null;
                        }
                    });
                }
                Logging.Write($"{result.Count} styles loaded");
                StylesUpdated?.Invoke(null, EventArgs.Empty);
            });

            return result;
        }
    }
}