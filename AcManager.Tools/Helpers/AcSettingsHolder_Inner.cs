using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
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
        private static readonly Dictionary<string, FileSystemWatcher> Watchers = new Dictionary<string, FileSystemWatcher>();

        private static FileSystemWatcher GetWatcher(string directory) {
            FileSystemWatcher result;
            if (Watchers.TryGetValue(directory, out result)) return result;
            
            Directory.CreateDirectory(directory);
            result = new FileSystemWatcher {
                Path = directory,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*",
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            Watchers[directory] = result;
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
                IsLoading = true;
                LoadFromIni();
                IsLoading = false;
            }

            private bool _reloading;
            private DateTime _lastSaved;

            protected bool IsLoading { get; private set; }

            protected void IgnoreChangesForAWhile() {
                _lastSaved = DateTime.Now;
            }

            private async void ReloadLater() {
                if (_reloading || _saving || DateTime.Now - _lastSaved < TimeSpan.FromSeconds(3)) return;

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
                        Logging.Warning("Can’t load config file: " + Path.GetFileName(Filename));
                        return;
                    }

                    IsLoading = true;
                    Application.Current.Dispatcher.Invoke(LoadFromIni);
                    IsLoading = false;
                } finally {
                    _reloading = false;
                }
            }

            private bool _saving;

            protected virtual async void Save() {
                if (_saving || IsLoading) return;

                _saving = true;
                await Task.Delay(500);

                if (!_saving) return;

                try {
                    SetToIni();
                    IgnoreChangesForAWhile();
                    await Ini.SaveAsAsync(Filename);
                } catch (Exception e) {
                    NonfatalError.Notify(Resources.AcSettings_CannotSave, Resources.AcSettings_CannotSave_Commentary, e);
                } finally {
                    _saving = false;
                }
            }

            public void SaveImmediately() {
                SetToIni();
                IgnoreChangesForAWhile();
                Ini.SaveAs(Filename);
                _saving = false;
            }

            protected void ForceSave() {
                var l = IsLoading;
                IsLoading = false;
                Save();
                IsLoading = l;
            }

            public IniFile Ini;

            /// <summary>
            /// Called from IniSettings constructor!
            /// </summary>
            protected abstract void LoadFromIni();

            protected abstract void SetToIni();

            [NotifyPropertyChangedInvocator]
            protected override void OnPropertyChanged([CallerMemberName] string propertyName = null) {
                OnPropertyChanged(true, propertyName);
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
                        ? (parameter ?? Resources.AcSettings_Off) : value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                return value == parameter || value as string ==  Resources.AcSettings_Off ? 0d : value;
            }
        }

        public static IValueConverter ZeroToOffConverter { get; } = new InnerZeroToOffConverter();
    }
}
