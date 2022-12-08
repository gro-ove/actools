using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.ServerPlugins;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        private bool _cmPluginLiveConditions;

        public bool CmPluginLiveConditions {
            get => _cmPluginLiveConditions;
            set => Apply(value, ref _cmPluginLiveConditions, () => {
                if (Loaded) {
                    Changed = true;
                }
            });
        }

        public LiveConditionsServerPlugin.LiveConditionParams CmPluginLiveConditionsParams { get; } = new LiveConditionsServerPlugin.LiveConditionParams();

        private async Task<string> PrepareExternalPluginConfig() {
            var apiKey = "";
            if (CmPluginLiveConditionsParams.UseRealConditions) {
                var knownKeys = ValuesStorage.GetStringList("owmKeys").ToList();
                apiKey = await Prompt.ShowAsync("OpenWeatherMap API key:", "OpenWeatherAPI",
                        comment: "You can get a key on [url=\"https://openweathermap.org/\"]OpenWeatherAPI website[/url]", suggestions: knownKeys);
                if (apiKey == null) return null;
                if (!knownKeys.Contains(apiKey)) {
                    knownKeys.Add(apiKey);
                    ValuesStorage.Storage.SetStringList("owmKeys", knownKeys);
                }
            }

            var track = await TracksManager.Instance.GetLayoutByIdAsync(TrackId, TrackLayoutId) ?? throw new Exception("Track is missing");

            var remoteSplit = PluginUdpAddress.Split(':');
            if (remoteSplit.Length != 2) throw new Exception("Incorrect remote address format");
            if (!(PluginUdpPort > 0)) throw new Exception("Listening plugin port is required");

            var timezone = await RealConditionsHelper.GetTimezoneId(track);
            if (timezone == null) throw new Exception("Failed to determine timezone");

            var geoTags = await LiveConditionsServerPlugin.GetSomeGeoTagsAsync(track);
            if (geoTags == null) throw new Exception("Failed to get geotags");

            var extraPlugins = PluginEntries.Select(x => $"\nplugin.externalPlugin={x.UdpPort}, {x.Address}").JoinToString();
            return $@"# Something to show to remember where config comes from
message=Dynamic conditions for {DisplayName}

# Plugin settings
plugin.listeningPort={remoteSplit[1].Trim()}
plugin.remotePort={PluginUdpPort.Value.ToInvariantString()}
plugin.remoteHostName={remoteSplit[0].Trim()}
plugin.serverCapacity={Capacity.ToInvariantString()}{extraPlugins}

# Weather settings
weather.useV2={(RequiredCspVersion >= 1643 ? "1" : "0")}
weather.apiKey={apiKey}
weather.trackLatitude={geoTags.LatitudeValue?.ToInvariantString() ?? "0"}
weather.trackLongitude={geoTags.LongitudeValue?.ToInvariantString() ?? "0"}
weather.trackLengthKm={(track.SpecsLengthValue / 1e3).ToInvariantString()}
weather.trackTimezoneId={timezone}
weather.useRealConditions={(CmPluginLiveConditionsParams.UseRealConditions ? "1" : "0")}
weather.timeOffset={CmPluginLiveConditionsParams.TimeOffset.ToInvariantString()}
weather.useFixedStartingTime={(CmPluginLiveConditionsParams.UseFixedStartingTime ? "1" : "0")}
weather.fixedStartingTimeValue={CmPluginLiveConditionsParams.FixedStartingTimeValue.ToInvariantString()}
weather.fixedStartingDateValue={CmPluginLiveConditionsParams.FixedStartingDateValue.Date.ToInvariantString()}
weather.timeMultiplier={CmPluginLiveConditionsParams.TimeMultiplier.ToInvariantString()}
weather.temperatureOffset={CmPluginLiveConditionsParams.TemperatureOffset.ToInvariantString()}
weather.useFixedAirTemperature={(CmPluginLiveConditionsParams.UseFixedAirTemperature ? "1" : "0")}
weather.fixedAirTemperature={CmPluginLiveConditionsParams.FixedAirTemperature.ToInvariantString()}
weather.weatherTypeChangePeriod={CmPluginLiveConditionsParams.WeatherTypeChangePeriod.ToInvariantString()}
weather.weatherTypeChangeToNeighboursOnly={(CmPluginLiveConditionsParams.WeatherTypeChangeToNeighboursOnly ? "1" : "0")}
weather.weatherRainChance={CmPluginLiveConditionsParams.WeatherRainChance.ToInvariantString()}
weather.weatherThunderChance={CmPluginLiveConditionsParams.WeatherThunderChance.ToInvariantString()}
weather.trackGripStartingValue={CmPluginLiveConditionsParams.TrackGripStartingValue.ToInvariantString()}
weather.trackGripIncreasePerLap={CmPluginLiveConditionsParams.TrackGripIncreasePerLap.ToInvariantString()}
weather.trackGripTransfer={CmPluginLiveConditionsParams.TrackGripTransfer.ToInvariantString()}
weather.rainTimeMultiplier={CmPluginLiveConditionsParams.RainTimeMultiplier.ToInvariantString()}
weather.rainWetnessDecreaseTime={CmPluginLiveConditionsParams.RainWetnessDecreaseTime.ToInvariantString()}
weather.rainWetnessIncreaseTime={CmPluginLiveConditionsParams.RainWetnessIncreaseTime.ToInvariantString()}
weather.rainWaterDecreaseTime={CmPluginLiveConditionsParams.RainWaterDecreaseTime.ToInvariantString()}
weather.rainWaterIncreaseTime={CmPluginLiveConditionsParams.RainWaterIncreaseTime.ToInvariantString()}";
        }

        private AsyncCommand _SaveExternalPluginConfigCommand;

        public AsyncCommand SaveExternalPluginConfigCommand => _SaveExternalPluginConfigCommand ?? (_SaveExternalPluginConfigCommand = new AsyncCommand(
                async () => {
                    try {
                        var data = await PrepareExternalPluginConfig();
                        if (data == null) return;

                        var filename = FileRelatedDialogs.Save(new SaveDialogParams {
                            Filters = { DialogFilterPiece.ConfigFiles },
                            DetaultExtension = ".cfg",
                            DirectorySaveKey = "externalServerPlugin",
                            DefaultFileName = "config.cfg"
                        });
                        if (filename != null) {
                            File.WriteAllText(filename, data);
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t prepare config", e);
                    }
                }));

        private AsyncCommand _SaveExternalPluginCommand;

        public AsyncCommand SaveExternalPluginCommand => _SaveExternalPluginCommand ?? (_SaveExternalPluginCommand = new AsyncCommand(async () => {
            try {
                var data = await PrepareExternalPluginConfig();
                if (data == null) return;

                var filename = FileRelatedDialogs.Save(new SaveDialogParams {
                    Filters = { DialogFilterPiece.Applications },
                    DetaultExtension = ".exe",
                    DirectorySaveKey = "externalServerPlugin",
                    DefaultFileName = $"Dynamic conditions for {FileUtils.EnsureFileNameIsValid(Name, true)}.exe"
                });
                if (filename == null) return;

                var packedPlugin = await CmApiProvider.GetStaticDataBytesAsync("server_dynamic_conditions", TimeSpan.FromDays(3));
                if (packedPlugin == null) return;

                await Task.Run(() => {
                    using (var stream = new MemoryStream(packedPlugin, false))
                    using (var archive = new ZipArchive(stream)) {
                        var exe = archive.GetEntry("AcTools.ServerPlugin.DynamicConditions.exe")?.Open().ReadAsBytesAndDispose();
                        if (exe == null) throw new Exception("Data is damaged");

                        using (var writer = new ExtendedBinaryWriter(filename)) {
                            writer.Write(exe);
                            writer.Write(data);
                            writer.Write(0xBEE5);
                            writer.Write(data.Length);
                        }
                    }
                });
            } catch (Exception e) {
                NonfatalError.Notify("Can’t prepare config", e);
            }
        }));
    }
}