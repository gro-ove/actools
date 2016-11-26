using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Tools.Managers.Presets {
    public class PresetsManager : AbstractFilesStorage {
        public const string FileExtension = ".cmpreset";

        public static void Initialize(string path) {
            Debug.Assert(Instance == null);
            Instance = new PresetsManager(path);
        }

        public static PresetsManager Instance { get; private set; }

        private PresetsManager(string path = null)
            : base(path) {
            Debug.Assert(path != null);
            _builtInPresets = new Dictionary<string, List<BuiltInPresetEntry>>(2);
        }

        private readonly Dictionary<string, List<BuiltInPresetEntry>> _builtInPresets;

        private List<BuiltInPresetEntry> GetBuiltInPresetsList(string category) {
            if (_builtInPresets.ContainsKey(category)) return _builtInPresets[category];
            return _builtInPresets[category] = new List<BuiltInPresetEntry>(1);
        }

        public void RegisterBuiltInPreset(byte[] data, string category, params string[] localFilename) {
            var directory = GetDirectory(category);
            GetBuiltInPresetsList(category).Add(new BuiltInPresetEntry(
                directory,
                Path.Combine(directory, Path.Combine(localFilename)) + FileExtension,
                data
            ));
        }

        public IEnumerable<ISavedPresetEntry> GetSavedPresets(string category) {
            var directory = GetDirectory(category);
            var filesList = FileUtils.GetFilesRecursive(directory)
                                     .Where(x => x.ToLowerInvariant().EndsWith(FileExtension))
                                     .Select(x => new SavedPresetEntry(directory, x))
                                     .ToList<ISavedPresetEntry>();
            return filesList.Union(GetBuiltInPresetsList(category)
                                       .Where(x => filesList.All(y => x.Filename != y.Filename))).OrderBy(x => x.Filename);
        }

        public static event EventHandler<PresetSavedEventArgs> PresetSaved;

        public bool SavePresetUsingDialog(string key, string category, string data, string filename) {
            if (data == null) {
                return false;
            }

            var presetsDirectory = EnsureDirectory(category);

            var dialog = new SaveFileDialog {
                InitialDirectory = presetsDirectory,
                Filter = string.Format(ToolsStrings.Presets_FileFilter, FileExtension),
                DefaultExt = FileExtension
            };

            if (filename != null) {
                dialog.InitialDirectory = Path.GetDirectoryName(filename);
                dialog.FileName = Path.GetFileNameWithoutExtension(filename);
            }

            if (dialog.ShowDialog() != true) {
                return false;
            }

            filename = dialog.FileName;
            if (!filename.StartsWith(presetsDirectory)) {
                if (ModernDialog.ShowMessage(ToolsStrings.Presets_ChooseFileInInitialDirectory,
                                             ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK) {
                    return SavePresetUsingDialog(key, category, data, filename);
                }
                
                return false;
            }
            
            File.WriteAllText(filename, data);
            PresetSaved?.Invoke(this, new PresetSavedEventArgs(key, filename));
            return true;
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
