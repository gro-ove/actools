using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors.Solver {
    public interface ISolver {
        void OnSuccess(Solution selectedSolution);

        void OnError(Solution selectedSolution);

        IEnumerable<Solution> Solutions { get; }
    }

    public abstract class AbstractSolver<T> : ISolver where T : AcObjectNew {
        [NotNull]
        public readonly T Target;

        [NotNull]
        public readonly AcError Error;

        protected AbstractSolver([NotNull] T target, [NotNull] AcError error) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (error == null) {
                throw new ArgumentNullException(nameof(error));
            }

            Target = target;
            Error = error;
        }

        protected abstract IEnumerable<Solution> GetSolutions();

        public virtual void OnSuccess(Solution selectedSolution) {
        }

        public virtual void OnError(Solution selectedSolution) {
        }

        private IReadOnlyList<Solution> _solutions;

        [NotNull]
        public IEnumerable<Solution> Solutions => _solutions ?? (_solutions = GetSolutions().ToList());

        public static IEnumerable<Solution> TryToFindRenamedFile(string baseDirectory, string filename) {
            return FileUtils.FindRenamedFile(baseDirectory, filename)
                .Select(x => new Solution($@"Restore from …{ x.Substring(baseDirectory.Length)}",
                    @"Original file will be moved to Recycle Bin if exists",
                    () => {
                        var directory = Path.GetDirectoryName(filename);
                        if (directory == null) throw new IOException("directory = null");

                        if (!Directory.Exists(directory)) {
                            Directory.CreateDirectory(directory);
                        }

                        if (File.Exists(filename)) {
                            FileUtils.Recycle(filename);
                        }

                        File.Move(x, filename);
                    }));
        }

        public static IEnumerable<Solution> TryToFindAnyFile(string baseDirectory, string filename, string searchPattern) {
            return Directory.GetFiles(baseDirectory, searchPattern)
                .Select(x => new Solution($@"Restore from …{ x.Substring(baseDirectory.Length) }",
                    @"Original file will be moved to Recycle Bin if exists",
                    () => {
                        var directory = Path.GetDirectoryName(filename);
                        if (directory == null) throw new IOException("directory = null");

                        if (!Directory.Exists(directory)) {
                            Directory.CreateDirectory(directory);
                        }

                        if (File.Exists(filename)) {
                            FileUtils.Recycle(filename);
                        }

                        File.Move(x, filename);
                    }));
        }

        protected static bool TryToRestoreDamagedJsonFile(string filename, JObjectRestorationScheme scheme) {
            var data = File.ReadAllText(filename);
            var jObject = JsonExtension.TryToRestore(data, scheme);
            if (jObject == null) return false;

            FileUtils.Recycle(filename);
            File.WriteAllText(filename, jObject.ToString());
            return true;
        }
    }
}
