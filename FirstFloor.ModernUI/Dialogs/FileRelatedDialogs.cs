using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace FirstFloor.ModernUI.Dialogs {
    public sealed class DialogFilterPiece : Displayable {
        public static readonly DialogFilterPiece AllFiles = new DialogFilterPiece("All files", "*.*");
        public static readonly DialogFilterPiece DdsFiles = new DialogFilterPiece("DDS files", "*.dds");
        public static readonly DialogFilterPiece XmlFiles = new DialogFilterPiece("XML Files", "*.xml");
        public static readonly DialogFilterPiece IniFiles = new DialogFilterPiece("INI Files", "*.ini");
        public static readonly DialogFilterPiece JpegFiles = new DialogFilterPiece("JPEG files", "*.jpg", "*.jpeg");
        public static readonly DialogFilterPiece PngFiles = new DialogFilterPiece("PNG files", "*.png");
        public static readonly DialogFilterPiece DdsAndTiffFiles = new DialogFilterPiece("DDS & TIFF files", "*.dds", "*.tif", "*.tiff");
        public static readonly DialogFilterPiece ImageFiles = new DialogFilterPiece("Image files", "*.dds", "*.tif", "*.tiff", "*.jpg", "*.jpeg", "*.png");
        public static readonly DialogFilterPiece Applications = new DialogFilterPiece("Applications", "*.exe");
        public static readonly DialogFilterPiece ZipFiles = new DialogFilterPiece("ZIP archives", "*.zip");
        public static readonly DialogFilterPiece TextFiles = new DialogFilterPiece("Text files", "*.txt");
        public static readonly DialogFilterPiece ConfigFiles = new DialogFilterPiece("Config files", "*.cfg");
        public static readonly DialogFilterPiece FbxFiles = new DialogFilterPiece("FBX models", "*.fbx");
        public static readonly DialogFilterPiece Kn5Files = new DialogFilterPiece("KN5 models", "*.kn5");
        public static readonly DialogFilterPiece LutTables = new DialogFilterPiece("LUT tables", "*.lut");
        public static readonly DialogFilterPiece CsvTables = new DialogFilterPiece("LUT tables", "*.csv");
        public static readonly DialogFilterPiece TarGZipFiles = new DialogFilterPiece("Tar GZip archives", "*.tar.gz");
        public static readonly DialogFilterPiece DynamicLibraries = new DialogFilterPiece("Dynamic libraries", "*.dll");
        public static readonly DialogFilterPiece Archives = new DialogFilterPiece("Archives",
                "*.zip", "*.rar", "*.7z", "*.gzip", "*.tar", "*.tar.gz", "*.bz2");

        public DialogFilterPiece([NotNull] string displayName, [Localizable(false), NotNull] params string[] filter) {
            Filter = string.Join(";", filter);
            ShortName = displayName;
            DisplayName = $"{ShortName} ({Filter})";
            BaseExtension = filter.Length > 0 && filter[0].StartsWith("*.") ? filter[0].Substring(1) : null;
        }

        [NotNull]
        public string Filter { get; }

        [NotNull]
        public string ShortName { get; }

        [CanBeNull]
        public string BaseExtension { get; }

        [NotNull]
        public string WinFilter => $"{DisplayName}|{Filter}";
    }

    public abstract class DialogParamsBase {
        [CanBeNull]
        public string Title { get; set; }

        [CanBeNull]
        public string InitialDirectory { get; set; }

        [Localizable(false), CanBeNull]
        public string DirectorySaveKey { get; set; }

        [CanBeNull]
        internal string ActualSaveKey => DirectorySaveKey == null ? null : "dialogs:" + DirectorySaveKey;
    }

    public abstract class FileDialogParamsBase : DialogParamsBase {
        [NotNull]
        public List<FileDialogCustomPlace> CustomPlaces { get; set; } = new List<FileDialogCustomPlace>();

        [NotNull]
        public List<DialogFilterPiece> Filters { get; set; } = new List<DialogFilterPiece>();

        [CanBeNull]
        public string DetaultExtension { get; set; }

        [CanBeNull]
        public string DefaultFileName { get; set; }

        public bool IncludeAllFiles { get; set; } = true;
        public bool DereferenceLinks { get; set; } = true;
        public bool ValidateNames { get; set; } = true;
        public bool RestoreDirectory { get; set; } = false;

        internal IEnumerable<DialogFilterPiece> GetFilters() {
            return !IncludeAllFiles ? Filters : Filters.Concat(new[] { DialogFilterPiece.AllFiles });
        }
    }

    public class OpenDialogParams : FileDialogParamsBase {
        public bool CheckFileExists { get; set; } = true;
        public bool UseCachedIfAny { get; set; } = false;
    }

    public class SaveDialogParams : FileDialogParamsBase {
        public bool AddExtension { get; set; } = true;
        public bool CheckPathExists { get; set; } = true;
        public bool OverwritePrompt { get; set; } = true;
        public bool CreatePrompt { get; set; } = false;
    }

    public static class FileRelatedDialogs {
        [CanBeNull]
        private static string OpenFallback([CanBeNull] string key) {
            var dialog = new OpenFileDialog();

            if (dialog.ShowDialog() != true) {
                return null;
            }

            if (key != null) {
                ValuesStorage.Set(key, Path.GetDirectoryName(dialog.FileName));
            }

            return dialog.FileName;
        }

        [CanBeNull]
        private static string SaveFallback([CanBeNull] string key) {
            var dialog = new SaveFileDialog();
            if (dialog.ShowDialog() != true) {
                return null;
            }

            if (key != null) {
                ValuesStorage.Set(key, Path.GetDirectoryName(dialog.FileName));
            }

            return dialog.FileName;
        }

        private static OpenFileDialog ConfigureOpenDialog([NotNull] OpenDialogParams p, string currentFilename, bool multiselect) {
            var filters = p.GetFilters().ToList();
            var dialog = new OpenFileDialog {
                Filter = string.Join("|", filters.Select(x => x.WinFilter)),
                CheckFileExists = p.CheckFileExists,
                RestoreDirectory = p.RestoreDirectory,
                DereferenceLinks = p.DereferenceLinks,
                ValidateNames = p.ValidateNames,
                Multiselect = multiselect
            };

            var extension = p.DetaultExtension ?? filters[0].BaseExtension;
            if (extension != null) {
                dialog.DefaultExt = extension;
            }

            if (p.Title != null) {
                dialog.Title = p.Title;
            }

            var key = p.ActualSaveKey;
            var initial = key == null ? p.InitialDirectory :
                    ValuesStorage.Get(key, p.InitialDirectory);
            if (initial != null) {
                dialog.InitialDirectory = initial;
            }

            foreach (var place in p.CustomPlaces) {
                dialog.CustomPlaces.Add(place);
            }

            if (currentFilename != null) {
                dialog.InitialDirectory = Path.GetDirectoryName(currentFilename) ?? "";
                dialog.FileName = Path.GetFileNameWithoutExtension(currentFilename);
            }

            if (dialog.ShowDialog() != true) {
                return null;
            }

            if (key != null) {
                ValuesStorage.Set(key, Path.GetDirectoryName(dialog.FileName));
            }

            return dialog;
        }

        [CanBeNull]
        public static string Open([NotNull] OpenDialogParams p, string currentFilename = null) {
            var key = p.ActualSaveKey;
            var cached = p.ActualSaveKey + @":cached";

            try {
                if (p.UseCachedIfAny && ValuesStorage.Contains(cached) && File.Exists(ValuesStorage.Get<string>(cached))) {
                    return ValuesStorage.Get<string>(cached);
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            try {
                var dialog = ConfigureOpenDialog(p, currentFilename, false);
                if (dialog == null) return null;
                if (p.UseCachedIfAny) {
                    ValuesStorage.Set(cached, dialog.FileName);
                }
                return dialog.FileName;
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t use Open File Dialog properly", e);
                return OpenFallback(key);
            }
        }

        [CanBeNull]
        public static string[] OpenMultiple([NotNull] OpenDialogParams p, string currentFilename = null) {
            var key = p.ActualSaveKey;
            var cached = p.ActualSaveKey + @":cached";

            try {
                if (p.UseCachedIfAny && ValuesStorage.Contains(cached) && ValuesStorage.GetStringList(cached).All(File.Exists)) {
                    return ValuesStorage.GetStringList(cached).ToArray();
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            try {
                var dialog = ConfigureOpenDialog(p, currentFilename, true);
                if (dialog == null || dialog.FileNames.Length == 0) return null;
                if (p.UseCachedIfAny) {
                    ValuesStorage.Set(cached, dialog.FileNames);
                }
                return dialog.FileNames.Distinct().ToArray();
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t use Open File Dialog properly", e);
                return new[]{ OpenFallback(key) };
            }
        }

        [CanBeNull]
        public static string Save([NotNull] SaveDialogParams p, string currentFilename = null) {
            var key = p.ActualSaveKey;
            try {
                var filters = p.GetFilters().ToList();
                var dialog = new SaveFileDialog {
                    Filter = string.Join("|", filters.Select(x => $"{x.DisplayName}|{x.Filter}")),
                    AddExtension = p.AddExtension,
                    CheckPathExists = p.CheckPathExists,
                    CreatePrompt = p.CreatePrompt,
                    OverwritePrompt = p.OverwritePrompt,
                    RestoreDirectory = p.RestoreDirectory,
                    DereferenceLinks = p.DereferenceLinks,
                    ValidateNames = p.ValidateNames,
                };

                var extension = p.DetaultExtension ?? filters[0].BaseExtension;
                if (extension != null) {
                    dialog.DefaultExt = extension;
                }

                if (p.Title != null) {
                    dialog.Title = p.Title;
                }

                if (p.DefaultFileName != null) {
                    dialog.FileName = p.DefaultFileName;
                }

                var initial = key == null ? p.InitialDirectory :
                        ValuesStorage.Get(key, p.InitialDirectory);
                if (initial != null) {
                    dialog.InitialDirectory = initial;
                }

                foreach (var place in p.CustomPlaces) {
                    dialog.CustomPlaces.Add(place);
                }

                if (currentFilename != null) {
                    dialog.InitialDirectory = Path.GetDirectoryName(currentFilename) ?? "";
                    dialog.FileName = Path.GetFileNameWithoutExtension(currentFilename);
                }

                if (dialog.ShowDialog() != true) {
                    return null;
                }

                if (key != null) {
                    ValuesStorage.Set(key, Path.GetDirectoryName(dialog.FileName));
                }

                return dialog.FileName;
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t use Save File Dialog properly", e);
                return SaveFallback(key);
            }
        }
    }
}