// #define REFLECTION_DEBUG

using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleReflective : Kn5MaterialSimple {
        private EffectDarkMaterial.ReflectiveMaterial _material;

#if REFLECTION_DEBUG && DEBUG
        private readonly bool _debugReflectionsMode;
        private EffectSpecialDebugReflections _debugReflections;
#endif

        public Kn5MaterialSimpleReflective([NotNull] Kn5MaterialDescription description) : base(description) {
#if REFLECTION_DEBUG && DEBUG
            _debugReflectionsMode = description.Material?.Name == "Material #25";
#endif
        }

        protected virtual bool IsAdditive() {
            return !Equals(Kn5Material.GetPropertyValueAByName("isAdditive"), 0.0f);
        }

        public override void Initialize(IDeviceContextHolder contextHolder) {
#if REFLECTION_DEBUG && DEBUG
            if (_debugReflectionsMode) {
                _debugReflections = contextHolder.GetEffect<EffectSpecialDebugReflections>();
                return;
            }
#endif

            if (IsAdditive()) {
                Flags |= EffectDarkMaterial.IsAdditive;
            }

            _material = new EffectDarkMaterial.ReflectiveMaterial {
                FresnelC = Kn5Material.GetPropertyValueAByName("fresnelC"),
                FresnelExp = Kn5Material.GetPropertyValueAByName("fresnelEXP"),
                FresnelMaxLevel = Kn5Material.GetPropertyValueAByName("fresnelMaxLevel")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
#if REFLECTION_DEBUG && DEBUG
            if (_debugReflectionsMode) {
                contextHolder.DeviceContext.InputAssembler.InputLayout = _debugReflections.LayoutPNTG;
                return true;
            }
#endif

            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxReflectiveMaterial.Set(_material);
            return true;
        }

#if REFLECTION_DEBUG && DEBUG
        public override void SetMatrices(Matrix objectTransform, ICamera camera) {
            if (_debugReflectionsMode) {
                _debugReflections.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
                _debugReflections.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
                _debugReflections.FxWorld.SetMatrix(objectTransform);
                return;
            }

            base.SetMatrices(objectTransform, camera);
        }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            if (_debugReflectionsMode) {
                _debugReflections.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
                return;
            }

            base.Draw(contextHolder, indices, mode);
        }
#endif

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechReflective;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Reflective;
        }
    }
}