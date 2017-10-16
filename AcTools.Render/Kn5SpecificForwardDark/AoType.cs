using System;
using System.ComponentModel;
using AcTools.Render.Base;
using AcTools.Render.Base.PostEffects.AO;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public enum AoType {
        // Basic space-screen ambient occlusion
        [Description("SSAO")]
        Ssao = 0,

        // Alternative implementation taking horizontal distance into consideration
        [Description("SSAO (Alt.)")]
        SsaoAlt = 1,

        // Horizontal-based ambient occlusion
        [Description("HBAO")]
        Hbao = 2,

        // Adaptive SSAO
        /*[Description("ASSAO")]
        Assao = 3,*/
    }

    public static class AoTypeExtension {
        public static bool IsProductionReady(this AoType type) {
            return type == AoType.Ssao || type == AoType.SsaoAlt || type == AoType.Hbao;
        }

        public static bool IsScreenSpace(this AoType type) {
            return type == AoType.Hbao;
        }

        public static Format GetFormat(this AoType type) {
            switch (type) {
                case AoType.Ssao:
                case AoType.SsaoAlt:
                case AoType.Hbao:
                    return Format.R8_UNorm;
                /*case AoType.Ssgi:
                    return Format.R8G8B8A8_UNorm;*/
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static AoHelperBase GetHelper(this AoType type, DeviceContextHolder holder) {
            switch (type) {
                case AoType.Ssao:
                    return holder.GetHelper<SsaoHelper>();
                case AoType.SsaoAlt:
                    return holder.GetHelper<SsaoAltHelper>();
                case AoType.Hbao:
                    return holder.GetHelper<HbaoHelper>();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}