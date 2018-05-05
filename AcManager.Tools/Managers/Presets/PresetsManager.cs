using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Tools.Managers.Presets {
    public class PresetsCategory {
        public const string DefaultFileExtension = ".cmpreset";

        public PresetsCategory([NotNull] string directoryName, string extension = null) {
            DirectoryName = directoryName;
            Extension = extension ?? DefaultFileExtension;
        }

        [NotNull]
        public readonly string DirectoryName, Extension;

        protected bool Equals(PresetsCategory other) {
            return string.Equals(DirectoryName, other.DirectoryName) && string.Equals(Extension, other.Extension);
        }

        public override bool Equals(object obj) {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((PresetsCategory)obj));
        }

        public override int GetHashCode() {
            unchecked {
                return (DirectoryName.GetHashCode() * 397) ^ Extension.GetHashCode();
            }
        }
    }

    public class PresetsManager : AbstractFilesStorage {
        public static void Initialize(string path) {
            Debug.Assert(Instance == null);
            Instance = new PresetsManager(path);
        }

        public static PresetsManager Instance { get; private set; }

        private PresetsManager(string path = null) : base(path) {
            Debug.Assert(path != null);
            _builtInPresets = new Dictionary<PresetsCategory, List<BuiltInPresetEntry>>(2);
        }

        #region “Overrides” with PresetsCategory
        public string GetDirectory(PresetsCategory category) {
            if (category.DirectoryName.IndexOf(':') != -1) return category.DirectoryName;
            return base.GetDirectory(category.DirectoryName);
        }

        public string EnsureDirectory(PresetsCategory category) {
            var directory = GetDirectory(category);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            return directory;
        }

        public ContentWatcher Watcher(PresetsCategory category) {
            return GetWatcher(GetDirectory(category));
        }
        #endregion

        public override string GetDirectory(params string[] file) {
            if (file.Length == 1 && file[0].IndexOf(':') != -1) return file[0];
            return base.GetDirectory(file);
        }

        public string GetPresetFilename(PresetsCategory category, string name) {
            return Path.Combine(Instance.GetDirectory(category.DirectoryName), name + category.Extension);
        }

        private readonly Dictionary<PresetsCategory, List<BuiltInPresetEntry>> _builtInPresets;

        private List<BuiltInPresetEntry> GetBuiltInPresetsList(PresetsCategory category) {
            return _builtInPresets.TryGetValue(category, out var value) ? value
                    : (_builtInPresets[category] = new List<BuiltInPresetEntry>(10));
        }

        private IEnumerable<ISavedPresetEntry> GetDataPresets(PresetsCategory category) {
            if (category.DirectoryName.IndexOf(':') != -1) return new BuiltInPresetEntry[0];
            var directory = FilesStorage.Instance.GetDirectory(FilesStorage.DataDirName, ContentCategory.BuiltInPresets, category.DirectoryName);
            var virtualDirectory = GetDirectory(category);
            return FileUtils.GetFilesRecursive(directory, @"*" + category.Extension)
                            .TryToSelect(x => {
                                var relative = FileUtils.GetPathWithin(x, directory);
                                return relative == null
                                        ? throw new Exception($"Can’t find relative path from “{x}” to “{directory}”")
                                        : new DataBuiltInPresetEntry(virtualDirectory, category.Extension, Path.Combine(virtualDirectory, relative), x);
                            }, e => Logging.Error(e))
                            .ToList<ISavedPresetEntry>();
        }

        private IEnumerable<ISavedPresetEntry> GetBuiltInPresets(PresetsCategory category) {
            return GetBuiltInPresetsList(category).Concat(GetDataPresets(category));
        }

        public bool HasBuiltInPreset(PresetsCategory category, string filename) {
            return _builtInPresets.GetValueOrDefault(category)?.Any(x => FileUtils.ArePathsEqual(x.VirtualFilename, filename)) == true;
        }

        public ISavedPresetEntry GetBuiltInPreset(PresetsCategory category, string filename) {
            return _builtInPresets.GetValueOrDefault(category)?.FirstOrDefault(x => FileUtils.ArePathsEqual(x.VirtualFilename, filename));
        }

        public void RegisterBuiltInPreset(byte[] data, PresetsCategory category, params string[] localFilename) {
            var directory = GetDirectory(category);
            GetBuiltInPresetsList(category).Add(new BuiltInPresetEntry(directory,
                    Path.Combine(directory, Path.Combine(localFilename)) + category.Extension, category.Extension, data));
        }

        public void RegisterBuiltInPreset(byte[] data, string categoryName, params string[] localFilename) {
            RegisterBuiltInPreset(data, new PresetsCategory(categoryName), localFilename);
        }

        public void ClearBuiltInPresets(PresetsCategory category) {
            _builtInPresets.Remove(category);
        }

        public IEnumerable<ISavedPresetEntry> GetSavedPresets(PresetsCategory category) {
            var directory = GetDirectory(category);
            var filesList = FileUtils.GetFilesRecursive(directory, @"*" + category.Extension)
                                     .Select(x => new SavedPresetEntry(directory, category.Extension, x))
                                     .ToList<ISavedPresetEntry>();
            return filesList.Concat(GetBuiltInPresets(category)
                                       .Where(x => filesList.All(y => x.VirtualFilename != y.VirtualFilename))).OrderBy(x => x.VirtualFilename);
        }

        public static event EventHandler<PresetSavedEventArgs> PresetSaved;

        public bool SavePresetUsingDialog([CanBeNull] string key, [NotNull] PresetsCategory category, [CanBeNull] string data,
                [CanBeNull] ref string filename) {
            return SavePresetUsingDialog(key, category, data == null ? null : Encoding.UTF8.GetBytes(data), ref filename);
        }

        public bool SavePresetUsingDialog([CanBeNull] string key, [NotNull] PresetsCategory category, [CanBeNull] byte[] data,
                [CanBeNull] ref string filename) {
            if (data == null) {
                return false;
            }

            var presetsDirectory = EnsureDirectory(category);
            filename = FileRelatedDialogs.Save(new SaveDialogParams {
                Filters = { new DialogFilterPiece("Presets", "*" + category.Extension) },
                InitialDirectory = presetsDirectory,
                DetaultExtension = category.Extension,
                CustomPlaces = {
                    new FileDialogCustomPlace(presetsDirectory)
                }
            }, filename);
            if (filename == null) return false;

            if (!FileUtils.IsAffectedBy(filename, presetsDirectory)) {
                if (ModernDialog.ShowMessage(ToolsStrings.Presets_ChooseFileInInitialDirectory,
                        ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK) {
                    return SavePresetUsingDialog(key, category, data, ref filename);
                }

                return false;
            }

            File.WriteAllBytes(filename, data);
            Logging.Debug("Preset saved as " + filename);

            if (key != null) {
                PresetSaved?.Invoke(this, new PresetSavedEventArgs(key, filename));
            }

            return true;
        }

        public bool SavePresetUsingDialog([CanBeNull] string key, [NotNull] PresetsCategory category, [CanBeNull] string data,
                [CanBeNull] string filename) {
            return SavePresetUsingDialog(key, category, data == null ? null : Encoding.UTF8.GetBytes(data), filename);
        }

        public bool SavePresetUsingDialog([CanBeNull] string key, [NotNull] PresetsCategory category, [CanBeNull] byte[] data,
                [CanBeNull] string filename) {
            return SavePresetUsingDialog(key, category, data, ref filename);
        }
    }

    public class PresetSavedEventArgs : EventArgs {
        public string Key { get; }

        public string Filename { get; }

        public PresetSavedEventArgs(string key, string filename) {
            Key = key;
            Filename = filename;
        }
    }
}
