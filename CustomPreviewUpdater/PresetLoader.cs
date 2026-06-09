using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Media;
using AcTools;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CustomPreviewUpdater {
    /* Messy copy-paste from AcManager library. I’d be happy to do it properly, but compiler is getting too slow for a good rewrite. Maybe I’ll redo thing properly a bit later… */
    internal static class PresetLoader {
        public static DarkPreviewsOptions Load(string filename) {
            return JsonConvert.DeserializeObject<SaveableData>(File.ReadAllText(filename)).ToPreviewsOptions(true);
        }

        public enum CarAmbientShadowsMode {
            [Description("Attached Shadow")]
            Attached = 0,

            [Description("Simple Occlusion")]
            Occlusion = 1,

            [Description("Blurred Occlusion")]
            Blurred = 2
        }

        private class SaveableDataBase {
            public virtual Color AmbientDownColor { get; set; } = Color.FromRgb(0xBA, 0xBA, 0xBA);
            public virtual Color AmbientUpColor { get; set; } = Color.FromRgb(0xE2, 0xE2, 0xE2);
            public virtual Color BackgroundColor { get; set; } = Color.FromRgb(0xFF, 0xFF, 0xFF);
            public virtual Color LightColor { get; set; } = Color.FromRgb(0xFF, 0xFF, 0xFF);

            public virtual int MsaaMode { get; set; }
            public virtual int SsaaMode { get; set; } = 1;
            public virtual int ShadowMapSize { get; set; } = 2048;
            public int CubemapReflectionMapSize { get; set; } = 1024;
            public int CubemapReflectionFacesPerFrame { get; set; } = 2;

            public virtual string ShowroomId { get; set; }
            public virtual string ColorGrading { get; set; }

            public virtual AoType AoType { get; set; } = AoType.SsaoAlt;
            public CarAmbientShadowsMode CarAmbientShadowsMode { get; set; } = CarAmbientShadowsMode.Attached;

            [JsonProperty("CubemapAmbientValue")]
            public virtual float CubemapAmbient { get; set; } = 0.5f;

            public virtual bool CubemapAmbientWhite { get; set; } = true;
            public virtual bool EnableShadows { get; set; } = true;
            public virtual bool AnyGround { get; set; } = true;
            public virtual bool FlatMirror { get; set; }
            public virtual bool FlatMirrorBlurred { get; set; } = true;
            public virtual float FlatMirrorBlurMuiltiplier { get; set; } = 1f;
            public virtual bool UseBloom { get; set; } = true;
            public virtual bool UseDither { get; set; } = true;
            public virtual bool UseColorGrading { get; set; }
            public virtual bool UseFxaa { get; set; }
            public virtual bool UsePcss { get; set; }
            public virtual bool UseSmaa { get; set; }
            public virtual bool UseAo { get; set; }
            public virtual bool UseSslr { get; set; }
            public virtual bool ReflectionCubemapAtCamera { get; set; }
            public virtual bool ReflectionsWithShadows { get; set; }
            public virtual bool ReflectionsWithMultipleLights { get; set; }

            public virtual float AmbientBrightness { get; set; } = 1.58f;
            public virtual float BackgroundBrightness { get; set; } = 1.0f;
            public virtual float FlatMirrorReflectiveness { get; set; } = 0.6f;
            public virtual bool FlatMirrorReflectedLight { get; set; }
            public virtual float LightBrightness { get; set; } = 1.27f;
            public virtual float Lightθ { get; set; } = 50f;
            public virtual float Lightφ { get; set; } = 104f;
            public virtual float MaterialsReflectiveness { get; set; } = 1f;
            public virtual float CarShadowsOpacity { get; set; } = 1f;

            public int ToneVersion;
            public virtual ToneMappingFn ToneMapping { get; set; } = ToneMappingFn.Reinhard;
            public virtual float ToneExposure { get; set; } = 1.189f;
            public virtual float ToneGamma { get; set; } = 1f;
            public virtual float ToneWhitePoint { get; set; } = 1.66f;

            public virtual float PcssSceneScale { get; set; } = 0.06f;
            public virtual float PcssLightScale { get; set; } = 2f;
            public virtual float BloomRadiusMultiplier { get; set; } = 1f;

            [JsonProperty("SsaoOpacity")]
            public virtual float AoOpacity { get; set; } = 0.3f;

            public virtual float AoRadius { get; set; } = 1f;

            public virtual bool MeshDebug { get; set; }
            public virtual bool MeshDebugWithEmissive { get; set; } = true;
            public virtual WireframeMode WireframeMode { get; set; } = WireframeMode.Disabled;
            public virtual bool IsWireframeColored { get; set; } = true;
            public virtual Color WireframeColor { get; set; } = Colors.White;
            public virtual float WireframeBrightness { get; set; } = 2.5f;

            public virtual bool UseCustomReflectionCubemap { get; set; }
            public virtual float CustomReflectionBrightness { get; set; } = 1.5f;
            public virtual string CustomReflectionCubemap { get; set; }

            public virtual bool UseDof { get; set; } = true;
            public virtual float DofFocusPlane { get; set; } = 1.6f;
            public virtual float DofScale { get; set; } = 1f;
            public virtual bool UseAccumulationDof { get; set; } = true;
            public virtual bool AccumulationDofBokeh { get; set; }
            public virtual int AccumulationDofIterations { get; set; } = 40;
            public virtual float AccumulationDofApertureSize { get; set; }

            [CanBeNull]
            public virtual JObject[] ExtraLights { get; set; }

            [CanBeNull]
            public virtual double[] CameraPosition { get; set; } = { 3.29, 1.10, 4.93 };

            [CanBeNull]
            public virtual double[] CameraLookAt { get; set; } = { -0.53, 0.49, 0.11 };

            public virtual float CameraTilt { get; set; }
            public virtual float CameraFov { get; set; } = 32f;
            public virtual bool CameraOrbitMode { get; set; } = true;

            [CanBeNull]
            private static byte[] Compress(byte[] data) {
                if (data == null) return null;

                try {
                    using (var memory = new MemoryStream()) {
                        using (var deflate = new DeflateStream(memory, CompressionMode.Compress)) {
                            deflate.Write(data, 0, data.Length);
                        }
                        return memory.ToArray();
                    }
                } catch (Exception e) {
                    return null;
                }
            }

            [CanBeNull]
            private static byte[] Decompress(byte[] data) {
                if (data == null) return null;

                try {
                    using (var memory = new MemoryStream(data)) {
                        return new DeflateStream(memory, CompressionMode.Decompress).ReadAsBytesAndDispose();
                    }
                } catch (Exception e) {
                    return null;
                }
            }

            private static byte[] GetBytes(string data) {
                if (data == null) return null;

                if (data.StartsWith(@"path:")) {
                    return null;
                }

                try {
                    return Decompress(Convert.FromBase64String(data));
                } catch (Exception e) {
                    return null;
                }
            }

            private static string SetBytes(byte[] data) {
                if (data == null) return null;

                var c = Compress(data);
                return c == null ? null : Convert.ToBase64String(c);
            }

            [JsonIgnore]
            public byte[] ColorGradingData {
                get => GetBytes(ColorGrading);
                set => ColorGrading = SetBytes(value);
            }

            [JsonIgnore]
            public byte[] CustomReflectionCubemapData {
                get => GetBytes(CustomReflectionCubemap);
                set => CustomReflectionCubemap = SetBytes(value);
            }
        }

        private sealed class SaveableData : SaveableDataBase {
            public override Color AmbientDownColor { get; set; } = Color.FromRgb(0x51, 0x5E, 0x6D);
            public override Color AmbientUpColor { get; set; } = Color.FromRgb(0xC6, 0xE6, 0xFF);
            public override Color BackgroundColor { get; set; } = Colors.White;
            public override Color LightColor { get; set; } = Color.FromRgb(0xFF, 0xEA, 0xCC);

            public override int MsaaMode { get; set; }
            public override int SsaaMode { get; set; } = 4;
            public override int ShadowMapSize { get; set; } = 1024;

            public override string ShowroomId { get; set; }
            public override string ColorGrading { get; set; }

            [JsonConstructor]
            public SaveableData(string showroomId) {
                ShowroomId = showroomId;
            }

            public SaveableData() {
                ShowroomId = "at_previews";
            }

            public override ToneMappingFn ToneMapping { get; set; } = ToneMappingFn.Filmic;
            public override AoType AoType { get; set; } = AoType.Ssao;

            [JsonProperty("CubemapAmbientValue")]
            public override float CubemapAmbient { get; set; }

            public override bool CubemapAmbientWhite { get; set; } = true;
            public override bool EnableShadows { get; set; } = true;
            public override bool FlatMirror { get; set; }
            public override bool FlatMirrorBlurred { get; set; } = true;
            public override float FlatMirrorBlurMuiltiplier { get; set; } = 1f;
            public override bool UseBloom { get; set; } = true;
            public override bool UseDither { get; set; }
            public override bool UseColorGrading { get; set; }
            public override bool UseFxaa { get; set; } = true;
            public override bool UsePcss { get; set; }
            public override bool UseSmaa { get; set; }
            public override bool UseAo { get; set; }
            public override bool UseSslr { get; set; } = true;
            public override bool ReflectionCubemapAtCamera { get; set; } = true;
            public override bool ReflectionsWithShadows { get; set; } = false;
            public override bool ReflectionsWithMultipleLights { get; set; } = false;

            public override float AmbientBrightness { get; set; } = 3f;
            public override float BackgroundBrightness { get; set; } = 3f;
            public override float FlatMirrorReflectiveness { get; set; } = 0.6f;
            public override bool FlatMirrorReflectedLight { get; set; } = false;
            public override float LightBrightness { get; set; } = 12f;
            public override float Lightθ { get; set; } = 45f;
            public override float Lightφ { get; set; } = 0f;
            public override float MaterialsReflectiveness { get; set; } = 0.66f;
            public override float CarShadowsOpacity { get; set; } = 1f;
            public override float ToneExposure { get; set; } = 1.634f;
            public override float ToneGamma { get; set; } = 0.85f;
            public override float ToneWhitePoint { get; set; } = 2.0f;
            public override float PcssSceneScale { get; set; } = 0.06f;
            public override float PcssLightScale { get; set; } = 2f;
            public override float BloomRadiusMultiplier { get; set; } = 1f;

            [JsonProperty("SsaoOpacity")]
            public override float AoOpacity { get; set; } = 0.3f;

            [CanBeNull]
            public string[] ExtraActiveAnimations { get; set; }

            public int Width = CommonAcConsts.PreviewWidth, Height = CommonAcConsts.PreviewHeight;

            public bool SoftwareDownsize, AlignCar = true,
                    AlignCameraHorizontally, AlignCameraVertically, AlignCameraHorizontallyOffsetRelative, AlignCameraVerticallyOffsetRelative,
                    HeadlightsEnabled, BrakeLightsEnabled, LeftDoorOpen, RightDoorOpen, ShowDriver, ShowSeatbelt, ShowBlurredRims,
                    TryToGuessCarLights = true, LoadCarLights, LoadShowroomLights;

            public string FileName { get; set; } = "preview.jpg";

            public override bool UseDof { get; set; } = true;
            public override float DofFocusPlane { get; set; } = 1.6f;
            public override float DofScale { get; set; } = 1f;
            public override bool UseAccumulationDof { get; set; } = true;
            public override bool AccumulationDofBokeh { get; set; }
            public override int AccumulationDofIterations { get; set; } = 40;
            public override float AccumulationDofApertureSize { get; set; } = 0f;

            public override double[] CameraPosition { get; set; } = { -3.8676, 1.4236, 4.7038 };
            public override double[] CameraLookAt { get; set; } = { 0, 0.7, 0.5 };
            public override float CameraFov { get; set; } = 30f;

            public float AlignCameraHorizontallyOffset, AlignCameraVerticallyOffset, SteerDeg;
            // public string Checksum { get; set; }

            /// <summary>
            /// Convert to DarkPreviewsOptions.
            /// </summary>
            /// <param name="keepChecksum">Set to True if you’re loading existing preset and want checksum to be
            /// independent from any future format changes; otherwise, for actual checksum, set to False.</param>
            /// <returns>Instance of DarkPreviewsOptions.</returns>
            [NotNull]
            public DarkPreviewsOptions ToPreviewsOptions(bool keepChecksum) {
                var light = MathF.ToVector3Deg(Lightθ, Lightφ);
                return new DarkPreviewsOptions {
                    AmbientDown = AmbientDownColor.ToColor(),
                    AmbientUp = AmbientUpColor.ToColor(),
                    BackgroundColor = BackgroundColor.ToColor(),
                    LightColor = LightColor.ToColor(),
                    UseMsaa = MsaaMode != 0,
                    MsaaSampleCount = MsaaMode,
                    SsaaMultiplier = Math.Sqrt(SsaaMode),
                    ShadowMapSize = ShadowMapSize,
                    Showroom = ShowroomId,
                    ColorGradingData = UseColorGrading ? ColorGradingData : null,
                    UseCustomReflectionCubemap = UseCustomReflectionCubemap,
                    CustomReflectionCubemapData = UseCustomReflectionCubemap ? CustomReflectionCubemapData : null,
                    CustomReflectionBrightness = CustomReflectionBrightness,
                    CubemapAmbient = CubemapAmbient,
                    CubemapAmbientWhite = CubemapAmbientWhite,
                    EnableShadows = EnableShadows,
                    AnyGround = AnyGround,
                    FlatMirror = FlatMirror,
                    UseBloom = UseBloom,
                    FlatMirrorBlurred = FlatMirrorBlurred,
                    FlatMirrorBlurMuiltiplier = FlatMirrorBlurMuiltiplier,
                    UseDither = UseDither,
                    UseColorGrading = UseColorGrading,
                    UseFxaa = UseFxaa,
                    UseSmaa = UseSmaa,
                    UsePcss = UsePcss,
                    UseAo = UseAo,
                    AoType = AoType,
                    UseSslr = UseSslr,
                    ToneMapping = ToneMapping,
                    ReflectionCubemapAtCamera = ReflectionCubemapAtCamera,
                    ReflectionsWithShadows = ReflectionsWithShadows,
                    ReflectionsWithMultipleLights = ReflectionsWithMultipleLights,
                    AmbientBrightness = AmbientBrightness,
                    BackgroundBrightness = BackgroundBrightness,
                    FlatMirrorReflectiveness = FlatMirrorReflectiveness,
                    FlatMirrorReflectedLight = FlatMirrorReflectedLight,
                    LightBrightness = LightBrightness,
                    LightDirection = new double[] { light.X, light.Y, light.Z },
                    MaterialsReflectiveness = MaterialsReflectiveness,
                    CarShadowsOpacity = CarShadowsOpacity,
                    ToneExposure = ToneExposure,
                    ToneGamma = ToneGamma,
                    ToneWhitePoint = ToneWhitePoint,
                    PcssSceneScale = PcssSceneScale,
                    PcssLightScale = PcssLightScale,
                    BloomRadiusMultiplier = BloomRadiusMultiplier,
                    AoOpacity = AoOpacity,
                    AoRadius = AoRadius,
                    UseDof = UseDof,
                    DofFocusPlane = DofFocusPlane,
                    DofScale = DofScale,
                    UseAccumulationDof = UseAccumulationDof,
                    AccumulationDofBokeh = AccumulationDofBokeh,
                    AccumulationDofIterations = AccumulationDofIterations,
                    AccumulationDofApertureSize = AccumulationDofApertureSize,
                    PreviewWidth = Width,
                    PreviewHeight = Height,
                    SoftwareDownsize = SoftwareDownsize,
                    AlignCar = AlignCar,
                    AlignCameraHorizontally = AlignCameraHorizontally,
                    AlignCameraVertically = AlignCameraVertically,
                    AlignCameraHorizontallyOffsetRelative = AlignCameraHorizontallyOffsetRelative,
                    AlignCameraVerticallyOffsetRelative = AlignCameraVerticallyOffsetRelative,
                    HeadlightsEnabled = HeadlightsEnabled,
                    BrakeLightsEnabled = BrakeLightsEnabled,
                    LeftDoorOpen = LeftDoorOpen,
                    RightDoorOpen = RightDoorOpen,
                    ShowDriver = ShowDriver,
                    ShowSeatbelt = ShowSeatbelt,
                    ShowBlurredRims = ShowBlurredRims,
                    CameraPosition = CameraPosition,
                    CameraLookAt = CameraLookAt,
                    CameraTilt = CameraTilt,
                    CameraFov = CameraFov,
                    AlignCameraHorizontallyOffset = AlignCameraHorizontallyOffset,
                    AlignCameraVerticallyOffset = AlignCameraVerticallyOffset,
                    SteerDeg = SteerDeg,
                    TryToGuessCarLights = TryToGuessCarLights,
                    LoadCarLights = LoadCarLights,
                    LoadShowroomLights = LoadShowroomLights,
                    DelayedConvertation = false,
                    MeshDebugMode = false,
                    SuspensionDebugMode = false,
                    PreviewName = FileName,
                    WireframeMode = false,
                    SerializedLights = ExtraLights != null ? new JArray(ExtraLights.OfType<object>()).ToString() : null,
                    ExtraActiveAnimations = ExtraActiveAnimations,
                    // FixedChecksum = keepChecksum ? Checksum : null
                };
            }
        }
    }

    public static class ColorExtension {
        public static string ToHexString(this Color color, bool alphaChannel = false) {
            return $"#{(alphaChannel ? color.A.ToString("X2") : string.Empty)}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static Color SetAlpha(this Color color, byte alpha) {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        public static System.Drawing.Color SetAlpha(this System.Drawing.Color color, byte alpha) {
            return System.Drawing.Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        public static Color ToColor(this System.Drawing.Color color) {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Drawing.Color ToColor(this Color color) {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}