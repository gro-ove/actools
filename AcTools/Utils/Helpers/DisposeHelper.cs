using System;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class DisposeHelper {
        public static void Dispose<T> ([CanBeNull]ref T disposable) where T : class, IDisposable {
            if (disposable == null) return;
            disposable.Dispose();
            disposable = null;
        }
    }
}
