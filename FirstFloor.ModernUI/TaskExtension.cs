using System.Threading.Tasks;

namespace FirstFloor.ModernUI {
    internal static class TaskExtension {
        internal static void Forget(this Task task) {}

        internal static void Forget<T>(this Task<T> task) {}
    }
}