using System.IO;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public class CarDescription {
        [NotNull]
        public string MainKn5File { get; }

        [CanBeNull]
        public string CarDirectory { get; }

        [NotNull]
        public string CarDirectoryRequire => CarDirectory ?? Path.GetDirectoryName(MainKn5File) ?? "";

        [CanBeNull]
        public DataWrapper Data { get; }

        [CanBeNull]
        internal Kn5 Kn5Loaded { get; private set; }

        [NotNull]
        public Kn5 Kn5LoadedRequire => Kn5Loaded ?? (Kn5Loaded = Kn5.FromFile(MainKn5File));

        public Task LoadAsync() {
            return Task.Run(() => {
                Kn5Loaded = Kn5.FromFile(MainKn5File);
            });
        }

        public CarDescription(string mainKn5File, string carDirectory = null, DataWrapper data = null) {
            MainKn5File = mainKn5File;
            CarDirectory = carDirectory;
            Data = data;
        }

        public static CarDescription FromDirectory(string carDirectory) {
            return new CarDescription(FileUtils.GetMainCarFilename(carDirectory), carDirectory);
        }

        public static CarDescription FromDirectory(string carDirectory, DataWrapper data) {
            return new CarDescription(FileUtils.GetMainCarFilename(carDirectory, data), carDirectory, data);
        }

        public static CarDescription FromKn5(Kn5 kn5) {
            return new CarDescription(kn5.OriginalFilename) {
                Kn5Loaded = kn5
            };
        }

        public static CarDescription FromKn5(Kn5 kn5, string carDirectory, DataWrapper data = null) {
            return new CarDescription(kn5.OriginalFilename, carDirectory, data) {
                Kn5Loaded = kn5
            };
        }
    }
}