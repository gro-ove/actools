using System;
using System.IO;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.AcObjectsNew {
    public abstract class AcIniObject : AcCommonObject {
        protected AcIniObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
        }

        public void ReloadIniData() {
            ClearErrors(AcErrorCategory.Data);
            LoadIniOrThrow();
            Changed = false;
        }

        public override bool HandleChangedFile(string filename) {
            if (!FileUtils.IsAffected(filename, IniFilename)) return false;

            if (!Changed || ModernDialog.ShowMessage(@"Ini-file updated. Reload? All changes will be lost.", @"Reload file?", MessageBoxButton.YesNo) ==
                    MessageBoxResult.Yes) {
                ReloadIniData();
            }

            return true;
        }

        public string IniFilename { get; protected set; }

        private IniFile _iniObject;

        public IniFile IniObject {
            get { return _iniObject; }
            private set {
                if (_iniObject == value) return;

                _iniObject = value;
                OnPropertyChanged(nameof(HasData));
            }
        }

        public override bool HasData => _iniObject != null;

        #region Loading and saving
        protected override void LoadOrThrow() {
            LoadIniOrThrow();
        }

        private void LoadIniOrThrow() {
            string text;

            try {
                text = FileUtils.ReadAllText(IniFilename);
            } catch (FileNotFoundException) {
                AddError(AcErrorType.Data_IniIsMissing, Path.GetFileName(IniFilename));
                return;
            } catch (DirectoryNotFoundException) {
                AddError(AcErrorType.Data_IniIsMissing, Path.GetFileName(IniFilename));
                return;
            }

            try {
                IniObject = IniFile.Parse(text);
            } catch (Exception) {
                IniObject = null;
                AddError(AcErrorType.Data_IniIsDamaged, Path.GetFileName(IniFilename));
                return;
            }

            LoadData(IniObject);
        }

        protected abstract void LoadData(IniFile ini);

        public abstract void SaveData(IniFile ini);

        public override void Save() {
            var ini = IniObject;
            SaveData(ini);

            using ((FileAcManager as IIgnorer)?.IgnoreChanges()) {
                File.WriteAllText(IniFilename, ini.ToString());
            }

            Changed = false;
        }
        #endregion

        
    }
}
