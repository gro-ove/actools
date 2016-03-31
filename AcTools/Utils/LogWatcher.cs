using System.IO;
using System.Linq;

namespace AcTools.Utils {
    public class LogWatcher {
        private readonly string _logFile;
        private readonly string _tempFile;
        private int _prev, _skip;

        public LogWatcher(string logFile) {
            _logFile = logFile;
            _tempFile = logFile + "~";
            _skip = 0;
        }

        public string[] GetAll() {
            if (File.Exists(_tempFile)) {
                File.Delete(_tempFile);
            }

            File.Copy(_logFile, _tempFile);
            var result = File.ReadAllLines(_tempFile);
            File.Delete(_tempFile);

            _prev = result.Length;
            return result;
        }

        public void Reset() {
            _skip = _prev;
        }

        public string[] Get() {
            return GetAll().Skip(_skip).ToArray();
        }
    }
}
