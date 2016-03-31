using System.Text;

namespace AcTools.AcdFile {
    public class AcdEntry {
        public string Name;
        public byte[] Data;

        /// <summary>
        /// Converts binary data to UTF-8 string.
        /// </summary>
        /// <returns>Output string</returns>
        public override string ToString() {
            return Encoding.UTF8.GetString(Data);
        }
    }
}
