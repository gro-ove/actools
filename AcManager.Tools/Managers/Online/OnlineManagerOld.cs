using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers.Online {
    public class OnlineManagerOld : BaseOnlineManagerOld {
        private static OnlineManagerOld _instance;

        public static OnlineManagerOld Instance => _instance ?? (_instance = new OnlineManagerOld());

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            ErrorFatal = false;
            Pinged = 0;

            if (SteamIdHelper.Instance.Value == null) {
                ErrorFatal = true;
                throw new Exception(ToolsStrings.Common_SteamIdIsMissing);
            }

            var data = KunosApiProvider.TryToGetList()?.Select(x => new ServerEntryOld(this, x));
            if (data != null) {
                return data;
            }
                
            throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
        }

        protected override async Task<IEnumerable<AcPlaceholderNew>> ScanInnerAsync() {
            ErrorFatal = false;
            Pinged = 0;

            if (SteamIdHelper.Instance.Value == null) {
                ErrorFatal = true;
                throw new Exception(ToolsStrings.Common_SteamIdIsMissing);
            }

            var data = await Task.Run(() => KunosApiProvider.TryToGetList()?.Select(x => new ServerEntryOld(this, x)).ToList());
            if (data != null) {
                return data;
            }

            throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
        }
    }
}
