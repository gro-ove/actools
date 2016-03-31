using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AcTools.Utils {
    public partial class FileUtils {
        public static IEnumerable<string> FindRenamedFile(string baseDirectory, string missingFilename) {
            var currentPath = Path.GetDirectoryName(missingFilename);
            if (currentPath == null) yield break;

            if (currentPath == baseDirectory) {
                foreach (var candidate in Directory.GetFiles(currentPath).Where(candidate => CouldBeRenamedFile(baseDirectory, missingFilename, candidate))) {
                    yield return candidate;
                }
            } else {
                foreach (var candidate in GetFiles(baseDirectory).Where(candidate => candidate != missingFilename && 
                        CouldBeRenamedFile(baseDirectory, missingFilename, candidate))) {
                    yield return candidate;
                }
            }
        }

        private static bool CouldBeRenamedFile(string baseDirectory, string original, string candidate) {
            if (!original.StartsWith(baseDirectory) && !candidate.StartsWith(baseDirectory) || original == candidate) return false;

            var originalDirectory = Path.GetDirectoryName(original);
            var candidateDirectory = Path.GetDirectoryName(candidate);
            if (originalDirectory != candidateDirectory &&
                !CouldBeRenamedFile(baseDirectory, originalDirectory, candidateDirectory)) return false;

            var originalFile = Path.GetFileName(original);
            var candidateFile = Path.GetFileName(candidate);

            if (candidateFile == null) return false;

            // if it pass init test (==) and pass here, must be renamed directory
            if (originalFile == candidateFile) return true;

            if (candidateFile.Contains(originalFile)) return true;

            for (var i = 0; i < originalFile.Length * 0.3; i++) {
                if (candidateFile.Contains(originalFile.Substring(i)) ||
                    candidateFile.Contains(originalFile.Substring(0, originalFile.Length - i))) return true;
            }

            return false;
        }
    }
}
