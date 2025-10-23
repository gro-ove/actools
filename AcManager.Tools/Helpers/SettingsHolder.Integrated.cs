using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Starters;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class IntegratedSettings : NotifyPropertyChanged {
            internal IntegratedSettings() { }

            private DelayEntry[] _periodEntries;

            public DelayEntry[] Periods => _periodEntries ?? (_periodEntries = new[] {
                new DelayEntry(TimeSpan.FromHours(1)),
                new DelayEntry(TimeSpan.FromHours(3)),
                new DelayEntry(TimeSpan.FromHours(6)),
                new DelayEntry(TimeSpan.FromHours(12)),
                new DelayEntry(TimeSpan.FromDays(1))
            });

            private bool? _discordIntegration;

            public bool DiscordIntegration {
                get => _discordIntegration ?? (_discordIntegration = ValuesStorage.Get("Settings.IntegratedSettings.DiscordIntegration", true)).Value;
                set {
                    if (Equals(value, _discordIntegration)) return;
                    _discordIntegration = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.DiscordIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rsrLimitTemperature;

            public bool RsrLimitTemperature {
                get => _rsrLimitTemperature ?? (_rsrLimitTemperature = ValuesStorage.Get("Settings.IntegratedSettings.RsrLimitTemperature", true)).Value;
                set {
                    if (Equals(value, _rsrLimitTemperature)) return;
                    _rsrLimitTemperature = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.RsrLimitTemperature", value);
                    OnPropertyChanged();
                }
            }

            private bool? _dBoxIntegration;

            public bool DBoxIntegration {
                get => _dBoxIntegration ?? (_dBoxIntegration = ValuesStorage.Get("Settings.IntegratedSettings.DBoxIntegration", false)).Value;
                set {
                    if (Equals(value, _dBoxIntegration)) return;
                    _dBoxIntegration = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.DBoxIntegration", value);
                    OnPropertyChanged();
                }
            }

            private string _dBoxLocation;

            [CanBeNull]
            public string DBoxLocation {
                get => _dBoxLocation ?? (_dBoxLocation = ValuesStorage.Get("Settings.IntegratedSettings.DBoxLocation", ""));
                set {
                    value = value?.Trim();
                    if (Equals(value, _dBoxLocation)) return;
                    _dBoxLocation = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.DBoxLocation", value);
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _selectDBoxLocationCommand;

            public DelegateCommand SelectDBoxLocationCommand => _selectDBoxLocationCommand ?? (_selectDBoxLocationCommand = new DelegateCommand(() => {
                DBoxLocation = FileRelatedDialogs.Open(new OpenDialogParams {
                    DirectorySaveKey = "dbox",
                    InitialDirectory = Path.Combine(
                            Environment.GetEnvironmentVariable("ProgramFiles")?.Replace(@" (x86)", "") ?? @"C:\Program Files",
                            @"D-BOX\Gaming\Assetto Corsa"),
                    Filters = {
                        new DialogFilterPiece("D-Box Helper", "ACWithLiveMotion.exe"),
                        DialogFilterPiece.Applications,
                        DialogFilterPiece.AllFiles,
                    },
                    Title = "Select D-Box application",
                    DefaultFileName = Path.GetFileName(DBoxLocation),
                }) ?? DBoxLocation;
            }));

            private int? _dBoxDelay;

            public int DBoxDelay {
                get => _dBoxDelay ?? (_dBoxDelay = ValuesStorage.Get("Settings.IntegratedSettings.DBoxDelay", 30)).Value;
                set {
                    if (Equals(value, _dBoxDelay)) return;
                    _dBoxDelay = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.DBoxDelay", value);
                    OnPropertyChanged();
                }
            }

            public enum DBoxMode {
                Stock,
                CmCompatible
            }

            private AsyncCommand<DBoxMode> _switchDBoxModeCommand;

            public AsyncCommand<DBoxMode> SwitchDBoxModeCommand => _switchDBoxModeCommand ?? (_switchDBoxModeCommand = new AsyncCommand<DBoxMode>(async m => {
                try {
                    await new RegistryPatch {
                        [@"HKEY_LOCAL_MACHINE\SOFTWARE\D-BOX\PMG\AssettoCorsa"] = {
                            [@"DisableGameLaunch"] = m == DBoxMode.CmCompatible ? (object)true : null,
                            [@"ProcessName"] = m == DBoxMode.CmCompatible ? StarterBase.GetAcsName(Drive.Use32BitVersion) : null,
                        }
                    }.ApplyAsync(m == DBoxMode.CmCompatible ? "Switch D-BOX to CM-compatible mode" : "Switch D-BOX to default settings",
                            "D-BOX stores its settings in local machine section of Windows Registry, so admin privilegies are required to change them. Would you like for Content Manager to handle everything automatically, or just prepare a .reg-file for you to inspect and import manually?",
                            @"DBoxParams.reg");
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t change D-BOX mode", e);
                }
            }));
        }

        private static IntegratedSettings _integrated;
        public static IntegratedSettings Integrated => _integrated ?? (_integrated = new IntegratedSettings());
    }
}