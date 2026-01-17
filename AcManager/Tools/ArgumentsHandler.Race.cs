using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
        [CanBeNull]
        private static string GetSettings(NameValueCollection requestParams, string key) {
            var presetData = requestParams.Get(key + @"Data");
            var presetFile = requestParams.Get(key + @"File");
            return presetData != null ? presetData.FromCutBase64()?.ToUtf8String()
                    : presetFile != null ? File.ReadAllText(presetFile) : requestParams.Get(key);
        }

        private static async Task<ArgumentHandleResult> ProcessRaceQuick(CustomUriRequest custom) {
            var preset = GetSettings(custom.Params, @"preset") ?? throw new Exception(@"Settings are not specified");

            var assists = GetSettings(custom.Params, @"assists");
            if (assists != null && !UserPresetsControl.LoadSerializedPreset(AssistsViewModel.Instance.PresetableKey, assists)) {
                AssistsViewModel.Instance.ImportFromPresetData(assists);
            }

            if (custom.Params.GetFlag("loadPreset")) {
                QuickDrive.Show(serializedPreset: preset, forceAssistsLoading: custom.Params.GetFlag("loadAssists"));
                return ArgumentHandleResult.SuccessfulShow;
            }

            if (!await QuickDrive.RunAsync(serializedPreset: preset, forceAssistsLoading: custom.Params.GetFlag("loadAssists"))) {
                NonfatalError.Notify(AppStrings.Common_CannotStartRace, AppStrings.Arguments_CannotStartRace_Commentary);
                return ArgumentHandleResult.Failed;
            }

            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessRaceConfig(CustomUriRequest custom) {
            var config = GetSettings(custom.Params, @"config") ?? throw new Exception(@"Settings are not specified");

            var assists = GetSettings(custom.Params, @"assists");
            if (assists != null && !UserPresetsControl.LoadSerializedPreset(AssistsViewModel.Instance.PresetableKey, assists)) {
                AssistsViewModel.Instance.ImportFromPresetData(assists);
            }

            await GameWrapper.StartAsync(new Game.StartProperties {
                PreparedConfig = IniFile.Parse(config)
            });
            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessRaceOnline(NameValueCollection p) {
            // Required arguments
            var ip = p.Get(@"ip");
            var port = FlexibleParser.TryParseInt(p.Get(@"port"));
            var httpPort = FlexibleParser.TryParseInt(p.Get(@"httpPort"));
            var carId = p.Get(@"car");

            // Optional arguments
            var allowWithoutSteamId = p.GetFlag("allowWithoutSteamId");
            var carSkinId = p.Get(@"skin");
            var trackId = p.Get(@"track");
            var name = p.Get(@"name");
            var nationality = p.Get(@"nationality");
            var password = p.Get(@"plainPassword");
            var encryptedPassword = p.Get(@"password");

            if (string.IsNullOrWhiteSpace(ip)) {
                throw new InformativeException("IP is missing");
            }

            if (!port.HasValue) {
                throw new InformativeException("Port is missing or is in invalid format");
            }

            if (!httpPort.HasValue) {
                throw new InformativeException("HTTP port is missing or is in invalid format");
            }

            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(encryptedPassword)) {
                password = OnlineServer.DecryptSharedPassword(ip, httpPort.Value, encryptedPassword);
            }

            if (string.IsNullOrWhiteSpace(carId)) {
                throw new InformativeException("Car ID is missing");
            }

            var car = CarsManager.Instance.GetById(carId);
            if (car == null) {
                throw new InformativeException("Car is missing");
            }

            if (!string.IsNullOrWhiteSpace(carSkinId) && car.GetSkinById(carSkinId) == null) {
                throw new InformativeException("Car skin is missing");
            }

            var track = string.IsNullOrWhiteSpace(trackId) ? null : TracksManager.Instance.GetLayoutByKunosId(trackId);
            if (!string.IsNullOrWhiteSpace(trackId) && track == null) {
                throw new InformativeException("Track is missing");
            }

            if (!SteamIdHelper.Instance.IsReady && !allowWithoutSteamId) {
                throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);
            }

            await GameWrapper.StartAsync(new Game.StartProperties {
                BasicProperties = new Game.BasicProperties {
                    CarId = carId,
                    TrackId = track?.MainTrackObject.Id ?? @"imola",
                    TrackConfigurationId = track?.LayoutId,
                    CarSkinId = carSkinId,
                    DriverName = name,
                    DriverNationality = nationality
                },
                ModeProperties = new Game.OnlineProperties {
                    Guid = SteamIdHelper.Instance.Value,
                    ServerIp = ip,
                    ServerPort = port.Value,
                    ServerHttpPort = httpPort.Value,
                    Password = password,
                    RequestedCar = carId
                }
            });

            return ArgumentHandleResult.Successful;
        }

        private class FakeSource : IOnlineListSource {
            private ServerInformation _information;

            public FakeSource(string ip, int httpPort) {
                _information = new ServerInformation { Ip = ip, PortHttp = httpPort };
                Id = _information.Id;
            }

            public string Id { get; }

            public string DisplayName => "Temporary source";

            public event EventHandler Obsolete {
                add { }
                remove { }
            }

            public async Task<bool> LoadAsync(ListAddAsyncCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress,
                    CancellationToken cancellation) {
                // This source will load provided server, but only once — call .ReloadAsync() and server will be nicely removed.
                await callback(new[] { _information }.NonNull());
                _information = null;
                return true;
            }
        }

        public static async Task JoinInvitation([NotNull] string ip, int port, [CanBeNull] string password) {
            OnlineManager.EnsureInitialized();

            var list = OnlineManager.Instance.List;
            var source = new FakeSource(ip, port);
            var wrapper = new OnlineSourceWrapper(list, source);

            ServerEntry server;

            using (var waiting = new WaitingDialog()) {
                waiting.Report(ControlsStrings.Common_Loading);

                await wrapper.EnsureLoadedAsync();
                server = list.GetByIdOrDefault(source.Id);
                if (server == null) {
                    throw new Exception(@"Unexpected");
                }
            }

            if (password != null) {
                server.Password = password;
            }

            var content = new OnlineServer(server) {
                Margin = new Thickness(0, 0, 0, -38),
                ToolBar = { FitWidth = true },

                // Values taken from ModernDialog.xaml
                // TODO: Extract them to some style?
                Title = { FontSize = 24, FontWeight = FontWeights.Light, Margin = new Thickness(6, 0, 0, 8) }
            };

            content.Title.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);

            var dlg = new ModernDialog {
                ShowTitle = false,
                Content = content,
                MinHeight = 400,
                MinWidth = 450,
                MaxHeight = 99999,
                MaxWidth = 700,
                Padding = new Thickness(0),
                ButtonsMargin = new Thickness(8),
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = @".OnlineServerDialog"
            };

            dlg.SetBinding(Window.TitleProperty, new Binding {
                Path = new PropertyPath(nameof(server.DisplayName)),
                Source = server
            });

            dlg.ShowDialog();
            await wrapper.ReloadAsync(true);
        }

        private static async Task<ArgumentHandleResult> ProcessRaceCsp(NameValueCollection p) {
            // Required arguments
            var paramValue = p.Get(@"param");
            var modeId = p.Get(@"mode");
            var carId = p.Get(@"car") ?? @":";
            var skinId = p.Get(@"skin");
            if (carId.StartsWith(":")) {
                var car = await CarsManager.Instance.GetByIdAsync(ValuesStorage.Get(".cspracecmd.car", string.Empty));
                var skin = car?.GetSkinById(ValuesStorage.Get(".cspracecmd.skin", string.Empty));
                car = SelectCarDialog.Show(car, ref skin, carId.Length > 1 ? carId.Substring(1) : null);
                if (car == null) {
                    return ArgumentHandleResult.Failed;
                }
                carId = car.Id;
                skinId = skin?.Id;
                ValuesStorage.Set(".cspracecmd.car", carId);
                ValuesStorage.Set(".cspracecmd.skin", skinId);
            }

            var trackId = p.Get(@"track") ?? @":";
            if (trackId.StartsWith(":")) {
                var track = await TracksManager.Instance.GetLayoutByIdAsync(ValuesStorage.Get(".cspracecmd.track", string.Empty));
                track = SelectTrackDialog.Show(track, Tuple.Create(trackId.Length > 1 ? trackId.Substring(1) : null, (string)null));
                if (track == null) {
                    return ArgumentHandleResult.Failed;
                }
                trackId = track.IdWithLayout;
                ValuesStorage.Set(".cspracecmd.track", trackId);
            }

            // &cfg=SECTION[KEY]VALUE[KEY2]VALUE2&cfg=SECTION2[KEY]VALUE;
            var cfg = p.GetValues(@"cfg");
            if (cfg != null && cfg.Length > 0) {
                var iniFile = new IniFile(Path.Combine(AcPaths.GetDocumentsCfgDirectory(), PatchHelper.PatchDirectoryName,
                        @"state\lua\new_modes", $@"{modeId}__settings.ini"));
                foreach (var c in cfg) {
                    var s = c.Split('[');
                    if (s.Length > 1) {
                        var section = iniFile[s[0]];
                        for (var i = 1; i < s.Length; i++) {
                            var kv = s[i].Split(new[] { ']' }, 2, StringSplitOptions.None);
                            if (kv.Length == 2) {
                                section.SetOrRemove(kv[0], kv[1] == string.Empty ? null : kv[1]);
                            }
                        }
                    }
                }
                iniFile.Save();
            }

            Game.BaseModeProperties modeProperties = null;
            try {
                await NewRaceModeData.Instance.WaitUntilReadyAsync();
                modeProperties = new QuickDrive_Custom.ViewModel(modeId, false).GetModePropertiesImpl(new Game.AiCar[0]);
            } catch (Exception e) {
                Logging.Warning($"Failed to prepare mode properties: {e}");
            }

            await GameWrapper.StartAsync(new Game.StartProperties {
                BasicProperties = new Game.BasicProperties {
                    CarId = carId,
                    TrackId = trackId.Split('/')[0],
                    TrackConfigurationId = trackId.Split('/').ElementAtOrDefault(1),
                    CarSkinId = skinId,
                },
                ModeProperties = modeProperties,
                AdditionalPropertieses = new object[] {
                    new NewModeDetails(string.IsNullOrWhiteSpace(paramValue) ? paramValue : $"{modeId}, {paramValue}", null, null, null),
                }.ToList()
            });

            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessRaceOnlineJoin(NameValueCollection p) {
            // Required arguments
            var ip = p.Get(@"ip");
            var httpPort = FlexibleParser.TryParseInt(p.Get(@"httpPort"));
            var password = p.Get(@"plainPassword");
            var encryptedPassword = p.Get(@"password");

            if (string.IsNullOrWhiteSpace(ip)) {
                throw new InformativeException("IP is missing");
            }

            if (!httpPort.HasValue) {
                throw new InformativeException("HTTP port is missing or is in invalid format");
            }

            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(encryptedPassword)) {
                password = OnlineServer.DecryptSharedPassword(ip, httpPort.Value, encryptedPassword);
            }

            await JoinInvitation(ip, httpPort.Value, password);
            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessRaceRaceU(NameValueCollection p) {
            await Task.Delay(0);
            RaceU.NavigateTo();
            return ArgumentHandleResult.SuccessfulShow;
        }

        private static async Task<ArgumentHandleResult> ProcessWorldSimSeries(NameValueCollection p) {
            await Task.Delay(0);
            WorldSimSeries.NavigateTo();
            return ArgumentHandleResult.SuccessfulShow;
        }

        private static async Task<ArgumentHandleResult> ProcessWorldSimSeriesLogin(NameValueCollection p) {
            await Task.Delay(0);
            WorldSimSeries.NavigateTo(p.Get(@"token"));
            return ArgumentHandleResult.SuccessfulShow;
        }
    }
}