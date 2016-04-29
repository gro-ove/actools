/*namespace AcTools.Render.Base.Shaders {
    public interface IFxMatrixVariable {
        void Set(Matrix value);

        void Set(Matrix[] value);
    }

    public interface IFxResourceVariable {
        void Set(ShaderResourceView value);

        void Set(ShaderResourceView[] value);
    }

    public interface IFxScalarVariable {
        void Set(float value);

        void Set(float[] value);

        void Set(int value);

        void Set(int[] value);

        void Set(bool value);

        void Set(bool[] value);
    }

    public interface IFxVectorVariable {
        void Set(Vector2 value);

        void Set(Vector3 value);

        void Set(Vector4 value);
    }

    public class FxEffectMatrixVariable : IFxMatrixVariable {
        private readonly EffectMatrixVariable _inner;

        public FxEffectMatrixVariable(EffectMatrixVariable inner) {
            _inner = inner;
        }

        public void Set(Matrix value) {
            _inner.SetMatrix(value);
        }

        public void Set(Matrix[] value) {
            _inner.SetMatrixArray(value);
        }
    }

    public class FxEffectResourceVariable : IFxResourceVariable {
        private readonly EffectResourceVariable _inner;

        public FxEffectResourceVariable(EffectResourceVariable inner) {
            _inner = inner;
        }

        public void Set(ShaderResourceView value) {
            _inner.SetResource(value);
        }

        public void Set(ShaderResourceView[] value) {
            _inner.SetResourceArray(value);
        }
    }

    public class FxEffectScalarVariable : IFxScalarVariable {
        private readonly EffectScalarVariable _inner;

        public FxEffectScalarVariable(EffectScalarVariable inner) {
            _inner = inner;
        }

        public void Set(float value) {
            _inner.Set(value);
        }

        public void Set(float[] value) {
            _inner.Set(value);
        }

        public void Set(int value) {
            _inner.Set(value);
        }

        public void Set(int[] value) {
            _inner.Set(value);
        }

        public void Set(bool value) {
            _inner.Set(value);
        }

        public void Set(bool[] value) {
            _inner.Set(value);
        }
    }
}*/