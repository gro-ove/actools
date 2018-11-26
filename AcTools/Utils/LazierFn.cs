using System;

namespace AcTools.Utils {
    // TODO: Unite Laziers?
    public class LazierFn<TInput, TOutput> {
        private bool _set;
        private readonly Func<TInput, TOutput> _fn;
        private TInput _input;
        private TOutput _value;

        public LazierFn(Func<TInput, TOutput> fn) {
            _fn = fn;
        }

        public TOutput Get(TInput input) {
            if (!_set || !Equals(_input, input)) {
                _set = true;
                _input = input;
                _value = _fn(input);
            }

            return _value;
        }

        public void Reset() {
            _set = false;
            _value = default;
        }
    }
}