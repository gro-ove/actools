using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers.Online {
    public class LanManager : BaseOnlineManager {
        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new LanManager();
        }

        public static LanManager Instance { get; private set; }

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            var result = new List<ServerEntry>();

            KunosApiProvider.TryToGetLanList(i => {
                try {
                    result.Add(new ServerEntry(this, i));
                } catch (Exception e) {
                    Logging.Warning("[LanManager] Cannot create ServerEntry: " + e);
                }
            });
            
            return result;
        }

        protected override Task<IEnumerable<AcPlaceholderNew>> ScanInnerAsync() {
            ScanDefferedAsync().Forget();
            return Task.FromResult((IEnumerable<AcPlaceholderNew>)new AcPlaceholderNew[0]);
        }

        private async Task ScanDefferedAsync() {
            BackgroundLoading = true;
            Pinged = 0;

            await Task.Run(() => {
                KunosApiProvider.TryToGetLanList(async i => {
                    try {
                        var entry = new ServerEntry(this, i);
                        InnerWrappersList.Add(new AcItemWrapper(this, entry));
                        if (entry.Status == ServerStatus.Unloaded) {
                            await entry.Update(ServerEntry.UpdateMode.Lite); // BUG: Wait?
                        }
                        Pinged++;
                    } catch (Exception e) {
                        Logging.Warning("[LanManager] Cannot create ServerEntry: " + e);
                    }
                });
            });

            BackgroundLoading = false;
        }
    }
}
