using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableCollider : Kn5RenderableFile {
        public Kn5RenderableCollider(Kn5 kn5, Matrix matrix, bool asyncTexturesLoading = true, IKn5ToRenderableConverter converter = null)
                : base(kn5, matrix, asyncTexturesLoading, converter) {
            foreach (var mesh in Dummies) {
                mesh.LocalMatrix = Matrix.Identity;
            }

            foreach (var mesh in Meshes) {
                mesh.SetTransparent(true);
            }
        }

        protected override Kn5SharedMaterials InitializeMaterials(IDeviceContextHolder contextHolder) {
            return new ColliderSharedMaterials(contextHolder, OriginalFile);
        }

        public class ColliderSharedMaterials : Kn5SharedMaterials {
            public ColliderSharedMaterials(IDeviceContextHolder holder, Kn5 kn5) : base(holder, kn5) { }

            protected override IRenderableMaterial CreateMaterial(object key) {
                return base.CreateMaterial(BasicMaterials.DebugColliderKey);
            }
        }

        public override void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (mode == SpecialRenderMode.SimpleTransparent) {
                base.Draw(holder, camera, mode, filter);
            }
        }
    }
}