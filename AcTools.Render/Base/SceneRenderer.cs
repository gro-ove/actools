using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;

namespace AcTools.Render.Base {
    public abstract class SceneRenderer : BaseRenderer, IReflectionDraw, IShadowsDraw {
        public readonly RenderableList Scene;

        public CameraBase Camera { get; protected set; }

        protected SceneRenderer() {
            Scene = new RenderableList();
        }

        protected int GetTrianglesCount() {
            return Scene.GetTrianglesCount();
        }

        private bool _lockCamera;

        public bool LockCamera {
            get => _lockCamera;
            set {
                if (Equals(value, _lockCamera)) return;
                _lockCamera = value;
                OnPropertyChanged();
            }
        }

        protected override void ResizeInner() {
            Camera?.SetLens(AspectRatio);
        }

        protected virtual void DrawPrepare() {
            Camera?.UpdateViewMatrix();
        }

        protected override void DrawOverride() {
            DrawPrepare();
            base.DrawOverride();
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Deferred);
        }

        public virtual void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera) {
            Scene.Draw(holder, camera, SpecialRenderMode.Reflection);
        }

        public virtual void DrawSceneForShadows(DeviceContextHolder holder, ICamera camera) {
            Scene.Draw(holder, camera, SpecialRenderMode.Shadow);
        }

        protected override void DisposeOverride() {
            Scene.Dispose();
            base.DisposeOverride();
        }
    }
}
