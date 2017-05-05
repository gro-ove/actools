using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shaders {
    // To make it more type-strict (and avoid losing tons of hours because of accidental “Set()” instead of “SetMatrix()” in the future! Arghh…)
    public class EffectOnlyMatrixVariable {
        [CanBeNull]
        private readonly EffectMatrixVariable _v;

        public EffectOnlyMatrixVariable(EffectVariable v) {
            _v = v.AsMatrix();
        }

        public bool IsValid => _v != null;

        public void SetMatrix(Matrix m) {
            _v?.SetMatrix(m);
        }
    }

    public class EffectOnlyMatrixArrayVariable {
        [CanBeNull]
        private readonly EffectMatrixVariable _v;

        public EffectOnlyMatrixArrayVariable(EffectVariable v) {
            _v = v.AsMatrix();
        }

        public bool IsValid => _v != null;

        public void SetMatrixArray(Matrix[] m) {
            _v?.SetMatrixArray(m);
        }

        public void SetMatrixArray(Matrix[] m, int offset, int count) {
            _v?.SetMatrixArray(m, offset, count);
        }
    }

    public class EffectOnlyResourceVariable {
        [CanBeNull]
        private readonly EffectResourceVariable _v;

        public EffectOnlyResourceVariable(EffectVariable v) {
            _v = v.AsResource();
        }

        public bool IsValid => _v != null;

        public void SetResource(ShaderResourceView m) {
            _v?.SetResource(m);
        }
    }

    public class EffectOnlyResourceArrayVariable {
        [CanBeNull]
        private readonly EffectResourceVariable _v;

        public EffectOnlyResourceArrayVariable(EffectVariable v) {
            _v = v.AsResource();
        }

        public bool IsValid => _v != null;

        public void SetResourceArray(ShaderResourceView[] m) {
            _v?.SetResourceArray(m);
        }

        public void SetResourceArray(ShaderResourceView[] m, int offset, int count) {
            _v?.SetResourceArray(m, offset, count);
        }
    }

    public class EffectOnlyIntVariable {
        [CanBeNull]
        private readonly EffectScalarVariable _v;

        public EffectOnlyIntVariable(EffectVariable v) {
            _v = v.AsScalar();
        }

        public bool IsValid => _v != null;
        
        public void Set(int m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyFloatVariable {
        [CanBeNull]
        private readonly EffectScalarVariable _v;

        public EffectOnlyFloatVariable(EffectVariable v) {
            _v = v.AsScalar();
        }

        public bool IsValid => _v != null;

        public void Set(float m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyBoolVariable {
        [CanBeNull]
        private readonly EffectScalarVariable _v;

        public EffectOnlyBoolVariable(EffectVariable v) {
            _v = v.AsScalar();
        }

        public bool IsValid => _v != null;

        public void Set(bool m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyIntArrayVariable {
        [CanBeNull]
        private readonly EffectScalarVariable _v;

        public EffectOnlyIntArrayVariable(EffectVariable v) {
            _v = v.AsScalar();
        }

        public bool IsValid => _v != null;
        
        public void Set(int[] m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyFloatArrayVariable {
        [CanBeNull]
        private readonly EffectScalarVariable _v;

        public EffectOnlyFloatArrayVariable(EffectVariable v) {
            _v = v.AsScalar();
        }

        public bool IsValid => _v != null;

        public void Set(float[] m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyBoolArrayVariable {
        [CanBeNull]
        private readonly EffectScalarVariable _v;

        public EffectOnlyBoolArrayVariable(EffectVariable v) {
            _v = v.AsScalar();
        }

        public bool IsValid => _v != null;

        public void Set(bool[] m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyVector2Variable {
        [CanBeNull]
        private readonly EffectVectorVariable _v;

        public EffectOnlyVector2Variable(EffectVariable v) {
            _v = v.AsVector();
        }

        public bool IsValid => _v != null;

        public void Set(Vector2 m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyVector3Variable {
        [CanBeNull]
        private readonly EffectVectorVariable _v;

        public EffectOnlyVector3Variable(EffectVariable v) {
            _v = v.AsVector();
        }

        public bool IsValid => _v != null;

        public void Set(Vector3 m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyVector4Variable {
        [CanBeNull]
        private readonly EffectVectorVariable _v;

        public EffectOnlyVector4Variable(EffectVariable v) {
            _v = v.AsVector();
        }

        public bool IsValid => _v != null;

        public void Set(Vector4 m) {
            _v?.Set(m);
        }

        public void Set(Color4 m) {
            _v?.Set(m);
        }
    }

    public class EffectOnlyVectorArrayVariable {
        [CanBeNull]
        private readonly EffectVectorVariable _v;

        public EffectOnlyVectorArrayVariable(EffectVariable v) {
            _v = v.AsVector();
        }

        public bool IsValid => _v != null;

        public void Set(Vector4[] m) {
            _v?.Set(m);
        }
    }
}