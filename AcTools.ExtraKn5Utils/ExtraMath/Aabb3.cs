using System;
using AcTools.Numerics;

namespace AcTools.ExtraKn5Utils.ExtraMath {
    public struct Aabb3 {
        public Vec3 Min;
        public Vec3 Max;

        public static Aabb3 CreateNew() {
            return new Aabb3 {
                Min = new Vec3(float.MaxValue),
                Max = new Vec3(-float.MaxValue)
            };
        }

        public bool IsSet => Max.X >= Min.X;

        public void Extend(Vec3 v) {
            for (var i = 0; i < 3; ++i) {
                Min[i] = Math.Min(Min[i], v[i]);
                Max[i] = Math.Max(Max[i], v[i]);
            }
        }

        public void Extend(Aabb3 v) {
            if (v.IsSet) {
                Extend(v.Min);
                Extend(v.Max);
            }
        }

        public Vec3 Size => Max - Min;
        public Vec3 Center => (Max + Min) / 2f;

        public override string ToString() {
            return $"[Min:{Min} Max:{Max}]";
        }
    }
}