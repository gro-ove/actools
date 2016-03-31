using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;

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
                throw new Exception(@"Can't find Steam ID.");
            }

            var data = KunosApiProvider.TryToGetList()?.Select(x => new ServerEntry(this, x));
            if (data != null) {
                return data;
            }
                
            throw new Exception(@"Can't load data.");
        }
    }
}
