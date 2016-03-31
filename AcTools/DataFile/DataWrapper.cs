using System.IO;
using AcTools.AcdFile;

namespace AcTools.DataFile {
    public class DataWrapper {
        private readonly string _carDirectory;
        private readonly Acd _acd;

        private DataWrapper(string carDirectory) {
            _carDirectory = carDirectory;

            var dataAcd = Path.Combine(carDirectory, "data.acd");
            if (File.Exists(dataAcd)) {
                _acd = Acd.FromFile(dataAcd);
            } else {
                var dataDirectory = Path.Combine(carDirectory, "data");
                if (Directory.Exists(dataDirectory)) {
                    _acd = Acd.FromDirectory(dataDirectory);
                }
            }
        }

        public bool IsEmpty {
            get { return _acd == null; }
        }

        public IniFile GetIniFile(string name){
            return new IniFile(_carDirectory, name, _acd);
        }

        public LutDataFile GetLutFile(string name){
            return new LutDataFile(_carDirectory, name, _acd);
        }

        public RawDataFile GetRawFile(string name){
            return new RawDataFile(_carDirectory, name, _acd);
        }

        public static DataWrapper FromFile(string carDirectory) {
            if (!Directory.Exists(carDirectory)) {
                throw new DirectoryNotFoundException(carDirectory);
            }

            return new DataWrapper(carDirectory);
        }
    }
}
