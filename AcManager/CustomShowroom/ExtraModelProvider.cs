using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using AcManager.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.CustomShowroom {
    public class ExtraModelProvider : IExtraModelProvider {
        private static ExtraModelProvider _instance;

        public static void Initialize() {
            if (_instance != null) return;
            _instance = new ExtraModelProvider();
            ExtraModels.Register(_instance);
        }

        public async Task<byte[]> GetModel(string key) {
            if (key != ExtraModels.KeyCrewExtra) return null;

            using (var dialog = new WaitingDialog()) {
                dialog.Report(ControlsStrings.Common_Downloading);

                var data = await CmApiProvider.GetStaticDataAsync("cs_crew", dialog, dialog.CancellationToken);
                if (data == null) return null;

                return await Task.Run(() => {
                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        return archive.GetEntry("Crew.kn5").Open().ReadAsBytesAndDispose();
                    }
                });
            }
        }
    }
}