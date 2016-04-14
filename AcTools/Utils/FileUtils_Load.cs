using System.IO;
using System.Text;
using AcTools.Utils.Helpers;

namespace AcTools.Utils {
    public partial class FileUtils {
        public static string ReadAllText(string filename) {
            var bytes = File.ReadAllBytes(filename);
            return bytes.ToUtf8String();
        }
    }
}
