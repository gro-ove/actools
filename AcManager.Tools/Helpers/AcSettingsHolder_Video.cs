using System;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class VideoSettings : IniSettings {
            public sealed class ResolutionEntry : Displayable, IEquatable<ResolutionEntry> {
                public int Width { get; }

                public int Height { get; }

                public int Framerate { get; }

                internal ResolutionEntry(int width, int height, int framerate) {
                    Width = width;
                    Height = height;
                    Framerate = framerate;

                    DisplayName = $"{Width}×{Height}, {Framerate} Hz";
                }

                public bool Equals(ResolutionEntry other) {
                    return other != null && Width == other.Width && Height == other.Height && Framerate == other.Framerate;
                }

                public override bool Equals(object obj) {
                    return obj is ResolutionEntry && Equals((ResolutionEntry)obj);
                }

                public override int GetHashCode() {
                    unchecked {
                        var hashCode = Width;
                        hashCode = (hashCode * 397) ^ Height;
                        hashCode = (hashCode * 397) ^ Framerate;
                        return hashCode;
                    }
                }
            }

            internal VideoSettings() : base("video") {}

            #region Entries lists
            public SettingEntry[] CameraModes { get; } = {
                new SettingEntry("DEFAULT", "Single Screen"),
                new SettingEntry("TRIPLE", "Triple Screen"),
                new SettingEntry("OCULUS", "Oculus Rift")
            };

            public SettingEntry[] AnisotropicLevels { get; } = {
                new SettingEntry("0", "Off"),
                new SettingEntry("2", "2x"),
                new SettingEntry("4", "4x"),
                new SettingEntry("8", "8x"),
                new SettingEntry("16", "16x"),
            };

            public SettingEntry[] AntiAliasingLevels { get; } = {
                new SettingEntry("1", "Off"),
                new SettingEntry("2", "2x"),
                new SettingEntry("4", "4x"),
                new SettingEntry("8", "8x (experimental)")
            };

            public SettingEntry[] ShadowMapSizes { get; } = {
                new SettingEntry("512", "512×512"),
                new SettingEntry("1024", "1024×1024"),
                new SettingEntry("2048", "2048×2048"),
                new SettingEntry("4096", "4096×4096"),
                //new SettingEntry("8196", "8196×8196 (for crazy lads)"),
            };

            public SettingEntry[] WorldDetailLevels { get; } = {
                new SettingEntry("0", "Minimum"),
                new SettingEntry("1", "Low"),
                new SettingEntry("2", "Medium"),
                new SettingEntry("3", "High"),
                new SettingEntry("4", "Very High"),
                new SettingEntry("5", "Maximum")
            };

            public SettingEntry[] SmokeLevels { get; } = {
                new SettingEntry("0", "Off"),
                new SettingEntry("1", "Minimum"),
                new SettingEntry("2", "Low"),
                new SettingEntry("3", "Medium"),
                new SettingEntry("4", "High"),
                new SettingEntry("5", "Maximum")
            };

            public SettingEntry[] PostProcessingQualities => WorldDetailLevels;

            public SettingEntry[] GlareQualities { get; } = {
                new SettingEntry("0", "Off"),
                new SettingEntry("1", "Low"),
                new SettingEntry("2", "Medium"),
                new SettingEntry("3", "High"),
                new SettingEntry("4", "Very High"),
                new SettingEntry("5", "Maximum")
            };

            public SettingEntry[] DepthOfFieldQualities => GlareQualities;

            public SettingEntry[] MirrorResolutions { get; } = {
                new SettingEntry("0", "Off"),
                new SettingEntry("256", "64×256"),
                new SettingEntry("512", "128×512"),
                new SettingEntry("1024", "256×1024")
            };

            public SettingEntry[] CubemapResolutions { get; } = {
                new SettingEntry("0", "Off"),
                new SettingEntry("256", "256×256"),
                new SettingEntry("512", "512×512"),
                new SettingEntry("1024", "1024×1024"),
                new SettingEntry("2048", "2048×2048")
            };

            public SettingEntry[] CubemapRenderingFrequencies { get; } = {
                new SettingEntry("0", "Static"),
                new SettingEntry("1", "One face per frame"),
                new SettingEntry("2", "Two faces per frame"),
                new SettingEntry("3", "Three faces per frame"),
                new SettingEntry("4", "Four faces per frame"),
                new SettingEntry("5", "Five faces per frame"),
                new SettingEntry("6", "Six faces per frame"),
            };
            #endregion

            #region Main
            private ResolutionEntry[] _resolutions;

            public ResolutionEntry[] Resolutions {
                get { return _resolutions; }
                set {
                    if (ReferenceEquals(value, _resolutions)) return;
                    _resolutions = value;
                    OnPropertyChanged(false);
                }
            }

            private void UpdateResolutionsList() {
                var r = Resolution;
                var d = new User32.DEVMODE();
                var l = LinqExtension.RangeFrom()
                                           .Select(i => User32.EnumDisplaySettings(null, i, ref d)
                                                   ? new ResolutionEntry(d.dmPelsWidth, d.dmPelsHeight, d.dmDisplayFrequency) : null)
                                           .TakeWhile(x => x != null).ToArray();
                // if (Resolutions?.JoinToString(",") == l.JoinToString(",")) return;
                Resolutions = l;
                Resolution = Resolutions.FirstOrDefault(x => x.Equals(r));
            }

            private ResolutionEntry _resolution;

            public ResolutionEntry Resolution {
                get { return _resolution; }
                set {
                    if (!Resolutions.Contains(value)) value = Resolutions.MaxEntry(x => x.Width);
                    if (ReferenceEquals(value, _resolution)) return;
                    _resolution = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _cameraMode;

            public SettingEntry CameraMode {
                get { return _cameraMode; }
                set {
                    if (!CameraModes.Contains(value)) value = CameraModes[0];
                    if (Equals(value, _cameraMode)) return;
                    _cameraMode = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _anisotropicLevel;

            public SettingEntry AnisotropicLevel {
                get { return _anisotropicLevel; }
                set {
                    if (!AnisotropicLevels.Contains(value)) value = AnisotropicLevels[0];
                    if (Equals(value, _anisotropicLevel)) return;
                    _anisotropicLevel = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _antiAliasingLevel;

            public SettingEntry AntiAliasingLevel {
                get { return _antiAliasingLevel; }
                set {
                    if (!AntiAliasingLevels.Contains(value)) value = AntiAliasingLevels[0];
                    if (Equals(value, _antiAliasingLevel)) return;
                    _antiAliasingLevel = value;
                    OnPropertyChanged();
                }
            }

            private bool _fullscreen;

            public bool Fullscreen {
                get { return _fullscreen; }
                set {
                    if (Equals(value, _fullscreen)) return;
                    _fullscreen = value;
                    OnPropertyChanged();
                }
            }

            private bool _verticalSyncronization;

            public bool VerticalSyncronization {
                get { return _verticalSyncronization; }
                set {
                    if (Equals(value, _verticalSyncronization)) return;
                    _verticalSyncronization = value;
                    OnPropertyChanged();
                }
            }

            private bool _framerateLimitEnabled;

            public bool FramerateLimitEnabled {
                get { return _framerateLimitEnabled; }
                set {
                    if (Equals(value, _framerateLimitEnabled)) return;
                    _framerateLimitEnabled = value;
                    OnPropertyChanged();
                }
            }

            private int _framerateLimit;

            public int FramerateLimit {
                get { return _framerateLimit; }
                set {
                    value = value.Clamp(30, 240);
                    if (Equals(value, _framerateLimit)) return;
                    _framerateLimit = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Quality
            private SettingEntry _shadowMapSize;

            public SettingEntry ShadowMapSize {
                get { return _shadowMapSize; }
                set {
                    if (!ShadowMapSizes.Contains(value)) value = ShadowMapSizes[0];
                    if (Equals(value, _shadowMapSize)) return;
                    _shadowMapSize = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _worldDetail;

            public SettingEntry WorldDetail {
                get { return _worldDetail; }
                set {
                    if (!WorldDetailLevels.Contains(value)) value = WorldDetailLevels[0];
                    if (Equals(value, _worldDetail)) return;
                    _worldDetail = value;
                    OnPropertyChanged();
                }
            }

            private bool _smokeInMirrors;

            public bool SmokeInMirrors {
                get { return _smokeInMirrors; }
                set {
                    if (Equals(value, _smokeInMirrors)) return;
                    _smokeInMirrors = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _smokeLevel;

            public SettingEntry SmokeLevel {
                get { return _smokeLevel; }
                set {
                    if (!SmokeLevels.Contains(value)) value = SmokeLevels[0];
                    if (Equals(value, _smokeLevel)) return;
                    _smokeLevel = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Post-processing
            private bool _postProcessing;

            public bool PostProcessing {
                get { return _postProcessing; }
                set {
                    if (Equals(value, _postProcessing)) return;
                    _postProcessing = value;
                    OnPropertyChanged();
                }
            }

            private string _postProcessingFilter;
            
            [CanBeNull]
            public string PostProcessingFilter {
                get { return _postProcessingFilter; }
                set {
                    if (Equals(value, _postProcessingFilter)) return;
                    _postProcessingFilter = value;
                    _postProcessingFilterObjectSet = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PostProcessingFilterObject));
                }
            }

            private bool _postProcessingFilterObjectSet;
            private PpFilterObject _postProcessingFilterObject;

            [CanBeNull]
            public PpFilterObject PostProcessingFilterObject {
                get {
                    if (_postProcessingFilterObjectSet) return _postProcessingFilterObject;

                    _postProcessingFilterObjectSet = true;
                    _postProcessingFilterObject = PostProcessingFilter == null ? null : PpFiltersManager.Instance.GetByAcId(PostProcessingFilter);
                    return _postProcessingFilterObject;
                }
                set {
                    _postProcessingFilterObjectSet = true;
                    _postProcessingFilterObject = value;
                    _postProcessingFilter = value?.AcId;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PostProcessingFilterObject));
                }
            }

            private SettingEntry _postProcessingQuality;

            public SettingEntry PostProcessingQuality {
                get { return _postProcessingQuality; }
                set {
                    if (!PostProcessingQualities.Contains(value)) value = PostProcessingQualities[0];
                    if (Equals(value, _postProcessingQuality)) return;
                    _postProcessingQuality = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _glareQuality;

            public SettingEntry GlareQuality {
                get { return _glareQuality; }
                set {
                    if (!GlareQualities.Contains(value)) value = GlareQualities[0];
                    if (Equals(value, _glareQuality)) return;
                    _glareQuality = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _depthOfFieldQuality;

            public SettingEntry DepthOfFieldQuality {
                get { return _depthOfFieldQuality; }
                set {
                    if (!DepthOfFieldQualities.Contains(value)) value = DepthOfFieldQualities[0];
                    if (Equals(value, _depthOfFieldQuality)) return;
                    _depthOfFieldQuality = value;
                    OnPropertyChanged();
                }
            }

            private bool _raysOfGod;

            public bool RaysOfGod {
                get { return _raysOfGod; }
                set {
                    if (Equals(value, _raysOfGod)) return;
                    _raysOfGod = value;
                    OnPropertyChanged();
                }
            }

            private bool _heatShimmering;

            public bool HeatShimmering {
                get { return _heatShimmering; }
                set {
                    if (Equals(value, _heatShimmering)) return;
                    _heatShimmering = value;
                    OnPropertyChanged();
                }
            }

            private int _colorSaturation;

            public int ColorSaturation {
                get { return _colorSaturation; }
                set {
                    value = value.Clamp(0, 400);
                    if (Equals(value, _colorSaturation)) return;
                    _colorSaturation = value;
                    OnPropertyChanged();
                }
            }

            private bool _fxaa;

            public bool Fxaa {
                get { return _fxaa; }
                set {
                    if (Equals(value, _fxaa)) return;
                    _fxaa = value;
                    OnPropertyChanged();
                }
            }

            private int _motionBlur;

            public int MotionBlur {
                get { return _motionBlur; }
                set {
                    value = value.Clamp(0, 12);
                    if (Equals(value, _motionBlur)) return;
                    _motionBlur = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Reflections
            private SettingEntry _mirrorResolution;

            public SettingEntry MirrorResolution {
                get { return _mirrorResolution; }
                set {
                    if (!MirrorResolutions.Contains(value)) value = MirrorResolutions[0];
                    if (Equals(value, _mirrorResolution)) return;
                    _mirrorResolution = value;
                    OnPropertyChanged();
                }
            }

            private bool _mirrorHighQuality;

            public bool MirrorHighQuality {
                get { return _mirrorHighQuality; }
                set {
                    if (Equals(value, _mirrorHighQuality)) return;
                    _mirrorHighQuality = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _cubemapResolution;

            public SettingEntry CubemapResolution {
                get { return _cubemapResolution; }
                set {
                    if (!CubemapResolutions.Contains(value)) value = CubemapResolutions[0];
                    if (Equals(value, _cubemapResolution)) return;
                    _cubemapResolution = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _cubemapRenderingFrequency;

            public SettingEntry CubemapRenderingFrequency {
                get { return _cubemapRenderingFrequency; }
                set {
                    if (!CubemapRenderingFrequencies.Contains(value)) value = CubemapRenderingFrequencies[0];
                    if (Equals(value, _cubemapRenderingFrequency)) return;
                    _cubemapRenderingFrequency = value;
                    OnPropertyChanged();
                }
            }

            private int _cubemapDistance;

            public int CubemapDistance {
                get { return _cubemapDistance; }
                set {
                    value = value.Clamp(0, 10000);
                    if (Equals(value, _cubemapDistance)) return;
                    _cubemapDistance = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region View
            private bool _hideSteeringWheel;

            public bool HideSteeringWheel {
                get { return _hideSteeringWheel; }
                set {
                    if (Equals(value, _hideSteeringWheel)) return;
                    _hideSteeringWheel = value;
                    OnPropertyChanged();
                }
            }

            private bool _hideArms;

            public bool HideArms {
                get { return _hideArms; }
                set {
                    if (Equals(value, _hideArms)) return;
                    _hideArms = value;
                    OnPropertyChanged();
                }
            }

            private bool _lockSteeringWheel;

            public bool LockSteeringWheel {
                get { return _lockSteeringWheel; }
                set {
                    if (Equals(value, _lockSteeringWheel)) return;
                    _lockSteeringWheel = value;
                    OnPropertyChanged();
                }
            }

            #endregion

            public void EnsureResolutionIsCorrect() {
                Reload();
            }

            protected override void LoadFromIni() {
                UpdateResolutionsList();

                // VIDEO
                var section = Ini["VIDEO"];
                var loaded = new ResolutionEntry(section.GetInt("WIDTH", 0), section.GetInt("HEIGHT", 0), section.GetInt("REFRESH", 0));
                var resolution = Resolutions.FirstOrDefault(x => x.Equals(loaded));
                if (resolution == null) {
                    Resolution = Resolutions.FirstOrDefault(x => x.Width == loaded.Width && x.Height == loaded.Height);
                    ForceSave();
                    Logging.Warning($"RESOLUTION ({loaded.DisplayName}) IS INVALID, CHANGED TO ({Resolution?.DisplayName})");
                } else {
                    Resolution = resolution;
                }

                Fullscreen = section.GetBool("FULLSCREEN", true);
                VerticalSyncronization = section.GetBool("VSYNC", false);
                AntiAliasingLevel = section.GetEntry("AASAMPLES", AntiAliasingLevels);
                AnisotropicLevel = section.GetEntry("ANISOTROPIC", AnisotropicLevels, "8");
                ShadowMapSize = section.GetEntry("SHADOW_MAP_SIZE", ShadowMapSizes, "2048");

                var limit = section.GetDouble("FPS_CAP_MS", 0);
                FramerateLimitEnabled = Math.Abs(limit) >= 0.1;
                FramerateLimit = FramerateLimitEnabled ? (int)Math.Round(1e3 / limit) : 60;

                CameraMode = Ini["CAMERA"].GetEntry("MODE", CameraModes);

                // ASSETTOCORSA
                section = Ini["ASSETTOCORSA"];
                HideArms = section.GetBool("HIDE_ARMS", false);
                HideSteeringWheel = section.GetBool("HIDE_STEER", false);
                LockSteeringWheel = section.GetBool("LOCK_STEER", false);
                WorldDetail = section.GetEntry("WORLD_DETAIL", WorldDetailLevels, "4");

                MotionBlur = Ini["EFFECTS"].GetInt("MOTION_BLUR", 6);
                SmokeInMirrors = Ini["EFFECTS"].GetBool("RENDER_SMOKE_IN_MIRROR", false);
                SmokeLevel = Ini["EFFECTS"].GetEntry("SMOKE", SmokeLevels, "2");

                // POST_PROCESS
                section = Ini["POST_PROCESS"];
                PostProcessing = section.GetBool("ENABLED", true);
                PostProcessingQuality = section.GetEntry("QUALITY", PostProcessingQualities, "4");
                PostProcessingFilter = section.Get("FILTER", "default");
                GlareQuality = section.GetEntry("GLARE", GlareQualities, "4");
                DepthOfFieldQuality = section.GetEntry("DOF", DepthOfFieldQualities, "4");
                RaysOfGod = section.GetBool("RAYS_OF_GOD", true);
                HeatShimmering = section.GetBool("HEAT_SHIMMER", true);
                Fxaa = section.GetBool("FXAA", true);
                ColorSaturation = Ini["SATURATION"].GetInt("LEVEL", 100);

                MirrorHighQuality = Ini["MIRROR"].GetBool("HQ", false);
                MirrorResolution = Ini["MIRROR"].GetEntry("SIZE", MirrorResolutions, "512");

                section = Ini["CUBEMAP"];
                CubemapResolution = section.GetEntry("SIZE", CubemapResolutions, "1024");
                var orig = CubemapResolution.Value == "0" && section.ContainsKey("__ORIG_FACES_PER_FRAME");
                CubemapRenderingFrequency = section.GetEntry(orig ? "__ORIG_FACES_PER_FRAME" : "FACES_PER_FRAME", CubemapRenderingFrequencies, "3");
                CubemapDistance = section.GetInt(orig ? "__ORIG_FARPLANE" : "FARPLANE", 600);
            }

            protected override void SetToIni() {
                var section = Ini["VIDEO"];
                section.Set("WIDTH", Resolution.Width);
                section.Set("HEIGHT", Resolution.Height);
                section.Set("REFRESH", Resolution.Framerate);

                section.Set("FULLSCREEN", Fullscreen);
                section.Set("VSYNC", VerticalSyncronization);
                section.Set("AASAMPLES", AntiAliasingLevel);
                section.Set("ANISOTROPIC", AnisotropicLevel);
                section.Set("SHADOW_MAP_SIZE", ShadowMapSize);
                section.Set("FPS_CAP_MS", FramerateLimitEnabled ? 1e3 / FramerateLimit : 0d);
                Ini["CAMERA"].Set("MODE", CameraMode);
                
                section = Ini["ASSETTOCORSA"];
                section.Set("HIDE_ARMS", HideArms);
                section.Set("HIDE_STEER", HideSteeringWheel);
                section.Set("LOCK_STEER", LockSteeringWheel);
                section.Set("WORLD_DETAIL", WorldDetail);

                Ini["EFFECTS"].Set("MOTION_BLUR", MotionBlur);
                Ini["EFFECTS"].Set("RENDER_SMOKE_IN_MIRROR", SmokeInMirrors);
                Ini["EFFECTS"].Set("SMOKE", SmokeLevel);

                section = Ini["POST_PROCESS"];
                section.Set("ENABLED", PostProcessing);
                section.Set("QUALITY", PostProcessingQuality);
                section.Set("FILTER", PostProcessingFilter);
                section.Set("GLARE", GlareQuality);
                section.Set("DOF", DepthOfFieldQuality);
                section.Set("RAYS_OF_GOD", RaysOfGod);
                section.Set("HEAT_SHIMMER", HeatShimmering);
                section.Set("FXAA", Fxaa);
                Ini["SATURATION"].Set("LEVEL", ColorSaturation);

                Ini["MIRROR"].Set("HQ", MirrorHighQuality);
                Ini["MIRROR"].Set("SIZE", MirrorResolution);

                section = Ini["CUBEMAP"];
                if (CubemapResolution.Value == "0") {
                    section.Set("SIZE", 0);
                    section.Set("FACES_PER_FRAME", 0);
                    section.Set("FARPLANE", 0);
                    section.Set("__ORIG_FACES_PER_FRAME", CubemapRenderingFrequency);
                    section.Set("__ORIG_FARPLANE", CubemapDistance);
                } else {
                    section.Set("SIZE", CubemapResolution);
                    section.Set("FACES_PER_FRAME", CubemapRenderingFrequency);
                    section.Set("FARPLANE", CubemapDistance);
                    section.Remove("__ORIG_FACES_PER_FRAME");
                    section.Remove("__ORIG_FARPLANE");
                }
            }
        }

        private static VideoSettings _video;

        public static VideoSettings Video => _video ?? (_video = new VideoSettings());
    }
}
