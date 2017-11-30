using System.Drawing;
using AcTools.Utils.Helpers;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer {
        #region Colors, brightness
        private Color _lightColor = Color.FromArgb(200, 180, 180);

        public Color LightColor {
            get => _lightColor;
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
            get => _lightBrightness;
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
            get => _ambientBrightness;
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
            get => _ambientDown;
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
            get => _ambientUp;
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
            get => _anyGround;
            set {
                if (Equals(value, _anyGround)) return;
                _anyGround = value;
                OnPropertyChanged();
                IsDirty = true;
                _mirrorDirty = true;
                UpdateMirrorBuffers();

                /*if (!value) {
                    FlatMirror = false;
                    OpaqueGround = false;
                }*/
            }
        }

        private bool _flatMirror;

        public bool FlatMirror {
            get => _flatMirror;
            set {
                if (value == _flatMirror) return;
                _flatMirror = value;
                OnPropertyChanged();
                IsDirty = true;
                _mirrorDirty = true;
                UpdateMirrorBuffers();

                /*if (value) {
                    AnyGround = true;
                }*/
            }
        }

        private bool _flatMirrorBlurred;

        public bool FlatMirrorBlurred {
            get => _flatMirrorBlurred;
            set {
                if (Equals(value, _flatMirrorBlurred)) return;
                _flatMirrorBlurred = value;
                IsDirty = true;
                OnPropertyChanged();
                UpdateMirrorBuffers();
            }
        }

        private float _flatMirrorBlurMuiltiplier;

        public float FlatMirrorBlurMuiltiplier {
            get => _flatMirrorBlurMuiltiplier;
            set {
                if (Equals(value, _flatMirrorBlurMuiltiplier)) return;
                _flatMirrorBlurMuiltiplier = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _opaqueGround = true;

        public bool OpaqueGround {
            get => _opaqueGround;
            set {
                if (Equals(value, _opaqueGround)) return;
                _opaqueGround = value;
                IsDirty = true;
                OnPropertyChanged();
                _mirrorDirty = true;

                if (value) {
                    AnyGround = true;
                }
            }
        }

        private float _flatMirrorReflectiveness = 0.6f;

        public float FlatMirrorReflectiveness {
            get => _flatMirrorReflectiveness;
            set {
                if (Equals(value, _flatMirrorReflectiveness)) return;
                _flatMirrorReflectiveness = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _flatMirrorReflectedLight;

        public bool FlatMirrorReflectedLight {
            get => _flatMirrorReflectedLight;
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
            get => _gBufferMsaa;
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
            get => _aoOpacity;
            set {
                if (Equals(value, _aoOpacity)) return;
                _aoOpacity = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private float _aoRadius = 1f;

        public float AoRadius {
            get => _aoRadius;
            set {
                if (Equals(value, _aoRadius)) return;
                _aoRadius = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _useAo;

        public bool UseAo {
            get => _useAo;
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
                if (!value.IsProductionReady()) value = AoType.Ssao;
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
            get => _aoDebug;
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
            get => _useCorrectAmbientShadows && ShowroomNode != null;
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
            get => _blurCorrectAmbientShadows;
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
            get => _useSslr;
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
            get => _shadowMapSize;
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
            get => _materialsReflectiveness;
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
            get => _reflectionsWithShadows;
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
            get => _cubemapAmbientWhite;
            set {
                if (Equals(value, _cubemapAmbientWhite)) return;
                _cubemapAmbientWhite = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private float _cubemapAmbient = 0.5f;

        public float CubemapAmbient {
            get => _cubemapAmbient;
            set {
                if (Equals(value, _cubemapAmbient)) return;
                _cubemapAmbient = value;
                IsDirty = true;
                OnPropertyChanged();

                if (value <= 0f) {
                    _lightProbes.DisposeEverything();
                }
            }
        }

        private bool _reflectionsWithMultipleLights;

        public bool ReflectionsWithMultipleLights {
            get => _reflectionsWithMultipleLights;
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
            get => _pcssLightScale;
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
            get => _pcssSceneScale;
            set {
                if (Equals(value, _pcssSceneScale)) return;
                _pcssSceneScale = value;
                IsDirty = true;
                _pcssParamsSet = false;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Debugging
        private bool _meshDebug;

        public bool MeshDebug {
            get => _meshDebug;
            set {
                if (Equals(value, _meshDebug)) return;
                _meshDebug = value;
                IsDirty = true;
                OnPropertyChanged();
                UpdateMeshDebug(CarNode);
            }
        }

        private bool _showDepth;

        public bool ShowDepth {
            get => _showDepth;
            set {
                if (Equals(value, _showDepth)) return;
                _showDepth = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }
        #endregion

        #region DOF
        private DarkDof _dof;
        private bool _useDof;

        public bool UseDof {
            get => _useDof;
            set {
                if (Equals(value, _useDof)) return;
                _useDof = value;
                OnPropertyChanged();

                if (!value) {
                    DisposeHelper.Dispose(ref _dof);
                } else if (_dof == null) {
                    _dof = new DarkDof();
                }

                IsDirty = true;
                UpdateGBuffers();
            }
        }

        private float _dofFocusPlane = 1.6f;

        public float DofFocusPlane {
            get => _dofFocusPlane;
            set {
                if (Equals(value, _dofFocusPlane)) return;
                _dofFocusPlane = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _dofScale = 1f;

        public float DofScale {
            get => _dofScale;
            set {
                if (Equals(value, _dofScale)) return;
                _dofScale = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }
        #endregion
    }
}