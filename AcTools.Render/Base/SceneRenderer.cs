using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;

namespace AcTools.Render.Base {
    public abstract class SceneRenderer : BaseRenderer, IReflectionDraw, IShadowsDraw {
        public readonly RenderableList Scene;
        
        public BaseCamera Camera { get; protected set; }

        protected SceneRenderer() {
            Scene = new RenderableList();
        }

        protected int GetTrianglesCount() {
            return Scene.TrianglesCount;
        }

        protected override void ResizeInner() {
            Camera?.SetLens(AspectRatio);
        }

        protected virtual void DrawPrepare() {
            Camera?.UpdateViewMatrix();
        }

        protected override void DrawInner() {
            DrawPrepare();
            base.DrawInner();
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Deferred);
        }

        public virtual void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera) {
            Scene.Draw(holder, camera, SpecialRenderMode.Reflection);
        }

        public void DrawSceneForShadows(DeviceContextHolder holder, ICamera camera) {
            Scene.Draw(holder, camera, SpecialRenderMode.Shadow);
        }

        public override void Dispose() {
            Scene.Dispose();
            base.Dispose();
        }
    }
}
