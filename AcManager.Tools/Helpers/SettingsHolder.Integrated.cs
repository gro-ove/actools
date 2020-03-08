using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using AcManager.Tools.Starters;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

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

            private DelayEntry _theSetupMarketCacheListPeriod;

            public DelayEntry TheSetupMarketCacheListPeriod {
                get {
                    var saved = ValuesStorage.Get<TimeSpan?>("Settings.IntegratedSettings.TheSetupMarketCacheListPeriod");
                    return _theSetupMarketCacheListPeriod ?? (_theSetupMarketCacheListPeriod = Periods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            Periods.ElementAt(3));
                }
                set {
                    if (Equals(value, _theSetupMarketCacheListPeriod)) return;
                    _theSetupMarketCacheListPeriod = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCacheListPeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private DelayEntry _theSetupMarketCacheDataPeriod;

            public DelayEntry TheSetupMarketCacheDataPeriod {
                get {
                    var saved = ValuesStorage.Get<TimeSpan?>("Settings.IntegratedSettings.TheSetupMarketCacheDataPeriod");
                    return _theSetupMarketCacheDataPeriod ?? (_theSetupMarketCacheDataPeriod = Periods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            Periods.ElementAt(2));
                }
                set {
                    if (Equals(value, _theSetupMarketCacheDataPeriod)) return;
                    _theSetupMarketCacheDataPeriod = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCacheDataPeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private bool? _theSetupMarketCacheServer;

            public bool TheSetupMarketCacheServer {
                get => _theSetupMarketCacheServer ?? (_theSetupMarketCacheServer =
                        ValuesStorage.Get("Settings.IntegratedSettings.TheSetupMarketCacheServer2", true)).Value;
                set {
                    if (Equals(value, _theSetupMarketCacheServer)) return;
                    _theSetupMarketCacheServer = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCacheServer2", value);
                    OnPropertyChanged();
                }
            }

            private bool? _theSetupMarketTab;

            public bool TheSetupMarketTab {
                get => _theSetupMarketTab ?? (_theSetupMarketTab = ValuesStorage.Get("Settings.IntegratedSettings.TheSetupMarketTab", true)).Value;
                set {
                    if (Equals(value, _theSetupMarketTab)) return;
                    _theSetupMarketTab = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketTab", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TheSetupMarketCounter));
                }
            }

            private bool? _theSetupMarketCounter;

            public bool TheSetupMarketCounter {
                get => TheSetupMarketTab && (_theSetupMarketCounter ??
                        (_theSetupMarketCounter = ValuesStorage.Get("Settings.IntegratedSettings.TheSetupMarketCounter", false)).Value);
                set {
                    if (Equals(value, _theSetupMarketCounter)) return;
                    _theSetupMarketCounter = value;
                    ValuesStorage.Set("Settings.IntegratedSettings.TheSetupMarketCounter", value);
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

            public enum DBoxMode {
                Stock,
                CmCompatible
            }

            private AsyncCommand<DBoxMode> _switchDBoxModeCommand;

            public AsyncCommand<DBoxMode> SwitchDBoxModeCommand => _switchDBoxModeCommand ?? (_switchDBoxModeCommand = new AsyncCommand<DBoxMode>(async m => {
                try {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\D-BOX\PMG\AssettoCorsa";
                    var values = new Dictionary<string, object> {
                        [@"DisableGameLaunch"] = m == DBoxMode.CmCompatible ? (object)true : null,
                        [@"ProcessName"] = m == DBoxMode.CmCompatible ? StarterBase.GetAcsName(Drive.Use32BitVersion) : null,
                    };

                    var data = new StringBuilder();
                    data.Append(@"Windows Registry Editor Version 5.00").Append(Environment.NewLine).Append(Environment.NewLine);
                    data.Append($@"[{path}]").Append(Environment.NewLine);
                    foreach (var p in values) {
                        data.Append('"').Append(p.Key).Append(@"""=").Append(ToRegValue(p.Value)).Append(Environment.NewLine);
                    }
                    data.Append(Environment.NewLine);

                    var filename = FilesStorage.Instance.GetTemporaryFilename(@"DBoxParams.reg");
                    File.WriteAllText(filename, data.ToString());

                    var response = MessageDialog.Show(
                            "To switch D-BOX to a mode in which it could work with custom launchers, some of its values in Windows Registry have to be changed (and those values are in HKEY_LOCAL_MACHINE, so admin privilegies are required). Would you like for Content Manager to handle everything automatically, or just prepare .REG-file for you to check and import manually?",
                            m == DBoxMode.CmCompatible ? "Switch D-BOX to CM-compatible mode" : "Switch D-BOX to default settings",
                            new MessageDialogButton(MessageBoxButton.YesNoCancel) {
                                [MessageBoxResult.Yes] = "Set parameters automatically",
                                [MessageBoxResult.No] = "Just prepare .REG-file"
                            });

                    if (response == MessageBoxResult.Cancel) return;
                    if (response == MessageBoxResult.No) {
                        WindowsHelper.ViewFile(filename);
                        return;
                    }

                    try {
                        var proc = ProcessExtension.Start("regedit.exe", new[] { filename },
                                new ProcessStartInfo { Verb = "runas" });
                        await proc.WaitForExitAsync().ConfigureAwait(false);
                        Logging.Debug("Done: " + proc.ExitCode);
                    } catch (Win32Exception ex) when (ex.ErrorCode != -1) {
                        Logging.Debug(ex.ErrorCode);
                        throw new InformativeException("Access denied",
                                "D-BOX app stores its settings in HKEY_LOCAL_MACHINE branch, which requires administrator privilegies to modify.");
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t change D-BOX mode", e);
                }

                string ToRegValue(object v) {
                    switch (v) {
                        case bool b:
                            return b ? @"dword:00000001" : @"dword:00000000";
                        case string s:
                            return JsonConvert.SerializeObject(s);
                        case null:
                            return @"-";
                        default:
                            return JsonConvert.SerializeObject(v.ToString());
                    }
                }
            }));
        }

        private static IntegratedSettings _integrated;
        public static IntegratedSettings Integrated => _integrated ?? (_integrated = new IntegratedSettings());
    }
}