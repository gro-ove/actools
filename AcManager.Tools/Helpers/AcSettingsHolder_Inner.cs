using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        private static Dictionary<string, FileSystemWatcher> _watcher = new Dictionary<string, FileSystemWatcher>();

        private static FileSystemWatcher GetWatcher(string directory) {
            FileSystemWatcher result;
            if (_watcher.TryGetValue(directory, out result)) return result;
            
            Directory.CreateDirectory(directory);
            result = new FileSystemWatcher {
                Path = directory,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*",
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            _watcher[directory] = result;
            return result;
        }

        public abstract class IniSettings : NotifyPropertyChanged {
            public string Filename { get; }

            protected IniSettings(string name, bool reload = true, bool systemConfig = false) {
                try {
                    var directory = systemConfig ? FileUtils.GetSystemCfgDirectory(AcRootDirectory.Instance.RequireValue) :
                            FileUtils.GetDocumentsCfgDirectory();

                    Filename = Path.Combine(directory, name + ".ini");
                    if (reload) {
                        Reload();
                    }

                    var watcher = GetWatcher(directory);
                    watcher.Changed += OnChanged;
                    watcher.Created += OnChanged;
                    watcher.Deleted += OnChanged;
                    watcher.Renamed += OnRenamed;
                } catch (Exception e) {
                    Logging.Warning("IniSettings exception: " + e);
                }
            }

            protected virtual void OnRenamed(object sender, RenamedEventArgs e) {
                if (FileUtils.IsAffected(e.OldFullPath, Filename) || FileUtils.IsAffected(e.FullPath, Filename)) {
                    ReloadLater();
                }
            }

            protected virtual void OnChanged(object sender, FileSystemEventArgs e) {
                if (FileUtils.IsAffected(e.FullPath, Filename)) {
                    ReloadLater();
                }
            }

            protected void Reload() {
                Ini = new IniFile(Filename);
                _loading = true;
                LoadFromIni();
                _loading = false;
            }

            private bool _reloading;
            private bool _loading;
            private DateTime _lastSaved;

            private async void ReloadLater() {
                if (_reloading || _saving || DateTime.Now - _lastSaved < TimeSpan.FromSeconds(1)) return;

                _reloading = true;
                await Task.Delay(200);

                try {
                    int i;
                    for (i = 0; i < 5; i++) {
                        try {
                            Ini = new IniFile(Filename);
                            break;
                        } catch (Exception) {
                            await Task.Delay(100);
                        }
                    }

                    if (i == 5) {
                        Logging.Warning("Can't load config file: " + Path.GetFileName(Filename));
                        return;
                    }

                    _loading = true;
                    LoadFromIni();
                    _loading = false;
                } finally {
                    _reloading = false;
                }
            }

            private bool _saving;

            protected async void Save() {
                if (_saving || _loading) return;

                _saving = true;
                await Task.Delay(500);

                try {
                    SetToIni();
                    Ini.Save(Filename);
                    _lastSaved = DateTime.Now;
                } catch (Exception e) {
                    NonfatalError.Notify("Can't save AC settings", "Make sure app has access to cfg folder.", e);
                } finally {
                    _saving = false;
                }
            }

            protected void ForceSave() {
                var l = _loading;
                _loading = false;
                Save();
                _loading = l;
            }

            protected IniFile Ini;

            /// <summary>
            /// Called from IniSettings constructor!
            /// </summary>
            protected abstract void LoadFromIni();

            protected abstract void SetToIni();

            [NotifyPropertyChangedInvocator]
            protected override void OnPropertyChanged([CallerMemberName] string propertyName = null) {
                base.OnPropertyChanged(propertyName);
                Save();
            }

            [NotifyPropertyChangedInvocator]
            protected void OnPropertyChanged(bool save = true, [CallerMemberName] string propertyName = null) {
                base.OnPropertyChanged(propertyName);
                if (save) {
                    Save();
                }
            }
        }

        private class InnerZeroToOffConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                double d;
                return value == null || double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d) && Equals(d, 0d)
                        ? (parameter ?? "Off") : value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                return value == parameter || value as string ==  "Off" ? 0d : value;
            }
        }

        public static IValueConverter ZeroToOffConverter { get; } = new InnerZeroToOffConverter();
    }
}
