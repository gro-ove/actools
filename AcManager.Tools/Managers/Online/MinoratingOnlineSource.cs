using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers.Online {
    public class MinoratingOnlineSource : IOnlineListSource {
        public const string Key = @"minorating";
        public static readonly MinoratingOnlineSource Instance = new MinoratingOnlineSource();

        string IWithId<string>.Id => Key;

        public string DisplayName => "Minorating";

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        public async Task<bool> LoadAsync(ListAddAsyncCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var data = await Task.Run(() => KunosApiProvider.TryToGetMinoratingList(), cancellation);
            // if (cancellation.IsCancellationRequested) return false;

            if (data == null) {
                throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
            }

            await callback(data).ConfigureAwait(false);
            return true;
        }
    }
}