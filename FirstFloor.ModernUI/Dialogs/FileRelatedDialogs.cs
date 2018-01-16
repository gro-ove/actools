using System;
using System.Collections.Generic;
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
        public static readonly DialogFilterPiece JpegFiles = new DialogFilterPiece("JPEG files", "*.jpg", "*.jpeg");
        public static readonly DialogFilterPiece PngFiles = new DialogFilterPiece("PNG files", "*.png");
        public static readonly DialogFilterPiece DdsAndTiffFiles = new DialogFilterPiece("DDS & TIFF files", "*.dds", "*.tif", "*.tiff");
        public static readonly DialogFilterPiece ImageFiles = new DialogFilterPiece("Image files", "*.dds", "*.tif", "*.tiff", "*.jpg", "*.jpeg", "*.png");
        public static readonly DialogFilterPiece Applications = new DialogFilterPiece("Applications", "*.exe");
        public static readonly DialogFilterPiece ZipFiles = new DialogFilterPiece("ZIP archives", "*.zip");
        public static readonly DialogFilterPiece TextFiles = new DialogFilterPiece("Text files", "*.txt");
        public static readonly DialogFilterPiece LutTables = new DialogFilterPiece("LUT tables", "*.lut");
        public static readonly DialogFilterPiece CsvTables = new DialogFilterPiece("LUT tables", "*.csv");
        public static readonly DialogFilterPiece TarGZipFiles = new DialogFilterPiece("Tar GZip archives", "*.tar.gz");
        public static readonly DialogFilterPiece DynamicLibraries = new DialogFilterPiece("Dynamic libraries", "*.dll");
        public static readonly DialogFilterPiece Archives = new DialogFilterPiece("Tar GZip archives",
                "*.zip", "*.rar", "*.7z", "*.gzip", "*.tar", "*.tar.gz", "*.bz2");

        public DialogFilterPiece(string displayName, params string[] filter) {
            Filter = string.Join(";", filter);
            ShortName = displayName;
            DisplayName = $"{ShortName} ({Filter})";
            BaseExtension = filter.Length > 0 && filter[0].StartsWith("*.") ? filter[0].Substring(1) : null;
        }

        public string Filter { get; }
        public string ShortName { get; }
        public string BaseExtension { get; }

        public string WinFilter => $"{DisplayName}|{Filter}";
    }

    public abstract class DialogParamsBase {
        [CanBeNull]
        public string Title { get; set; }

        [CanBeNull]
        public string InitialDirectory { get; set; }

        [CanBeNull]
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
            return !IncludeAllFiles ? Filters : Filters.Concat(new[]{ DialogFilterPiece.AllFiles });
        }
    }

    public class OpenDialogParams : FileDialogParamsBase {
        public bool CheckFileExists { get; set; } = true;
    }

    public class SaveDialogParams : FileDialogParamsBase {
        public bool AddExtension { get; set; } = true;
        public bool CheckPathExists { get; set; } = true;
        public bool OverwritePrompt { get; set; } = true;
        public bool CreatePrompt { get; set; } = false;
    }

    public static class FileRelatedDialogs {
        private static string OpenFallback() {
            var dialog = new OpenFileDialog();
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static string SaveFallback() {
            var dialog = new SaveFileDialog();
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public static string Open(OpenDialogParams p, string currentFilename = null) {
            try {
                var key = p.ActualSaveKey;
                var filters = p.GetFilters().ToList();
                var dialog = new OpenFileDialog {
                    Filter = string.Join("|", filters.Select(x => x.WinFilter)),
                    DefaultExt = p.DetaultExtension ?? filters[0].BaseExtension,
                    CheckFileExists = p.CheckFileExists,
                    RestoreDirectory = p.RestoreDirectory,
                    DereferenceLinks = p.DereferenceLinks,
                    ValidateNames = p.ValidateNames,
                };

                if (p.Title != null) {
                    dialog.Title = p.Title;
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
                NonfatalError.NotifyBackground("Can’t use Open File Dialog properly", e);
                return OpenFallback();
            }
        }

        public static string Save(SaveDialogParams p, string currentFilename = null) {
            try {
                var key = p.ActualSaveKey;
                var filters = p.GetFilters().ToList();
                var dialog = new SaveFileDialog {
                    Filter = string.Join("|", filters.Select(x => $"{x.DisplayName}|{x.Filter}")),
                    DefaultExt = p.DetaultExtension ?? filters[0].BaseExtension,
                    AddExtension = p.AddExtension,
                    CheckPathExists = p.CheckPathExists,
                    CreatePrompt = p.CreatePrompt,
                    OverwritePrompt = p.OverwritePrompt,
                    RestoreDirectory = p.RestoreDirectory,
                    DereferenceLinks = p.DereferenceLinks,
                    ValidateNames = p.ValidateNames,
                };

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
                return SaveFallback();
            }
        }
    }
}