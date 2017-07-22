using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Render.Base.Utils {
    internal static class TaskExtension {
        public static bool IsCanceled([CanBeNull] this Exception e) {
            for (; e != null; e = (e as AggregateException)?.GetBaseException()) {
                if (e is OperationCanceledException) return true;
            }
            return false;
        }

        public static void Forget(this Task task) { }
    }
}