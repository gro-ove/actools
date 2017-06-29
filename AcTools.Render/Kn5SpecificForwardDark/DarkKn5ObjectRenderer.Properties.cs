using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.PostEffects.AO;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark.Lights;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public enum AoType {
        [Description("SSAO")]
        Ssao,

        [Description("SSAO (Alt.)")]
        SsaoAlt,

        [Description("HBAO")]
        Hbao,

        [Description("ASSAO")]
        Assao
    }

    public partial class DarkKn5ObjectRenderer {
        public static readonly AoType[] ProductionReadyAo = {
            AoType.Ssao, AoType.SsaoAlt
        };

        #region Colors, brightness
        private Color _lightColor = Color.FromArgb(200, 180, 180);

        public Color LightColor {
            get { return _lightColor; }
            set {
                if (value.Equals(_lightColor)) return;
                _lightColor = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private float _lightBrightness = 1.5f;

        public float LightBrightness {
            get { return _lightBrightness; }
            set {
                if (Equals(value, _lightBrightness)) return;
                _lightBrightness = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private float _ambientBrightness = 2f;

        public float AmbientBrightness {
            get { return _ambientBrightness; }
            set {
                if (Equals(value, _ambientBrightness)) return;
                _ambientBrightness = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private Color _ambientDown = Color.FromArgb(150, 180, 180);

        public Color AmbientDown {
            get { return _ambientDown; }
            set {
                if (value.Equals(_ambientDown)) return;
                _ambientDown = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private Color _ambientUp = Color.FromArgb(180, 180, 150);

        public Color AmbientUp {
            get { return _ambientUp; }
            set {
                if (value.Equals(_ambientUp)) return;
                _ambientUp = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }
        #endregion

        #region Flat mirror/ground
        private bool _anyGround = true;

        public bool AnyGround {
            get { return _anyGround; }
            set {
                if (Equals(value, _anyGround)) return;
                _anyGround = value;

                if (!value) {
                    FlatMirror = false;
                }

                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private bool _flatMirror;

        public bool FlatMirror {
            get { return _flatMirror; }
            set {
                if (value == _flatMirror) return;
                _flatMirror = value;

                if (value) {
                    AnyGround = true;
                }

                IsDirty = true;
                OnPropertyChanged();
                _mirrorDirty = true;
                UpdateBlurredFlatMirror();
            }
        }

        private bool _flatMirrorBlurred;

        public bool FlatMirrorBlurred {
            get { return _flatMirrorBlurred; }
            set {
                if (Equals(value, _flatMirrorBlurred)) return;
                _flatMirrorBlurred = value;
                IsDirty = true;
                OnPropertyChanged();
                UpdateBlurredFlatMirror();
            }
        }

        private bool _opaqueGround = true;

        public bool OpaqueGround {
            get { return _opaqueGround; }
            set {
                if (Equals(value, _opaqueGround)) return;
                _opaqueGround = value;
                IsDirty = true;
                OnPropertyChanged();
                _mirrorDirty = true;
            }
        }

        private float _flatMirrorReflectiveness = 0.6f;

        public float FlatMirrorReflectiveness {
            get { return _flatMirrorReflectiveness; }
            set {
                if (Equals(value, _flatMirrorReflectiveness)) return;
                _flatMirrorReflectiveness = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _flatMirrorReflectedLight;

        public bool FlatMirrorReflectedLight {
            get { return _flatMirrorReflectedLight; }
            set {
                if (Equals(value, _flatMirrorReflectedLight)) return;
                _flatMirrorReflectedLight = value;
                OnPropertyChanged();
                IsDirty = true;
                SetShadowsDirty();
            }
        }
        #endregion

        #region AO, SSLR, G-Buffer
        private bool _gBufferMsaa = true;

        public bool GBufferMsaa {
            get { return _gBufferMsaa; }
            set {
                if (value == _gBufferMsaa) return;
                _gBufferMsaa = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        #region AO
        private float _aoOpacity = 0.3f;

        public float AoOpacity {
            get { return _aoOpacity; }
            set {
                if (Equals(value, _aoOpacity)) return;
                _aoOpacity = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _useAo;

        public bool UseAo {
            get { return _useAo; }
            set {
                if (Equals(value, _useAo)) return;
                _useAo = value;
                IsDirty = true;
                OnPropertyChanged();

                RecreateAoBuffer();
                UpdateGBuffers();
            }
        }

        private AoType _aoType = AoType.Ssao;

        public AoType AoType {
            get { return _aoType; }
            set {
#if !DEBUG
                if (!ProductionReadyAo.Contains(value)) value = AoType.Ssao;
#endif

                if (Equals(value, _aoType)) return;
                _aoType = value;
                IsDirty = true;
                OnPropertyChanged();
                RecreateAoBuffer();
            }
        }

        private bool _aoDebug;

        public bool AoDebug {
            get { return _aoDebug; }
            set {
                if (Equals(value, _aoDebug)) return;
                _aoDebug = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Correct ambient shadows
        private bool _useCorrectAmbientShadows;

        public bool UseCorrectAmbientShadows {
            get { return _useCorrectAmbientShadows && ShowroomNode != null; }
            set {
                if (Equals(value, _useCorrectAmbientShadows)) return;
                _useCorrectAmbientShadows = value;
                IsDirty = true;
                OnPropertyChanged();

                RecreateAoBuffer();
                UpdateGBuffers();
            }
        }

        private bool _blurCorrectAmbientShadows;

        public bool BlurCorrectAmbientShadows {
            get { return _blurCorrectAmbientShadows; }
            set {
                if (Equals(value, _blurCorrectAmbientShadows)) return;
                _blurCorrectAmbientShadows = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }
        #endregion

        #region SSLR
        private bool _useSslr;
        private DarkSslr _sslr;

        public bool UseSslr {
            get { return _useSslr; }
            set {
                if (Equals(value, _useSslr)) return;
                _useSslr = value;
                IsDirty = true;
                OnPropertyChanged();

                if (!value) {
                    DisposeHelper.Dispose(ref _sslr);
                } else if (_sslr == null) {
                    _sslr = new DarkSslr();
                }

                UpdateGBuffers();
            }
        }
        #endregion
        #endregion

        #region Shadows
        private int _shadowMapSize = 2048;

        public int ShadowMapSize {
            get { return _shadowMapSize; }
            set {
                if (Equals(value, _shadowMapSize)) return;
                _shadowMapSize = value;
                IsDirty = true;
                OnPropertyChanged();
                SetShadowsDirty();
            }
        }
        #endregion

        #region Reflections
        private float _materialsReflectiveness = 1f;

        public float MaterialsReflectiveness {
            get { return _materialsReflectiveness; }
            set {
                if (Equals(value, _materialsReflectiveness)) return;
                _materialsReflectiveness = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private bool _reflectionsWithShadows;

        public bool ReflectionsWithShadows {
            get { return _reflectionsWithShadows; }
            set {
                if (Equals(value, _reflectionsWithShadows)) return;
                _reflectionsWithShadows = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private bool _cubemapAmbientWhite = true;

        public bool CubemapAmbientWhite {
            get { return _cubemapAmbientWhite; }
            set {
                if (Equals(value, _cubemapAmbientWhite)) return;
                _cubemapAmbientWhite = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private float _cubemapAmbient = 0.5f;

        public float CubemapAmbient {
            get { return _cubemapAmbient; }
            set {
                if (Equals(value, _cubemapAmbient)) return;
                _cubemapAmbient = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _reflectionsWithMultipleLights;

        public bool ReflectionsWithMultipleLights {
            get { return _reflectionsWithMultipleLights; }
            set {
                if (Equals(value, _reflectionsWithMultipleLights)) return;
                _reflectionsWithMultipleLights = value;
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }
        #endregion

        #region PCSS
        private float _pcssLightScale = 2f;

        public float PcssLightScale {
            get { return _pcssLightScale; }
            set {
                if (Equals(value, _pcssLightScale)) return;
                _pcssLightScale = value;
                IsDirty = true;
                _pcssParamsSet = false;
                OnPropertyChanged();
            }
        }

        private float _pcssSceneScale = 0.06f;

        public float PcssSceneScale {
            get { return _pcssSceneScale; }
            set {
                if (Equals(value, _pcssSceneScale)) return;
                _pcssSceneScale = value;
                IsDirty = true;
                _pcssParamsSet = false;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}