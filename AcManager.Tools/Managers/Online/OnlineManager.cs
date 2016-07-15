using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.SemiGui;

namespace AcManager.Tools.Managers.Online {
    public class OnlineManager : BaseOnlineManager {
        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new OnlineManager();
        }

        public static OnlineManager Instance { get; private set; }

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            ErrorFatal = false;
            Pinged = 0;

            if (SteamIdHelper.Instance.Value == null) {
                ErrorFatal = true;
                throw new Exception(@"Can’t find Steam ID");
            }

            var data = KunosApiProvider.TryToGetList()?.Select(x => new ServerEntry(this, x));
            if (data != null) {
                return data;
            }
                
            throw new InformativeException(@"Can’t load data", "Make sure internet-connection is working.");
        }

        protected override async Task<IEnumerable<AcPlaceholderNew>> ScanInnerAsync() {
            ErrorFatal = false;
            Pinged = 0;

            if (SteamIdHelper.Instance.Value == null) {
                ErrorFatal = true;
                throw new Exception(@"Can’t find Steam ID");
            }

            var data = await Task.Run(() => KunosApiProvider.TryToGetList()?.Select(x => new ServerEntry(this, x)).ToList());
            if (data != null) {
                return data;
            }

            throw new InformativeException(@"Can’t load data", "Make sure internet-connection is working.");
        }
    }
}
