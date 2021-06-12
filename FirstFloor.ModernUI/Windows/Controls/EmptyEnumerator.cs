using System;
using System.Collections;

namespace FirstFloor.ModernUI.Windows.Controls {
    internal class EmptyEnumerator : IEnumerator {
        private EmptyEnumerator() { }

        public static IEnumerator Instance => _instance ?? (_instance = new EmptyEnumerator());

        public void Reset() { }

        public bool MoveNext() { return false; }

        public object Current => throw new InvalidOperationException();

        private static IEnumerator _instance;
    }
}