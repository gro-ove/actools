using System;

namespace AcTools.Utils {
    // Added, because usual Lazier or Lazy can’t be initialized from field initializer (or delegate
    // or func won’t be able to use “this”).
    public struct LazierThis<T> {
        private bool _set;
        private T _value;

        public T Get(Func<T> fn) {
            if (!_set) {
                _set = true;
                _value = fn();
            }

            return _value;
        }

        public void Reset() {
            _set = false;
            _value = default(T);
        }
    }
}