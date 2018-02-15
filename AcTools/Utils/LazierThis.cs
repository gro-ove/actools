using System;

namespace AcTools.Utils {
    // Added, because usual Lazier or Lazy can’t be initialized from field initializer (or delegate
    // or func won’t be able to use “this”).
    // BUG: Apparently, whole idea was stupid and it’s quite unreliable.
    // TODO: Remove it.
    public struct LazierThis<T> {
        private bool _set;
        private T _value;

        public T Get(Func<T> fn) {
            if (!_set) {
                _set = true;
                try {
                    _value = fn();
                } catch (Exception e) {
                    AcToolsLogging.Write(e);
                }
            }

            return _value;
        }

        public void Reset() {
            _set = false;
            _value = default(T);
        }
    }
}