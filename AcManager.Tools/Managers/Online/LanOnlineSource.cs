using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers.Online {
    public class LanOnlineSource : IOnlineBackgroundSource {
        public const string Key = @"lan";
        public static readonly LanOnlineSource Instance = new LanOnlineSource();

        string IWithId.Id => Key;

        public string DisplayName => "LAN";

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        public async Task<bool> LoadAsync(ItemAddCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            Logging.Here();
            await KunosApiProvider.TryToGetLanListAsync(callback, progress, cancellation);
            return !cancellation.IsCancellationRequested;
        }
    }
}