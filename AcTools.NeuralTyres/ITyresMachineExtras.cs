using System.IO.Compression;
using Newtonsoft.Json.Linq;

namespace AcTools.NeuralTyres {
    public interface ITyresMachineExtras {
        void OnSave(ZipArchive archive, JObject manifest, TyresMachine machine);
        void OnLoad(ZipArchive archive, JObject manifest, TyresMachine machine);
    }
}