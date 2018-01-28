using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.Presentation {
    public static class ExtraProgressRings {
        private static Dictionary<string, Style> _styles;
        public static IReadOnlyDictionary<string, Style> Styles => _styles;

        public static void Initialize() {
            _styles = new Dictionary<string, Style> { ["Default"] = null };
            LoadStylesAsync();
        }

        private const string DataId = "loading_icons";

        private static void LoadStylesAsync() {
            LoadStylesImmediately();
            Task.Delay(3000).ContinueWith(t => EnsureStylesUpdated()).Forget();
        }

        private static void LoadStylesImmediately() {
            try {
                var filename = FilesStorage.Instance.GetTemporaryFilename("Static", $"{DataId}.zip");
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
                var data = await CmApiProvider.GetStaticDataBytesIfUpdatedAsync(DataId, TimeSpan.FromDays(3));
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
                _styles = ReadStyles(entry);
            }
        }

        [Pure]
        private static Dictionary<string, Style> ReadStyles([CanBeNull] Stream stream) {
            if (stream == null) return new Dictionary<string, Style> { ["Default"] = null };

            var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;

            var dictinary = (ResourceDictionary)XamlReader.Load(memory);
            var list = (string[])dictinary["ProgressRingStyles"];
            var result = new Dictionary<string, Style>(list.Length);
            foreach (var key in list) {
                result[key] = (Style)dictinary[key];
                result[key].Seal();
            }

            Logging.Write($"{result.Count} styles loaded");
            return result;
        }
    }
}