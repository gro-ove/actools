using System.Collections.Generic;
using System.Windows;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public abstract class ElementPool {
        public static readonly ElementPool EmptyPool = new EmptyPoolInner();

        private class EmptyPoolInner : ElementPool {
            public EmptyPoolInner() : base(null) { }
            protected override FrameworkElement CloneContentIcon() => null;
        }

        protected readonly bool CloneMode;

        [CanBeNull]
        protected readonly FrameworkElement Original;

        private bool _firstGot;
        private List<FrameworkElement> _pool;

        protected ElementPool() {
            CloneMode = false;
        }

        protected ElementPool([CanBeNull] FrameworkElement original) {
            CloneMode = true;
            Original = original;
        }

        [CanBeNull]
        public FrameworkElement Get() {
            if (CloneMode && (!_firstGot || Original == null)) {
                _firstGot = true;
                return Original;
            }

            if (_pool != null) {
                var poolIndex = _pool.Count - 1;
                if (poolIndex >= 0) {
                    var last = _pool[poolIndex];
                    _pool.RemoveAt(poolIndex);
                    return last;
                }
            }

            return CloneContentIcon();
        }

        public void Release([CanBeNull] FrameworkElement icon) {
            if (icon == null) return;

            if (_pool == null) {
                _pool = new List<FrameworkElement>();
            } else if (_pool.Count > 50) {
                return;
            }

            _pool.Add(icon);
        }

        protected abstract FrameworkElement CloneContentIcon();
    }
}