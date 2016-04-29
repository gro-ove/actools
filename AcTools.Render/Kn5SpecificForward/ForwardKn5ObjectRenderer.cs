using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Render.Kn5SpecificDeferred;
using AcTools.Render.Kn5SpecificForward.Materials;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward {
    public class ForwardKn5ObjectRenderer : ForwardRenderer, IKn5ObjectRenderer {
        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public bool AutoRotate { get; set; } = true;

        private readonly Kn5 _kn5;
        private readonly Kn5CarHelper _kn5CarHelper;

        public ForwardKn5ObjectRenderer(string mainKn5Filename) {
            _kn5 = Kn5.FromFile(mainKn5Filename);
            _kn5CarHelper = new Kn5CarHelper(mainKn5Filename);
        }

        protected override void InitializeInner() {
            base.InitializeInner();

            Kn5MaterialsProvider.Initialize(new MaterialsProviderSimple());

            Kn5MaterialsProvider.SetKn5(_kn5);
            TexturesProvider.SetKn5(_kn5);

            var node = Kn5Converter.Convert(_kn5.RootNode);
            Scene.Add(node);

            var asList = node as Kn5RenderableList;
            if (asList != null) {
                Scene.AddRange(_kn5CarHelper.LoadAmbientShadows(asList));
                _kn5CarHelper.AdjustPosition(asList);
                _kn5CarHelper.LoadMirrors(asList);
            }

            Scene.UpdateBoundingBox();
            
            Camera = new CameraOrbit(32) {
                Alpha = 30.0f,
                Beta = 0.1f,
                NearZ = 0.2f,
                FarZ = 500f,
                Radius = node.BoundingBox?.GetSize().Length() ?? 4.8f,
                Target = (node.BoundingBox?.GetCenter() ?? Vector3.Zero) - new Vector3(0f, 0.1f, 0f)
            };
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();
            DeviceContextHolder.GetEffect<EffectSimpleMaterial>().FxEyePosW.Set(Camera.Position);
        }

        private float _elapsedCamera;

        protected override void OnTick(float dt) {
            if (AutoRotate) {
                CameraOrbit.Alpha += dt * 0.29f;
                CameraOrbit.Beta += (MathF.Sin(_elapsedCamera * 0.39f) * 0.2f + 0.15f - CameraOrbit.Beta) / 10f;
                _elapsedCamera += dt;
            }
        }
    }
}
