using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using ShowCommand = FirstFloor.ModernUI.Commands.AsyncCommand
        <System.Tuple<System.IProgress<double?>, System.Threading.CancellationToken>>;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsVideo {
        private class SettingNameAttribute : Attribute {
            public string Name { get; }

            public SettingNameAttribute(string name) {
                Name = name;
            }
        }

        private class DataIdAttribute : Attribute {
            public string Id { get; }

            public DataIdAttribute(string id) {
                Id = id;
            }
        }

        private enum Setting {
            [SettingName("MSAA"),
             DataId(@"video_aa")]
            Msaa,

            [SettingName("FXAA"),
             DataId(@"video_aa")]
            Fxaa,

            [SettingName("Anisotropic filtering"),
             DataId(@"video_anisotropic_filtration")]
            AnisotropicFiltering,

            [SettingName("World details"),
             DataId(@"video_world_details")]
            WorldDetails,

            [SettingName("Shadows resolution"),
             DataId(@"video_shadows")]
            ShadowResolution,

            [SettingName("Post-processing"),
             DataId(@"video_pp")]
            PostProcessing,

            [SettingName("Post-processing quality"),
             DataId(@"video_pp_overall_quality")]
            PostProcessingQuality,

            [SettingName("Depth of field"),
             DataId(@"video_dof")]
            DepthOfField,

            [SettingName("Glare"),
             DataId(@"video_glare")]
            Glare,

            [SettingName("Sunrays"),
             DataId(@"video_sunrays")]
            Sunrays,

            [SettingName("Heat shimmering"),
             DataId(@"video_heat")]
            HeatShimmering,

            [SettingName("Motion blur"),
             DataId(@"video_motion_blur")]
            MotionBlur,

            [SettingName("Mirrors resolution"),
             DataId(@"video_mirrors")]
            MirrorsResolution,

            [SettingName("High-quality mirrors"),
             DataId(@"video_mirrors")]
            MirrorsHighQuality,

            [SettingName("Mip LOD bias"),
             DataId(@"video_mip_lod_bias")]
            MipLodBias,

            [SettingName("Skybox reflection"),
             DataId(@"video_skybox_reflection")]
            SkyboxReflectionGain,

            [SettingName("Reflections resolution"),
             DataId(@"video_reflections_resolution")]
            ReflectionsResolution,

            [SettingName("Reflections distance"),
             DataId(@"video_reflections_distance")]
            ReflectionsDistance,
        }

        private sealed class SettingValue : Displayable {
            public Lazier<byte[]> Data { get; }
            public string Value { get; }

            public bool IsActive { get; set; }
            public double PerformanceHitPercentage { get; set; }
            public string CustomDescription { get; set; }

            public SettingValue(string name, string value, ZipArchiveEntry entry, double performanceHitMs, bool isActive) {
                DisplayName = name.ToSentenceMember();
                Data = Lazier.Create(() => entry.Open().ReadAsBytesAndDispose());
                Value = value;
                PerformanceHitPercentage = performanceHitMs / 12 * 100;
                IsActive = isActive;
            }
        }

        public partial class ViewModel {
            private ShowCommand _descriptionMsaaCommand;

            public ShowCommand DescriptionMsaaCommand => _descriptionMsaaCommand ?? (_descriptionMsaaCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.Msaa, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionFxaaCommand;

            public ShowCommand DescriptionFxaaCommand => _descriptionFxaaCommand ?? (_descriptionFxaaCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.Fxaa, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionAnisotropicCommand;

            public ShowCommand DescriptionAnisotropicCommand => _descriptionAnisotropicCommand ?? (_descriptionAnisotropicCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.AnisotropicFiltering, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionWorldCommand;

            public ShowCommand DescriptionWorldCommand => _descriptionWorldCommand ?? (_descriptionWorldCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.WorldDetails, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionShadowCommand;

            public ShowCommand DescriptionShadowCommand => _descriptionShadowCommand ?? (_descriptionShadowCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.ShadowResolution, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionPpCommand;

            public ShowCommand DescriptionPpCommand => _descriptionPpCommand ?? (_descriptionPpCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.PostProcessing, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionPpQualityCommand;

            public ShowCommand DescriptionPpQualityCommand => _descriptionPpQualityCommand ?? (_descriptionPpQualityCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.PostProcessingQuality, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionGlareCommand;

            public ShowCommand DescriptionGlareCommand => _descriptionGlareCommand ?? (_descriptionGlareCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.Glare, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionDofCommand;

            public ShowCommand DescriptionDofCommand => _descriptionDofCommand ?? (_descriptionDofCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.DepthOfField, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionMotionBlurCommand;

            public ShowCommand DescriptionMotionBlurCommand => _descriptionMotionBlurCommand ?? (_descriptionMotionBlurCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.MotionBlur, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionSunraysCommand;

            public ShowCommand DescriptionSunraysCommand => _descriptionSunraysCommand ?? (_descriptionSunraysCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.Sunrays, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionHeatCommand;

            public ShowCommand DescriptionHeatCommand => _descriptionHeatCommand ?? (_descriptionHeatCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.HeatShimmering, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionMirrorsHqCommand;

            public ShowCommand DescriptionMirrorsHqCommand => _descriptionMirrorsHqCommand ?? (_descriptionMirrorsHqCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.MirrorsHighQuality, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionMirrorsResolutionCommand;

            public ShowCommand DescriptionMirrorsResolutionCommand => _descriptionMirrorsResolutionCommand ?? (_descriptionMirrorsResolutionCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.MirrorsResolution, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionMipLodBiasCommand;

            public ShowCommand DescriptionMipLodBiasCommand => _descriptionMipLodBiasCommand ?? (_descriptionMipLodBiasCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.MipLodBias, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionSkyboxReflectionCommand;

            public ShowCommand DescriptionSkyboxReflectionCommand => _descriptionSkyboxReflectionCommand ?? (_descriptionSkyboxReflectionCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.SkyboxReflectionGain, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionCubemapResolutionCommand;

            public ShowCommand DescriptionCubemapResolutionCommand => _descriptionCubemapResolutionCommand ?? (_descriptionCubemapResolutionCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.ReflectionsResolution, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private ShowCommand _descriptionCubemapDistanceCommand;

            public ShowCommand DescriptionCubemapDistanceCommand => _descriptionCubemapDistanceCommand ?? (_descriptionCubemapDistanceCommand =
                    new ShowCommand(t => ShowDescriptionAsync(Setting.ReflectionsDistance, t?.Item1, t?.Item2 ?? default(CancellationToken))));

            private static bool HasSmaaEnabled() {
                var root = AcRootDirectory.Instance.Value;
                if (root == null) return false;

                try {
                    if (File.Exists(Path.Combine(root, "dxgi.dll"))) {
                        var config = new IniFile(Path.Combine(root, "dxgi.ini"));
                        var preset = config["GENERAL"].GetStrings("PresetFiles").ArrayElementAtOrDefault(config["GENERAL"].GetInt("CurrentPreset", -1));
                        if (preset != null) {
                            var fullPath = FileUtils.GetFullPath(preset, root);
                            if (File.Exists(fullPath)) {
                                return File.ReadAllLines(fullPath).FirstOrDefault(x => x.StartsWith(@"Effects="))?.Contains(@"SMAA.fx") == true;
                            }
                        }
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }

                return false;
            }

            private IReadOnlyList<SettingValue> GetValues(Setting setting, ZipArchive archive, out string description, out Action<string> selectCallback) {
                switch (setting) {
                    case Setting.AnisotropicFiltering:
                        description = DefaultDescription();
                        selectCallback = x => Video.AnisotropicLevel = Video.AnisotropicLevels.GetById(x);
                        return Entries(Video.AnisotropicLevels, Video.AnisotropicLevel);
                    case Setting.WorldDetails:
                        description = DefaultDescription();
                        selectCallback = x => Video.WorldDetails = Video.WorldDetailsLevels.GetById(x);
                        return Entries(Video.WorldDetailsLevels, Video.WorldDetails);
                    case Setting.ShadowResolution:
                        description = DefaultDescription();
                        selectCallback = x => Video.ShadowMapSize = Video.ShadowMapSizes.GetById(x);
                        return Entries(Video.ShadowMapSizes, Video.ShadowMapSize);
                    case Setting.Msaa:
                        description = DefaultDescription(@"msaa");
                        selectCallback = x => Video.AntiAliasingLevel = Video.AntiAliasingLevels.GetById(x);
                        return Entries(Video.AntiAliasingLevels, Video.AntiAliasingLevel,
                                Video.PostProcessing && Video.Fxaa ? @"fxaa" : HasSmaaEnabled() ? @"smaa" : @"base");
                    case Setting.Fxaa:
                        var fxaaDescription = DefaultDescription(@"fxaa");
                        description = fxaaDescription.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)[0];

                        selectCallback = x => {
                            if (x.As(false)) {
                                Video.Fxaa = true;
                                Video.PostProcessing = true;
                            } else {
                                Video.Fxaa = false;
                            }
                        };

                        var msaaPrefix = $@"{Video.AntiAliasingLevel.Value}__";
                        return Result(archive.Entries.Where(x => x.FullName.StartsWith(msaaPrefix)).Take(3).Select(x => {
                            var isSmaa = x.FullName.EndsWith(@"__smaa.jpg");
                            var isEnabled = x.FullName.EndsWith(@"__fxaa.jpg");
                            return new SettingValue(isSmaa ? "off, with SMAA instead" : isEnabled ? "on" : "off", isEnabled.As<string>(), x,
                                    GetPerformanceHit(x),
                                    isEnabled == (Video.PostProcessing && Video.Fxaa)) {
                                        CustomDescription = isSmaa ? fxaaDescription : null
                                    };
                        }).OrderBy(x => x.DisplayName.Contains(@"SMAA") ? 2 : x.IsActive ? 1 : 0));
                    case Setting.PostProcessing:
                        description = DefaultDescription();
                        selectCallback = x => Video.PostProcessing = x.As(false);
                        return Flag(Video.PostProcessing);
                    case Setting.PostProcessingQuality:
                        description = DefaultDescription();
                        selectCallback = x => Video.PostProcessingQuality = Video.PostProcessingQualities.GetById(x);
                        return Entries(Video.PostProcessingQualities, Video.PostProcessingQuality);
                    case Setting.DepthOfField:
                        description = DefaultDescription();
                        selectCallback = x => Video.DepthOfFieldQuality = Video.DepthOfFieldQualities.GetById(x);
                        return Entries(Video.DepthOfFieldQualities, Video.DepthOfFieldQuality);
                    case Setting.Glare:
                        description = DefaultDescription();
                        selectCallback = x => Video.GlareQuality = Video.GlareQualities.GetById(x);
                        return Entries(Video.GlareQualities, Video.GlareQuality);
                    case Setting.Sunrays:
                        description = DefaultDescription();
                        selectCallback = x => Video.Sunrays = x.As(false);
                        return Flag(Video.Sunrays);
                    case Setting.HeatShimmering:
                        description = DefaultDescription();
                        selectCallback = x => Video.HeatShimmering = x.As(false);
                        return Flag(Video.HeatShimmering);
                    case Setting.MotionBlur:
                        description = DefaultDescription();
                        selectCallback = x => Video.MotionBlur = x.As(0);
                        return Number(Video.MotionBlur, v => $@"{v}x");
                    case Setting.MirrorsHighQuality:
                        description = DefaultDescription(@"hq");
                        selectCallback = x => Video.MirrorsHighQuality = x.As(false);
                        var resolutionPrefix = $@"{Video.MirrorsResolution.Value}__";
                        return Result(archive.Entries.Where(x => x.FullName.StartsWith(resolutionPrefix)).Take(2).Select(x => {
                            var isEnabled = x.FullName.EndsWith(@"__hq.jpg");
                            return new SettingValue(isEnabled ? "on" : "off", isEnabled.As<string>(), x,
                                    GetPerformanceHit(x),
                                    isEnabled == (Video.PostProcessing && Video.Fxaa));
                        }).OrderBy(x => x.IsActive ? 1 : 0));
                    case Setting.MirrorsResolution:
                        description = DefaultDescription(@"resolution");
                        selectCallback = x => Video.MirrorsResolution = Video.MirrorsResolutions.GetById(x);
                        return Entries(Video.MirrorsResolutions, Video.MirrorsResolution, Video.MirrorsHighQuality ? @"hq" : @"lq", @"0");
                    case Setting.SkyboxReflectionGain:
                        description = DefaultDescription();
                        selectCallback = x => Graphics.SkyboxReflectionGain = x.As(0);
                        return Number(Graphics.SkyboxReflectionGain, v => $@"{v}%");
                    case Setting.MipLodBias:
                        description = DefaultDescription();
                        selectCallback = x => Graphics.MipLodBias = x.As(0);
                        return Number(Graphics.MipLodBias, v => $@"{v:F0}", Video.AnisotropicLevel.Value == "0" ? @"noaf" : @"af");
                    case Setting.ReflectionsResolution:
                        description = DefaultDescription();
                        selectCallback = x => Video.CubemapResolution = Video.CubemapResolutions.GetById(x);
                        return Adjust(Entries(Video.CubemapResolutions, Video.CubemapResolution),
                                x => x.PerformanceHitPercentage *= (Video.CubemapRenderingFrequency.Value.As(0) + 1) / 7d);
                    case Setting.ReflectionsDistance:
                        description = DefaultDescription();
                        selectCallback = x => Video.CubemapDistance = x.As(0);
                        return Adjust(Number(Video.CubemapDistance, v => $"{v} m"),
                                x => x.PerformanceHitPercentage *= Video.CubemapRenderingFrequency.Value.As(0) / 6d);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(setting), setting, null);
                }

                string DefaultDescription(string type = null) {
                    var bytes = archive.GetEntry($@"description{(type == null ? null : $@"_{type}")}.txt")?.Open().ReadAsBytesAndDispose();
                    return bytes == null ? null : Encoding.UTF8.GetString(bytes);
                }

                IReadOnlyList<SettingValue> Flag(bool currentSetting) {
                    return Result(archive.Entries.Where(x => x.FullName.EndsWith(@".jpg")).Take(2).Select(x => {
                        var isEnabled = x.FullName.StartsWith(@"1");
                        return new SettingValue(isEnabled ? "on" : "off", isEnabled.As<string>(), x, GetPerformanceHit(x), isEnabled == currentSetting);
                    }).OrderBy(x => x.IsActive ? 1 : 0));
                }

                IReadOnlyList<SettingValue> Number(double currentSetting, Func<double, string> displayValueCallback, string filter = null) {
                    var closestValue = double.MaxValue;
                    return Result(archive.Entries.Where(x => x.FullName.EndsWith(@".jpg") && EntriesFilter(x, filter, null)).Select(x => {
                        var value = x.FullName.Split(new[] { @"__" }, StringSplitOptions.RemoveEmptyEntries)[0].As(0d);
                        var difference = (value - currentSetting).Abs();
                        if (difference < closestValue) {
                            closestValue = difference;
                        }
                        return new SettingValue(displayValueCallback(value), value.As<string>(), x, GetPerformanceHit(x),
                                closestValue == difference);
                    }).ToList().Select(x => {
                        x.IsActive = (x.Value.As(0d) - currentSetting).Abs() == closestValue;
                        return x;
                    }).OrderBy(x => x.Value.As(0d)));
                }

                IReadOnlyList<SettingValue> Entries(SettingEntry[] values, SettingEntry currentSetting, string filter = null, string appendExtra = null) {
                    return Result(values.Select(x => new {
                        Value = x,
                        Entry = archive.Entries.FirstOrDefault(y => y.FullName.StartsWith(x.Value + @"__") && y.FullName.EndsWith(@".jpg")
                                && EntriesFilter(y, filter, appendExtra)),
                    }).Where(x => x.Entry != null).Select(x =>
                            new SettingValue(x.Value.DisplayName, x.Value.Value, x.Entry, GetPerformanceHit(x.Entry), x.Value == currentSetting)));
                }

                bool EntriesFilter(ZipArchiveEntry entry, string filter, string appendExtra) {
                    return filter == null || entry.FullName.Contains($@"__{filter}.jpg")
                            || appendExtra != null && entry.FullName.StartsWith($@"{appendExtra}__");
                }

                IReadOnlyList<SettingValue> Adjust(IReadOnlyList<SettingValue> items, Action<SettingValue> callback) {
                    foreach (var item in items) {
                        callback(item);
                    }
                    return items;
                }

                IReadOnlyList<SettingValue> Result(IEnumerable<SettingValue> items) {
                    var result = items.ToList();
                    if (result.Count == 0) {
                        throw new Exception("Content not found");
                    }

                    var minHit = result.Min(x => x.PerformanceHitPercentage);
                    result.ForEach(x => x.PerformanceHitPercentage -= minHit);
                    return result;
                }

                double GetPerformanceHit(ZipArchiveEntry entry) {
                    var piece = entry.FullName.Split(new[] { @"__", @".jpg" }, StringSplitOptions.RemoveEmptyEntries)[1];
                    return piece.As(0d);
                }
            }

            private static Color GetColor(double performanceHitPercentage) {
                if (performanceHitPercentage == 0d) return Colors.White;
                if (performanceHitPercentage < 2d) return Colors.LimeGreen;
                if (performanceHitPercentage < 5d) return Colors.Yellow;
                if (performanceHitPercentage < 10d) return Colors.Orange;
                if (performanceHitPercentage < 15d) return Colors.Red;
                return Colors.Brown;
            }

            private async Task ShowDescriptionAsync(Setting setting, [CanBeNull] IProgress<double?> progress,
                    CancellationToken cancellationToken) {
                try {
                    var id = setting.GetAttribute<DataIdAttribute>()?.Id;
                    if (id == null) {
                        throw new Exception("ID is missing");
                    }

                    var data = (await CmApiProvider.GetStaticDataAsync(id, TimeSpan.MaxValue,
                            progress == null ? null : new Progress<AsyncProgressEntry>(v => progress.Report(v.Progress)), cancellationToken))?.Item1;
                    if (data == null) {
                        throw new Exception("Failed to load data");
                    }

                    using (var archive = ZipFile.OpenRead(data)) {
                        var values = GetValues(setting, archive, out var description, out var selectCallback);
                        var alignment = GetAlignment(setting);
                        var dialog = new ImageViewer<SettingValue>(values, values.FindIndex(x => x.IsActive),
                                e => Task.FromResult((object)e.Data.Value), e => {
                                    var ui = (Panel)_uiParent.FindResource(@"SettingDescription");
                                    ui.RequireChild<TextBlock>("SettingName").Text = setting.GetAttribute<SettingNameAttribute>()?.Name;
                                    ui.RequireChild<BbCodeBlock>("Description").Text = e.CustomDescription ?? description;
                                    ui.RequireChild<TextBlock>("SettingValue").Text = e.DisplayName;
                                    if (values.All(x => x.PerformanceHitPercentage == 0d)) {
                                        ui.RequireChild<FrameworkElement>("PerformanceHitPanel").Visibility = Visibility.Collapsed;
                                    } else {
                                        var phBlock = ui.RequireChild<TextBlock>("PerformanceHit");
                                        phBlock.Text = e.PerformanceHitPercentage == 0d ? "none" : $@"≈{e.PerformanceHitPercentage:F2}%";
                                        phBlock.Foreground = new SolidColorBrush(GetColor(e.PerformanceHitPercentage));
                                    }
                                    return ui;
                                }) {
                                    MaxAreaWidth = 1920,
                                    MaxAreaHeight = 1080,
                                    MaxImageWidth = 1920,
                                    MaxImageHeight = 1080,
                                    HorizontalDetailsAlignment = alignment.Item1,
                                    VerticalDetailsAlignment = alignment.Item2,
                                    Model = { IsLooped = true }
                                };

                        if (selectCallback == null) {
                            dialog.ShowDialog();
                        } else {
                            var result = dialog.SelectDialog();
                            if (result != null) {
                                selectCallback.Invoke(result.Value);
                            }
                        }
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t show description for that option", "At first launch, some images might have to be downloaded.", e);
                }
            }

            private static Tuple<HorizontalAlignment, VerticalAlignment> GetAlignment(Setting setting) {
                switch (setting) {
                    case Setting.ShadowResolution:
                    case Setting.PostProcessing:
                        return Tuple.Create(HorizontalAlignment.Left, VerticalAlignment.Top);
                    case Setting.DepthOfField:
                    case Setting.MotionBlur:
                    case Setting.PostProcessingQuality:
                    case Setting.SkyboxReflectionGain:
                    case Setting.ReflectionsResolution:
                    case Setting.ReflectionsDistance:
                        return Tuple.Create(HorizontalAlignment.Center, VerticalAlignment.Top);
                    case Setting.WorldDetails:
                    case Setting.MirrorsHighQuality:
                    case Setting.MirrorsResolution:
                    case Setting.Sunrays:
                        return Tuple.Create(HorizontalAlignment.Right, VerticalAlignment.Bottom);
                    case Setting.Glare:
                    case Setting.MipLodBias:
                        return Tuple.Create(HorizontalAlignment.Center, VerticalAlignment.Bottom);
                    case Setting.Msaa:
                    case Setting.Fxaa:
                    case Setting.AnisotropicFiltering:
                    case Setting.HeatShimmering:
                        return Tuple.Create(HorizontalAlignment.Left, VerticalAlignment.Bottom);
                    default:
                        Logging.Debug($"Unknown: {setting}");
                        break;
                }

                return Tuple.Create(HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }
}