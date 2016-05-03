using System.Threading.Tasks;

namespace AcTools.Render.Base.Utils {
    internal static class TaskExtension {
        public static void Forget(this Task task) { }
    }
}