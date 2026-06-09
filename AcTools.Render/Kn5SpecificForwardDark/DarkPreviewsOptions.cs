using System;
using System.Drawing;
using AcTools.Render.Forward;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkPreviewsOptions {
        public string PreviewName = "preview.jpg";

        /// <summary>
        /// Either ID or KN5’s filename.
        /// </summary>
        [CanBeNull]
        public string Showroom = null;

        public double SsaaMultiplier = 4d;
        public int PreviewWidth = CommonAcConsts.PreviewWidth;
        public int PreviewHeight = CommonAcConsts.PreviewHeight;
        public bool UseFxaa = false;
        public bool UseSmaa = false;
        public bool UseMsaa = false;
        public int MsaaSampleCount = 4;
        public bool SoftwareDownsize = false;

        public bool WireframeMode = false;
        public bool MeshDebugMode = false;
        public bool SuspensionDebugMode = false;

        public bool HeadlightsEnabled = false;
        public bool BrakeLightsEnabled = false;
        public double SteerDeg = 0d;
        public bool LeftDoorOpen = false;
        public bool RightDoorOpen = false;
        public bool ShowDriver = false;
        public bool ShowSeatbelt = false;
        public bool ShowBlurredRims = false;

        public double[] CameraPosition = { 3.194, 0.342, 13.049 };
        public double[] CameraLookAt = { 2.945, 0.384, 12.082 };
        public double CameraFov = 9d;
        public double CameraTilt = 0d;

        public bool AlignCar = true;
        public bool AlignCameraHorizontally = true;
        public bool AlignCameraVertically = false;
        public bool AlignCameraHorizontallyOffsetRelative = true;
        public bool AlignCameraVerticallyOffsetRelative = true;
        public double AlignCameraHorizontallyOffset = 0.3;
        public double AlignCameraVerticallyOffset = 0.0;

        public bool AnyGround = true;
        public bool FlatMirror = false;
        public bool FlatMirrorBlurred = false;
        public double FlatMirrorBlurMuiltiplier = 1d;
        public double FlatMirrorReflectiveness = 1d;
        public bool FlatMirrorReflectedLight = false;

        public float CubemapAmbient = 0.5f;
        public bool CubemapAmbientWhite = true;
        public bool UseBloom = true;
        public bool UseSslr = false;
        public bool UseAo = false;
        public AoType AoType = AoType.Ssao;
        public bool EnableShadows = true;
        public bool UsePcss = true;
        public int ShadowMapSize = 4096;
        public bool ReflectionCubemapAtCamera = true;
        public bool ReflectionsWithShadows = false;
        public bool ReflectionsWithMultipleLights = false;

        public Color BackgroundColor = Color.Black;
        public Color LightColor = Color.FromArgb(0xffffff);
        public Color AmbientUp = Color.FromArgb(0xb4b496);
        public Color AmbientDown = Color.FromArgb(0x96b4b4);

        public double AmbientBrightness = 2d;
        public double BackgroundBrightness = 1d;
        public double LightBrightness = 1.5;
        public double[] LightDirection = { 0.2, 1.0, 0.8 };

        public ToneMappingFn ToneMapping = ToneMappingFn.None;
        public bool UseDither = false;
        public bool UseColorGrading = false;
        public double ToneExposure = 0.8;
        public double ToneGamma = 1.0;
        public double ToneWhitePoint = 1.66;

        [CanBeNull]
        public byte[] ColorGradingData = null;

        public bool UseCustomReflectionCubemap = false;
        public double CustomReflectionBrightness = 1.5;

        [CanBeNull]
        public byte[] CustomReflectionCubemapData = null;

        public double MaterialsReflectiveness = 1.2;
        public double CarShadowsOpacity = 1.0;
        public double BloomRadiusMultiplier = 0.8d;
        public double PcssSceneScale = 0.06d;
        public double PcssLightScale = 2d;
        public double AoOpacity = 0.3d;
        public double AoRadius = 1d;

        public bool DelayedConvertation = true;

        public bool UseDof = false;
        public double DofFocusPlane = 1.6;
        public double DofScale = 1d;
        public bool UseAccumulationDof = false;
        public bool AccumulationDofBokeh = false;
        public int AccumulationDofIterations = 300;
        public double AccumulationDofApertureSize = 0.01;

        public bool LoadCarLights = false;
        public bool TryToGuessCarLights = true;
        public bool LoadShowroomLights = false;

        [CanBeNull]
        public string[] ExtraActiveAnimations;

        [CanBeNull]
        public string SerializedLights;

        #region Checksum
        private static int GetHashCode(double[] array) {
            if (array == null) return 0;
            unchecked {
                var hashCode = 0;
                for (var i = 0; i < array.Length; i++) {
                    hashCode = (hashCode * 397) ^ array[i].GetHashCode();
                }
                return hashCode;
            }
        }

        private static int GetHashCode(byte[] array) {
            if (array == null) return 0;
            unchecked {
                var hashCode = 0;
                for (var i = 0; i < array.Length; i += 1000) {
                    hashCode = (hashCode * 397) ^ array[i].GetHashCode();
                }
                return hashCode;
            }
        }

        // public string FixedChecksum { get; set; }

        public string GetChecksum(bool cspRenderMode) {
            // if (FixedChecksum != null) return FixedChecksum;

            unchecked {
                long hashCode = Showroom?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ SsaaMultiplier.GetHashCode();
                hashCode = (hashCode * 397) ^ PreviewWidth;
                hashCode = (hashCode * 397) ^ PreviewHeight;
                hashCode = (hashCode * 397) ^ UseFxaa.GetHashCode();
                hashCode = (hashCode * 397) ^ UseSmaa.GetHashCode();
                hashCode = (hashCode * 397) ^ UseMsaa.GetHashCode();
                hashCode = (hashCode * 397) ^ MsaaSampleCount;
                hashCode = (hashCode * 397) ^ SoftwareDownsize.GetHashCode();
                hashCode = (hashCode * 397) ^ WireframeMode.GetHashCode();
                hashCode = (hashCode * 397) ^ MeshDebugMode.GetHashCode();
                hashCode = (hashCode * 397) ^ SuspensionDebugMode.GetHashCode();
                hashCode = (hashCode * 397) ^ HeadlightsEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ BrakeLightsEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ SteerDeg.GetHashCode();
                hashCode = (hashCode * 397) ^ LeftDoorOpen.GetHashCode();
                hashCode = (hashCode * 397) ^ RightDoorOpen.GetHashCode();
                hashCode = (hashCode * 397) ^ ShowDriver.GetHashCode();
                hashCode = (hashCode * 397) ^ GetHashCode(CameraPosition);
                hashCode = (hashCode * 397) ^ GetHashCode(CameraLookAt);
                hashCode = (hashCode * 397) ^ CameraFov.GetHashCode();
                hashCode = (hashCode * 397) ^ CameraTilt.GetHashCode();

                hashCode = (hashCode * 397) ^ AlignCar.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraHorizontally.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraVertically.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraHorizontallyOffsetRelative.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraVerticallyOffsetRelative.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraHorizontallyOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraVerticallyOffset.GetHashCode();
                if (!AnyGround) hashCode = (hashCode * 397) ^ 91001923;
                if (ShowSeatbelt) hashCode = (hashCode * 397) ^ 6327277;
                if (ShowBlurredRims) hashCode = (hashCode * 397) ^ 362618;

                hashCode = (hashCode * 397) ^ FlatMirror.GetHashCode();
                hashCode = (hashCode * 397) ^ FlatMirrorBlurred.GetHashCode();
                if (FlatMirrorBlurMuiltiplier != 1d) hashCode = (hashCode * 397) ^ FlatMirrorBlurMuiltiplier.GetHashCode();
                hashCode = (hashCode * 397) ^ FlatMirrorReflectiveness.GetHashCode();
                if (FlatMirrorReflectedLight) hashCode = (hashCode * 397) ^ 13214385;
                hashCode = (hashCode * 397) ^ CubemapAmbient.GetHashCode();
                hashCode = (hashCode * 397) ^ CubemapAmbientWhite.GetHashCode();
                hashCode = (hashCode * 397) ^ UseBloom.GetHashCode();
                hashCode = (hashCode * 397) ^ UseSslr.GetHashCode();
                hashCode = (hashCode * 397) ^ UseAo.GetHashCode();
                hashCode = (hashCode * 397) ^ AoType.GetHashCode();
                hashCode = (hashCode * 397) ^ EnableShadows.GetHashCode();
                hashCode = (hashCode * 397) ^ UsePcss.GetHashCode();
                hashCode = (hashCode * 397) ^ ShadowMapSize;
                hashCode = (hashCode * 397) ^ ReflectionCubemapAtCamera.GetHashCode();
                hashCode = (hashCode * 397) ^ ReflectionsWithShadows.GetHashCode();
                if (ReflectionsWithMultipleLights) hashCode = (hashCode * 397) ^ 8909091223;

                hashCode = (hashCode * 397) ^ BackgroundColor.GetHashCode();
                hashCode = (hashCode * 397) ^ LightColor.GetHashCode();
                hashCode = (hashCode * 397) ^ AmbientUp.GetHashCode();
                hashCode = (hashCode * 397) ^ AmbientDown.GetHashCode();
                hashCode = (hashCode * 397) ^ AmbientBrightness.GetHashCode();
                hashCode = (hashCode * 397) ^ BackgroundBrightness.GetHashCode();
                hashCode = (hashCode * 397) ^ LightBrightness.GetHashCode();
                hashCode = (hashCode * 397) ^ GetHashCode(LightDirection);

                if (UseDither) hashCode = (hashCode * 397) ^ 142004286;
                hashCode = (hashCode * 397) ^ UseColorGrading.GetHashCode();
                hashCode = (hashCode * 397) ^ ToneExposure.GetHashCode();
                hashCode = (hashCode * 397) ^ ToneGamma.GetHashCode();
                hashCode = (hashCode * 397) ^ ToneWhitePoint.GetHashCode();
                hashCode = (hashCode * 397) ^ GetHashCode(ColorGradingData);
                hashCode = (hashCode * 397) ^ MaterialsReflectiveness.GetHashCode();
                if (CarShadowsOpacity != 1d) hashCode = (hashCode * 397) ^ CarShadowsOpacity.GetHashCode();
                hashCode = (hashCode * 397) ^ BloomRadiusMultiplier.GetHashCode();
                hashCode = (hashCode * 397) ^ PcssSceneScale.GetHashCode();
                hashCode = (hashCode * 397) ^ PcssLightScale.GetHashCode();
                hashCode = (hashCode * 397) ^ AoOpacity.GetHashCode();
                hashCode = (hashCode * 397) ^ AoRadius.GetHashCode();
                if (SerializedLights != null) hashCode = (hashCode * 397) ^ SerializedLights.GetHashCode();

                if (UseCustomReflectionCubemap) {
                    hashCode = (hashCode * 397) ^ UseCustomReflectionCubemap.GetHashCode();
                    hashCode = (hashCode * 397) ^ CustomReflectionBrightness.GetHashCode();
                    hashCode = (hashCode * 397) ^ (CustomReflectionCubemapData?.Length ?? -1).GetHashCode();
                }

                if (ExtraActiveAnimations?.Length > 0) {
                    hashCode = (hashCode * 397) ^ ExtraActiveAnimations.GetEnumerableHashCode();
                }

                if (UseDof) {
                    hashCode = (hashCode * 397) ^ 70094303;
                    hashCode = (hashCode * 397) ^ DofFocusPlane.GetHashCode();

                    if (UseAccumulationDof) {
                        hashCode = (hashCode * 397) ^ 68657796;
                        hashCode = (hashCode * 397) ^ AccumulationDofApertureSize.GetHashCode();
                        hashCode = (hashCode * 397) ^ AccumulationDofIterations.GetHashCode();
                        hashCode = (hashCode * 397) ^ AccumulationDofBokeh.GetHashCode();
                    } else {
                        hashCode = (hashCode * 397) ^ DofScale.GetHashCode();
                    }
                }

                if (!LoadCarLights) hashCode = (hashCode * 397) ^ 96745201;
                if (!TryToGuessCarLights) hashCode = (hashCode * 397) ^ 64351024;
                if (!LoadShowroomLights) hashCode = (hashCode * 397) ^ 93872189;
                if (cspRenderMode) hashCode = (hashCode * 397) ^ 46728481;

                return Convert.ToBase64String(BitConverter.GetBytes(hashCode)).TrimEnd('=');
            }
        }
        #endregion
    }
}