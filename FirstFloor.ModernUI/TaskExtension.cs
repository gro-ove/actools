using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    internal static class TaskExtension {
        public static bool IsCanceled([CanBeNull] this Exception e) {
            for (; e != null; e = (e as AggregateException)?.GetBaseException()) {
                if (e is OperationCanceledException) return true;
            }
            return false;
        }

        internal static void Forget(this Task task) {}
        internal static void Forget<T>(this Task<T> task) {}
    }
}