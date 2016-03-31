using System.Diagnostics;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Objects;

namespace AcTools.Render.Base {
    public abstract class SceneRenderer : AbstractRenderer, IReflectionDraw {
        public readonly RenderableList Scene;

        public AbstractCamera Camera { get; protected set; } 

        protected SceneRenderer() {
            Scene = new RenderableList();
        }

        protected override void ResizeInner() {
            Camera.SetLens(AspectRatio);
        }

        protected virtual void DrawPrepare() {
            Camera.UpdateViewMatrix();
        }

        protected override void DrawInner() {
            DrawPrepare();
            base.DrawInner();
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Default);
        }

        public virtual void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera) {
            Scene.Draw(holder, camera, SpecialRenderMode.Reflection);
        }

        public override void Dispose() {
            Scene.Dispose();
            base.Dispose();
        }
    }
}
