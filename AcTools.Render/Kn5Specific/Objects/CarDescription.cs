using System.IO;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public class CarDescription : IWithId {
        [CanBeNull]
        private readonly string _carDirectory;

        [CanBeNull]
        private IKn5 _kn5Loaded;

        [NotNull]
        public string Id => MainModelFilename;

        [NotNull]
        public string MainModelFilename { get; }

        [CanBeNull]
        public IDataReadWrapper Data { get; }

        public Task LoadAsync() {
            return Task.Run(() => {
                _kn5Loaded = Kn5.FromFile(MainModelFilename);
            });
        }

        public CarDescription([NotNull] string mainModelFilename, string carDirectory = null, IDataReadWrapper data = null) {
            MainModelFilename = mainModelFilename;
            _carDirectory = carDirectory;
            Data = data;
        }

        public static CarDescription FromDirectory(string carDirectory) {
            return new CarDescription(AcPaths.GetMainCarFilename(carDirectory, true) ?? "", carDirectory);
        }

        public static CarDescription FromDirectory(string carDirectory, IDataReadWrapper data) {
            return new CarDescription(AcPaths.GetMainCarFilename(carDirectory, data, true) ?? "", carDirectory, data);
        }

        public static CarDescription FromKn5(IKn5 kn5) {
            return new CarDescription(kn5.OriginalFilename ?? "") {
                _kn5Loaded = kn5
            };
        }

        public static CarDescription FromKn5(IKn5 kn5, string carDirectory, IDataReadWrapper data = null) {
            return new CarDescription(kn5.OriginalFilename ?? "", carDirectory, data) {
                _kn5Loaded = kn5
            };
        }

        public IKn5 MainModel => _kn5Loaded ?? (_kn5Loaded = Kn5.FromFile(MainModelFilename));

        public string CarDirectory => _carDirectory ?? Path.GetDirectoryName(MainModelFilename) ?? "";
    }
}