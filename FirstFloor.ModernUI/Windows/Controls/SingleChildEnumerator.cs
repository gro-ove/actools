using System.Collections;

namespace FirstFloor.ModernUI.Windows.Controls {
    internal class SingleChildEnumerator : IEnumerator {
        internal SingleChildEnumerator(object child) {
            _child = child;
            _count = child == null ? 0 : 1;
        }

        object IEnumerator.Current => _index == 0 ? _child : null;

        bool IEnumerator.MoveNext() {
            return ++_index < _count;
        }

        void IEnumerator.Reset() {
            _index = -1;
        }

        private int _index = -1;
        private readonly int _count;
        private readonly object _child;
    }
}